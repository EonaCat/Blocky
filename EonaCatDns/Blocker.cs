using EonaCat.Dns.Database;
using EonaCat.Dns.Models;
using EonaCat.Helpers.Commands;
using EonaCat.Helpers.Controls;
using EonaCat.Helpers.Extensions;
using EonaCat.Logger;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Core;
using EonaCat.Dns;
using BlockList = EonaCat.Dns.Database.Models.Entities.BlockList;
using DllInfo = EonaCat.Dns.DllInfo;

internal class Blocker : IDisposable
{
    private readonly ConcurrentQueue<BlockListItem> _blockListUpdateQueue = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private EonaCatTimer _updateDataTimer;
    private static readonly ReaderWriterLockSlim BlockListLock = new();
    private static readonly HashSet<string> _blockListDomains = new();

    public Blocker()
    {
        BindEvents();
        _ = Task.Run(ProcessBlockListQueueAsync, _cancellationTokenSource.Token);
    }

    internal static BlockedSetup Setup { get; set; }
    public static bool UpdateTheBlockList { get; internal set; }
    public static bool UpdateSetup { get; internal set; }
    public bool IsDisposed { get; private set; }
    public static long TotalBlocked { get; private set; }
    public static long TotalAllowed { get; private set; }

    public static ConcurrentBag<TaskItem> RunningBlockerTasks { get; } = new();
    public static bool ProgressToConsole { get; set; }
    public static HashSet<string> AllowList { get; private set; }
    public bool IsUpdating { get; private set; }

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
                // Process blocklist items in the queue asynchronously
                while (_blockListUpdateQueue.TryDequeue(out var item))
                {
                    // Run each blocklist item processing asynchronously without blocking the DNS queries
                    _ = Task.Run(() =>
                    {
                        ProcessBlockListFromQueueAsync(item.Entries, item.RedirectionAddress).ConfigureAwait(false);
                        return Task.CompletedTask;
                    });
                }

                // Prevent long blocking times in this loop
                await Task.Delay(100, _cancellationTokenSource.Token).ConfigureAwait(false);
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

        var newDomains = new HashSet<string>();
        var tasks = blockedEntries.Select(async entry =>
        {
            // Process each entry asynchronously and avoid blocking DNS queries
            await ProcessBlockListEntryAsync(entry, address, blockListsFolder).ConfigureAwait(false);
        });

        await Logger.LogAsync($"Added {blockedEntries.Count()} Blocked URLs for downloading", ELogType.DEBUG).ConfigureAwait(false);
        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Add new domains to the blocklist asynchronously
        UpdateBlockList(newDomains);
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

    public static void UpdateBlockList(IEnumerable<string> domains)
    {
        BlockListLock.EnterWriteLock();
        try
        {
            // Merge new domains with the existing blocklist and remove duplicates
            _blockListDomains.UnionWith(domains);
        }
        finally
        {
            BlockListLock.ExitWriteLock();
        }
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

        await WriteDefaultAllowListAsync().ConfigureAwait(false);
        await GetBlockListCountFromDatabaseAsync().ConfigureAwait(false);
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
        UpdateTheBlockList = true;
        await Logger.LogAsync("Automatic blockList updates started").ConfigureAwait(false);
    }

    private async void TimerCallback()
    {
        try
        {
            if (IsUpdating)
            {
                return;
            }

            IsUpdating = true;
            if (UpdateTheBlockList)
            {
                UpdateTheBlockList = false;
                await ReloadBlockedEntriesAsync().ConfigureAwait(false);
            }

            if (UpdateSetup)
            {
                UpdateSetup = false;
                OnUpdateSetup?.Invoke(null, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            await Logger.LogAsync($"Update callback error: {ex.Message}", ELogType.ERROR).ConfigureAwait(false);
        }
        finally
        {
            IsUpdating = false;
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
            .Select(domain => new Domain { Url = domain, ForwardIp = domain, ListType = ListType.Allowed })).
            ConfigureAwait(false);
    }

    public static async Task GetBlockListCountFromDatabaseAsync()
    {
        TotalAllowed = await DatabaseManager.GetAllowedDomainsCountAsync().ConfigureAwait(false);
        TotalBlocked = await DatabaseManager.GetBlockedDomainsCountAsync().ConfigureAwait(false);
        OnBlockListCountRetrieved?.Invoke(null, EventArgs.Empty);
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

    public Task UpdateBlockedEntriesAsync(IEnumerable<BlockListItem> blockListItems)
    {
        foreach (var entry in blockListItems)
        {
            _blockListUpdateQueue.Enqueue(entry);
        }
        return Task.CompletedTask;
    }

    private async Task GetBlockedSetupAsync()
    {
        var blockedAddressSetting = await DatabaseManager.GetSettingAsync(SettingName.Blockedaddress).ConfigureAwait(false);
        var blockList = (await DatabaseManager.BlockLists.GetAll().Where(x => x.IsEnabled).ToArrayAsync().ConfigureAwait(false)).ToHashSet();

        Setup = blockList.Any()
            ? new BlockedSetup { Urls = blockList, RedirectionAddress = blockedAddressSetting?.Value ?? string.Empty }
            : null;
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
