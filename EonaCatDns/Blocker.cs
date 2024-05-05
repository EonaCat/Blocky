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

namespace EonaCat.Dns;

internal class Blocker : IDisposable
{
    private readonly ConcurrentQueue<BlockListItem> _blockListUpdateQueue = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _queueLock = new(1, 1);

    private EonaCatTimer _updateDataTimer;

    public Blocker()
    {
        BindEvents();
        Task.Run(ProcessBlockListQueueAsync, _cancellationTokenSource.Token);
    }

    internal static BlockedSetup Setup { get; set; }
    public static bool UpdateBlockList { get; internal set; }
    public static bool UpdateSetup { get; internal set; }
    private EonaCatTimer UpdateDataTimer { get; set; }
    public bool IsDisposed { get; private set; }
    public static int TotalBlocked { get; private set; }
    public static int TotalAllowed { get; private set; }

    public static ConcurrentBag<TaskItem> RunningBlockerTasks { get; } = new();

    public static bool ProgressToConsole { get; set; }

    public static HashSet<string> AllowList { get; private set; }

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
        DatabaseManager.Domains.OnEntityInsertedOrUpdated += Domains_OnEntityInsertedOrUpdated;
    }

    private async void Domains_OnEntityInsertedOrUpdated(object sender, Domain e)
    {
        await GetBlockListCountFromDatabaseAsync().ConfigureAwait(false);
    }

    public static event EventHandler OnUpdateSetup;
    public static event EventHandler OnBlockListCountRetrieved;

    private async Task ProcessBlockListQueueAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
            try
            {
                while (_blockListUpdateQueue.TryDequeue(out var item))
                    await ProcessBlockListFromQueueAsync(item.Entries, item.RedirectionAddress).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await Logger.LogAsync(ex).ConfigureAwait(false);
            }
    }

    private static async Task ProcessBlockListFromQueueAsync(IEnumerable<Uri> blockedEntries, string address)
    {
        address ??= ConstantsDns.DefaultRedirectionAddress;

        var blockListsFolder = Path.Combine(DllInfo.ConfigFolder, "blocklists");
        Directory.CreateDirectory(blockListsFolder);

        AllowList = await DatabaseManager.GetAllowedDomainsAsync().ConfigureAwait(false);

        var entries = blockedEntries as Uri[] ?? blockedEntries.ToArray();
        var tasks = entries.Select(blockedEntry => ProcessBlockListEntryAsync(blockedEntry, address, blockListsFolder))
            .ToList();

        await Logger.LogAsync($"Added {entries.Length} Blocked urls for downloading", ELogType.DEBUG)
            .ConfigureAwait(false);
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task ProcessBlockListEntryAsync(Uri entryUrl, string address, string blockListsFolder)
    {
        try
        {
            var currentBlockList = await DatabaseManager.BlockLists
                                       .FirstOrDefaultAsync(x => x.Url == entryUrl.AbsoluteUri).ConfigureAwait(false) ??
                                   new BlockList
                                   {
                                       CreationDate = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                                       IsEnabled = true,
                                       Name = entryUrl.AbsoluteUri,
                                       Url = entryUrl.AbsoluteUri
                                   };

            if (!currentBlockList.IsEnabled ||
                (DateTime.TryParse(currentBlockList.LastUpdated, out var dateTime) &&
                 dateTime > DateTime.Now.AddMinutes(-10)))
            {
                await Logger.LogAsync(currentBlockList.IsEnabled
                        ? $"Current blockList '{currentBlockList.Name}' was already updated in the last 10 minutes"
                        : $"Current blockList '{currentBlockList.Name}' is not enabled", ELogType.WARNING)
                    .ConfigureAwait(false);
                return;
            }

            await Logger.LogAsync($"Generating download task for {entryUrl.AbsoluteUri}", ELogType.DEBUG)
                .ConfigureAwait(false);

            var task = new BlockListDownloadTask();
            var taskItem = new TaskItem
            {
                Uri = entryUrl,
                Task = TaskHelper.Create(() =>
                    task.GenerateDownloadTaskAsync(address, blockListsFolder, currentBlockList,
                        ProgressToConsole))
            };

            // Start the task
            RunningBlockerTasks.Add(taskItem);
            await Logger.LogAsync($"Generated download task for {entryUrl.AbsoluteUri}", ELogType.DEBUG)
                .ConfigureAwait(false);
            await taskItem.Task.ExecuteAsync(null).ConfigureAwait(false);
        }
        catch (ArgumentException argumentException)
        {
            await Logger.LogAsync(argumentException.Message, ELogType.WARNING).ConfigureAwait(false);
        }
    }

    public Task UpdateBlockedEntriesAsync(IEnumerable<BlockListItem> blockListItems)
    {
        foreach (var entry in blockListItems) _blockListUpdateQueue.Enqueue(entry);
        return Task.CompletedTask;
    }

    internal static double GetProgress(int currentItem, int totalItems)
    {
        return (100.0 * currentItem / totalItems).RoundToDecimalPlaces(2);
    }

    public async Task InitialiseAsync(bool autoUpdate = false)
    {
        _updateDataTimer = new EonaCatTimer(TimeSpan.FromSeconds(5), TimerCallback);
        _updateDataTimer.Start();

        EnableAutomaticUpdates(autoUpdate);
        await GetBlockListCountFromDatabaseAsync();
        await WriteDefaultAllowListAsync();
    }


    private static void EnableAutomaticUpdates(bool autoUpdate)
    {
        if (!autoUpdate)
        {
            return;
        }

        var now = DateTime.Now;
        var next4Am = now.Date.AddHours(4);

        if (now >= next4Am)
        {
            next4Am = next4Am.AddDays(1);
        }

        var timeRemaining = next4Am - now;
        var timer = new Timer(async _ => await StartAutomaticUpdate(now).ConfigureAwait(false), null,
            timeRemaining, TimeSpan.FromHours(24));
    }

    private static async Task StartAutomaticUpdate(DateTime currentDateTime)
    {
        UpdateBlockList = true;
        await Logger.LogAsync($"Automatic blockList updates started at: {currentDateTime.ToLocalTime()}")
            .ConfigureAwait(false);
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
        await Logger.LogAsync("Adding domains to allow").ConfigureAwait(false);

        var allowedDomains = await DatabaseManager.GetAllowedDomainsAsync().ConfigureAwait(false);

        if (!allowedDomains.Any())
        {
            // Add defaults to allowList
            allowedDomains.Add("pool.ntp.org");
            allowedDomains.Add("windowsupdate.com");
        }

        await DatabaseManager.Domains.BulkInsertOrUpdateAsync(allowedDomains.Select(domain =>
            new Domain { Url = domain, ForwardIp = domain, ListType = ListType.Allowed })).ConfigureAwait(false);
        await Logger.LogAsync("Domains to allow added").ConfigureAwait(false);
    }

    public static async Task GetBlockListCountFromDatabaseAsync()
    {
        TotalAllowed = await DatabaseManager.GetAllowedDomainsCountAsync().ConfigureAwait(false);
        TotalBlocked = await DatabaseManager.GetBlockedDomainsCountAsync().ConfigureAwait(false);
        OnBlockListCountRetrieved?.Invoke(null, null!);
    }

    private static async Task GetBlockedSetupAsync()
    {
        var blockedAddressSetting =
            await DatabaseManager.GetSettingAsync(SettingName.Blockedaddress).ConfigureAwait(false);
        var address = string.Empty;
        var blockList =
            (await DatabaseManager.BlockLists.GetAll().Where(x => x.IsEnabled).ToArrayAsync().ConfigureAwait(false))
            .ToHashSet();

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
        if (IsDisposed)
        {
            return;
        }

        // Dispose managed resources
        if (disposing)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _updateDataTimer?.Stop();
        }

        // Dispose unmanaged resources
        IsDisposed = true;
    }
}