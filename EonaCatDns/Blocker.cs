/*
EonaCatDns
Copyright (C) 2017-2023 EonaCat (Jeroen Saey)

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

using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Models;
using EonaCat.Helpers.Commands;
using EonaCat.Helpers.Controls;
using EonaCat.Helpers.Extensions;
using EonaCat.Logger;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EonaCat.Dns;

internal class Blocker : IDisposable
{
    public static event EventHandler OnUpdateSetup;
    public static event EventHandler OnBlockListCountRetrieved;
    private readonly LinkedList<BlockListItem> _blockListUpdateQueue = new();
    internal static BlockedSetup Setup { get; set; }
    public static bool UpdateBlockList { get; internal set; }
    public static bool UpdateSetup { get; internal set; }
    private EonaCatTimer UpdateDataTimer { get; set; }
    public bool IsDisposed { get; private set; }
    public static int TotalBlocked { get; private set; }
    public static int TotalAllowed { get; private set; }

    public Blocker()
    {
        CreateBlockerQueue();
    }

    private void CreateBlockerQueue()
    {
        Task.Run(async () =>
        {
            RunningBlockerTasks = new ConcurrentBag<TaskItem>();
            while (!IsDisposed)
            {
                try
                {
                    var queue = new List<BlockListItem>(_blockListUpdateQueue);
                    if (!queue.Any())
                    {
                        await Task.Delay(5000).ConfigureAwait(false);
                        continue;
                    }

                    foreach (var item in queue)
                    {
                        await ProcessBlockListFromQueueAsync(item.Entries, item.RedirectionAddress).ConfigureAwait(false);
                        _blockListUpdateQueue.Remove(item);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                await Task.Delay(100).ConfigureAwait(false);
            }
        });
    }

    public static ConcurrentBag<TaskItem> RunningBlockerTasks { get; private set; } = new();

    public void Dispose()
    {
        Dispose(true);
    }

    private static async Task ProcessBlockListFromQueueAsync(IEnumerable<Uri> blockedEntries, string address)
    {
        address ??= ConstantsDns.DefaultRedirectionAddress;

        var blockListsFolder = Path.Combine(DllInfo.ConfigFolder, "blocklists");
        Directory.CreateDirectory(blockListsFolder);

        AllowList = await DatabaseManager.GetAllowedDomainsAsync().ConfigureAwait(false);

        var entryUrls = blockedEntries.ToList();
        foreach (var entryUrl in entryUrls)
        {
            try
            {
                // Get the blockList from the database (if exists)
                var currentBlockList = await DatabaseManager.BlockLists
                    .FirstOrDefaultAsync(x => x.Url == entryUrl.AbsoluteUri)
                    .ConfigureAwait(false);

                if (currentBlockList == null)
                {
                    currentBlockList = new Database.Models.Entities.BlockList
                    {
                        CreationDate = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        IsEnabled = true,
                        Name = entryUrl.AbsoluteUri,
                        Url = entryUrl.AbsoluteUri
                    };
                    await DatabaseManager.BlockLists.InsertOrUpdateAsync(currentBlockList).ConfigureAwait(false);
                }

                // Check if the task was recently updated

                if (!currentBlockList.IsEnabled ||
                    DateTime.TryParse(currentBlockList.LastUpdated, out var dateTime) &&
                    dateTime.Ticks > DateTime.Now.AddMinutes(-10).Ticks)
                {
                    Logger.Log(currentBlockList.IsEnabled
                        ? $"Current blockList '{currentBlockList.Name}' was already updated in the last 10 minutes"
                        : $"Current blockList '{currentBlockList.Name}' is not enabled",
                        ELogType.WARNING);
                    return;
                }

                Logger.Log($"Generating download task for {entryUrl.AbsoluteUri}", ELogType.DEBUG);

                var task = new BlockListDownloadTask();
                var taskItem = new TaskItem
                {
                    Uri = entryUrl,
                    Task = TaskHelper.Create(() => task.GenerateDownloadTaskAsync(address, blockListsFolder, currentBlockList))
                };

                // start the task
                RunningBlockerTasks.Add(taskItem);
                Logger.Log($"Generated download task for {entryUrl.AbsoluteUri}", ELogType.DEBUG);
            }
            catch (ArgumentException argumentException)
            {
                Logger.Log(argumentException.Message, ELogType.WARNING);
            }
        }

        Logger.Log($"Added {entryUrls.Count()} Blocked urls for downloading", ELogType.DEBUG);

        // Start all the tasks
        foreach (var task in RunningBlockerTasks)
        {
            await task.Task.ExecuteAsync(null).ConfigureAwait(false);
        }
    }

    public bool ProgressToConsole { get; set; }

    internal Task UpdateBlockedEntriesAsync(IEnumerable<BlockListItem> blockListItems)
    {
        foreach (var entry in blockListItems)
        {
            if (!_blockListUpdateQueue.Contains(entry))
            {
                _blockListUpdateQueue.AddLast(entry);
            }
        }

        return Task.CompletedTask;
    }

    public static HashSet<string> AllowList { get; private set; }

    internal static double GetProgress(int currentItem, int totalItems)
    {
        return (100.0 * currentItem / totalItems).RoundToDecimalPlaces(2);
    }

    internal Task InitialiseAsync(bool autoUpdate = false)
    {
        UpdateDataTimer = new EonaCatTimer(TimeSpan.FromSeconds(5), TimerCallback);
        UpdateDataTimer.Start();

        EnableAutomaticUpdates(autoUpdate);

        // Initiate the allow list
        return GetBlockListCountFromDatabaseAsync();
    }


    public static void EnableAutomaticUpdates(bool autoUpdate)
    {
        if (!autoUpdate)
        {
            return;
        }

        // Get the current time to calculate the time remaining until 4:00 AM
        var now = DateTime.Now;
        var next4Am = now.Date.AddHours(4);

        // If it's already past 4:00 AM today, schedule the next occurrence for tomorrow
        if (now >= next4Am)
        {
            next4Am = next4Am.AddDays(1);
        }

        // Calculate the time remaining until the next 4:00 AM
        var timeRemaining = next4Am - now;

        // Set up a timer to execute the method when the time comes
        var timer = new Timer(_ => StartAutomaticUpdate(now), null, timeRemaining, TimeSpan.FromHours(24));
    }

    private static void StartAutomaticUpdate(DateTime currentDateTime)
    {
        UpdateBlockList = true;
        Logger.Log($"Automatic blockList updates started at: {currentDateTime.ToLocalTime()}");
    }


    private async void TimerCallback()
    {
        if (UpdateBlockList || UpdateSetup)
        {
            if (UpdateBlockList)
            {
                UpdateBlockList = false;
                await ReloadBlockedEntriesAsync().ConfigureAwait(false);
            }

            if (UpdateSetup)
            {
                UpdateSetup = false;
                OnUpdateSetup?.Invoke(null, null!);
            }
        }
    }

    internal async Task WriteDefaultAllowListAsync()
    {
        Logger.Log("Adding domains to allow");

        var allowedDomains = await DatabaseManager.GetAllowedDomainsAsync().ConfigureAwait(false);

        if (!allowedDomains.Any())
        {
            // Add defaults to allowList
            allowedDomains.Add("pool.ntp.org");
            allowedDomains.Add("windowsupdate.com");
        }

        await DatabaseManager.Domains.BulkInsertOrUpdateAsync(allowedDomains.Select(domain =>
            new Domain { Url = domain, ForwardIp = domain, ListType = ListType.Allowed })).ConfigureAwait(false);
        Logger.Log("Domains to allow added");
    }

    public static async Task GetBlockListCountFromDatabaseAsync()
    {
        TotalAllowed = await DatabaseManager.GetAllowedDomainsCountAsync().ConfigureAwait(false);
        TotalBlocked = await DatabaseManager.GetBlockedDomainsCountAsync().ConfigureAwait(false);
        OnBlockListCountRetrieved?.Invoke(null, null!);
    }

    private static async Task GetBlockedSetupAsync()
    {
        var blockedAddressSetting = await DatabaseManager.GetSettingAsync(SettingName.Blockedaddress).ConfigureAwait(false);
        var address = string.Empty;
        var blockList = (await DatabaseManager.BlockLists.GetAll().Where(x => x.IsEnabled).ToArrayAsync().ConfigureAwait(false)).ToHashSet();

        if (!string.IsNullOrEmpty(blockedAddressSetting.Value))
        {
            address = blockedAddressSetting.Value;
        }

        if (blockList.Count > 0)
        {
            Setup = new BlockedSetup { Urls = blockList, RedirectionAddress = address };
        }
    }

    private async Task ReloadBlockedEntriesAsync()
    {
        await GetBlockedSetupAsync().ConfigureAwait(false);

        if (Setup != null && Setup.Urls.Any())
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
        if (!IsDisposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                UpdateDataTimer?.Stop();
            }

            // Dispose unmanaged resources

            IsDisposed = true;
        }
    }
}