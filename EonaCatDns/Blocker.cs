/*
EonaCatDns
Copyright (C) 2017-2025 EonaCat (Jeroen Saey)

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Models;
using EonaCat.Helpers.Commands;
using EonaCat.Helpers.Controls;
using EonaCat.Helpers.Extensions;
using EonaCat.Logger;
using BlockList = EonaCat.Dns.Database.Models.Entities.BlockList;

namespace EonaCat.Dns
{
    internal class Blocker : IDisposable
    {
        private readonly ConcurrentQueue<BlockListItem> _blockListUpdateQueue = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private EonaCatTimer _updateDataTimer;

        public Blocker()
        {
            BindEvents();
            _ = Task.Run(ProcessBlockListQueueAsync, _cancellationTokenSource.Token);
        }

        internal static BlockedSetup Setup { get; set; }
        public static bool UpdateBlockList { get; internal set; }
        public static bool UpdateSetup { get; internal set; }
        public bool IsDisposed { get; private set; }
        public static long TotalBlocked { get; private set; }
        public static long TotalAllowed { get; private set; }

        public static ConcurrentBag<TaskItem> RunningBlockerTasks { get; } = new();
        public static bool ProgressToConsole { get; set; }
        public static HashSet<string> AllowList { get; private set; }

        public static event EventHandler OnUpdateSetup;
        public static event EventHandler OnBlockListCountRetrieved;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void BindEvents()
        {
            DatabaseManager.Domains.OnEntityInsertedOrUpdated += Domains_OnEntityInsertedOrUpdated;
        }

        public void UnbindEvents()
        {
            DatabaseManager.Domains.OnEntityInsertedOrUpdated -= Domains_OnEntityInsertedOrUpdated;
        }

        private async void Domains_OnEntityInsertedOrUpdated(object sender, Domain e)
        {
            await GetBlockListCountFromDatabaseAsync().ConfigureAwait(false);
        }

        private async Task ProcessBlockListQueueAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    while (_blockListUpdateQueue.TryDequeue(out var item))
                    {
                        await ProcessBlockListFromQueueAsync(item.Entries, item.RedirectionAddress).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    await Logger.LogAsync(ex).ConfigureAwait(false);
                }
            }
        }

        private static async Task ProcessBlockListFromQueueAsync(IEnumerable<Uri> blockedEntries, string address)
        {
            address ??= ConstantsDns.DefaultRedirectionAddress;
            var blockListsFolder = Path.Combine(DllInfo.ConfigFolder, "blocklists");
            Directory.CreateDirectory(blockListsFolder);

            AllowList ??= await DatabaseManager.GetAllowedDomainsAsync().ConfigureAwait(false);

            var tasks = blockedEntries.Select(entry => ProcessBlockListEntryAsync(entry, address, blockListsFolder));

            await Logger.LogAsync($"Added {blockedEntries.Count()} Blocked urls for downloading", ELogType.DEBUG).ConfigureAwait(false);
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static async Task ProcessBlockListEntryAsync(Uri entryUrl, string address, string blockListsFolder)
        {
            try
            {
                var currentBlockList = await DatabaseManager.BlockLists
                    .FirstOrDefaultAsync(x => x.Url == entryUrl.AbsoluteUri)
                    .ConfigureAwait(false) ?? new BlockList
                    {
                        CreationDate = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture),
                        IsEnabled = true,
                        Name = entryUrl.AbsoluteUri,
                        Url = entryUrl.AbsoluteUri
                    };

                if (!currentBlockList.IsEnabled)
                {
                    await Logger.LogAsync($"BlockList '{currentBlockList.Name}' is not enabled", ELogType.WARNING)
                        .ConfigureAwait(false);
                    return;
                }

                if (IsRecentlyUpdated(currentBlockList))
                {
                    await Logger.LogAsync($"BlockList '{currentBlockList.Name}' was updated in the last 10 minutes", ELogType.WARNING)
                        .ConfigureAwait(false);
                    return;
                }

                var downloadTask = new BlockListDownloadTask();
                var taskItem = new TaskItem
                {
                    Uri = entryUrl,
                    Task = TaskHelper.Create(() => downloadTask.GenerateDownloadTaskAsync(blockListsFolder, currentBlockList, ProgressToConsole))
                };

                RunningBlockerTasks.Add(taskItem);
                await taskItem.Task.ExecuteAsync(null).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                await Logger.LogAsync($"Invalid argument: {ex.Message}", ELogType.WARNING).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.LogAsync($"Unexpected error processing BlockList '{entryUrl}': {ex.Message}", ELogType.ERROR).ConfigureAwait(false);
            }
        }


        private static bool IsRecentlyUpdated(BlockList blockList)
        {
            return DateTime.TryParse(blockList.LastUpdated, out var lastUpdated) &&
                   lastUpdated > DateTime.Now.AddMinutes(-10);
        }

        public Task UpdateBlockedEntriesAsync(IEnumerable<BlockListItem> blockListItems)
        {
            foreach (var entry in blockListItems)
            {
                _blockListUpdateQueue.Enqueue(entry);
            }
            return Task.CompletedTask;
        }

        internal static double GetProgress(int currentItem, int totalItems)
            => (100.0 * currentItem / totalItems).RoundToDecimalPlaces(2);

        public async Task InitialiseAsync(bool autoUpdate = false)
        {
            _updateDataTimer = new EonaCatTimer(TimeSpan.FromSeconds(5), TimerCallback);
            _updateDataTimer.Start();

            if (autoUpdate)
            {
                EnableAutomaticUpdates();
            }

            await GetBlockListCountFromDatabaseAsync().ConfigureAwait(false);
            await WriteDefaultAllowListAsync().ConfigureAwait(false);
        }

        private static void EnableAutomaticUpdates()
        {
            var now = DateTime.Now;
            var next4Am = now.Date.AddHours(4).AddDays(now >= now.Date.AddHours(4) ? 1 : 0);

            new Timer(async _ => await StartAutomaticUpdate().ConfigureAwait(false),
                null, next4Am - now, TimeSpan.FromHours(24));
        }

        private static async Task StartAutomaticUpdate()
        {
            UpdateBlockList = true;
            await Logger.LogAsync("Automatic blockList updates started").ConfigureAwait(false);
        }

        private async void TimerCallback()
        {
            if (UpdateBlockList)
            {
                UpdateBlockList = false;
                await ReloadBlockedEntriesAsync().ConfigureAwait(false);
            }

            if (UpdateSetup)
            {
                UpdateSetup = false;
                OnUpdateSetup?.Invoke(null, EventArgs.Empty);
            }
        }

        internal static async Task WriteDefaultAllowListAsync()
        {
            var allowedDomains = await DatabaseManager.GetAllowedDomainsAsync().ConfigureAwait(false);

            if (!allowedDomains.Any())
            {
                allowedDomains.Add("pool.ntp.org");
                allowedDomains.Add("windowsupdate.com");
            }

            await DatabaseManager.Domains.BulkInsertOrUpdateAsync(allowedDomains
                .Select(domain => new Domain { Url = domain, ForwardIp = domain, ListType = ListType.Allowed }))
                .ConfigureAwait(false);
        }

        public static async Task GetBlockListCountFromDatabaseAsync()
        {
            TotalAllowed = await DatabaseManager.GetAllowedDomainsCountAsync().ConfigureAwait(false);
            TotalBlocked = await DatabaseManager.GetBlockedDomainsCountAsync().ConfigureAwait(false);
            OnBlockListCountRetrieved?.Invoke(null, EventArgs.Empty);
        }

        private static async Task GetBlockedSetupAsync()
        {
            var blockedAddressSetting = await DatabaseManager.GetSettingAsync(SettingName.Blockedaddress).ConfigureAwait(false);
            var blockList = (await DatabaseManager.BlockLists.GetAll().Where(x => x.IsEnabled).ToArrayAsync().ConfigureAwait(false)).ToHashSet();

            Setup = blockList.Any()
                ? new BlockedSetup { Urls = blockList, RedirectionAddress = blockedAddressSetting?.Value ?? string.Empty }
                : null;
        }

        private async Task ReloadBlockedEntriesAsync()
        {
            await GetBlockedSetupAsync().ConfigureAwait(false);

            if (Setup?.Urls?.Any() == true)
            {
                var blockedEntries = Setup.Urls.Where(x => x.IsEnabled).Select(x => new Uri(x.Url)).ToList();
                Setup.Urls.Clear();

                await UpdateBlockedEntriesAsync(blockedEntries.Select(entry => new BlockListItem
                {
                    Entries = new HashSet<Uri> { entry },
                    RedirectionAddress = Setup.RedirectionAddress
                })).ConfigureAwait(false);
            }

            await GetBlockListCountFromDatabaseAsync().ConfigureAwait(false);
        }

        private void Dispose(bool disposing)
        {
            if (IsDisposed) return;

            if (disposing)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _updateDataTimer?.Stop();
            }

            IsDisposed = true;
        }
    }
}
