using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EonaCat.Blocky.Helpers;
using EonaCat.Dns;
using EonaCat.Dns.Managers.Stats;
using EonaCat.Helpers.Helpers;
using EonaCat.Logger;
using DllInfo = EonaCat.Dns.DllInfo;

namespace EonaCat.Blocky;

public static class BlockyDns
{
    private static EonaCatDns _eonaCatDns;
    public static bool IsRunning { get; private set; }

    public static async Task<StatsOverview> GetDnsStatsAsync()
    {
        return await _eonaCatDns.GetStatsOverviewAsync().ConfigureAwait(false);
    }

    public static async Task StartAsync(ELogType maxLogType = ELogType.DEBUG)
    {
        if (!IsRunning)
        {
            if (!IsRunning)
            {
                IsRunning = true;
            }
            else
            {
                await Logger.LogAsync("Blocky Dns is already running", ELogType.DEBUG).ConfigureAwait(false);
                return;
            }

            Console.Title = $"〠 Blocky {AppInfo.Version} - Blocking domains the way you want it 〠";

            // Initialise blocky
            Logger.UseLocalTime = Config.LogInLocalTime;
            Logger.MaxLogType = maxLogType;

            Logger.Configure();
            await Logger.LogAsync(Config.GetHeader()).ConfigureAwait(false);

            // Hide the version (if needed)
            AppInfo.HideVersion = Config.HideVersion;

            await StartEonaCatDnsAsync().ConfigureAwait(false);

            if (_eonaCatDns.IsRunning)
            {
                if (_eonaCatDns.IsFirstTime && Config.LoadDefaultBlockySetup)
                {
                    await DownloadBlockySetupAsync().ConfigureAwait(false);
                }
            }

            Console.WriteLine(Config.GetHeader());
        }
        else
        {
            await Logger.LogAsync("Blocky Dns is already running", ELogType.DEBUG).ConfigureAwait(false);
        }
    }

    private static async Task DownloadBlockySetupAsync(bool forceReload = false)
    {
        await Logger.LogAsync("Retrieving blockList for Blocky", ELogType.DEBUG);
        var setup = await Config.GetBlockySetupAsync(forceReload).ConfigureAwait(false);

        if (setup != null && !setup.Any())
        {
            return;
        }

        await _eonaCatDns.AddBlockedEntriesAsync(setup, Config.BlockyRedirectionserver).ConfigureAwait(false);
        await Logger.LogAsync("Retrieved blockList for Blocky", ELogType.DEBUG).ConfigureAwait(false);
    }

    private static async Task StartEonaCatDnsAsync()
    {
        await Task.Run(async () =>
        {
            var config = new EonaCatDnsConfig
            {
                LogLevel = Config.Loglevel,
                ListenAddressV4 = Config.BlockyInteraceV4Ips[0],
                ResolverAddressV4 = Config.ResolverAddressV4,
                ForwardersV4 = Config.DnsServers.ToList(),
                ListenAddressV6 = Config.IsV6Enabled ? Config.BlockyInteraceV6Ips[0] : null,
                LogBlockedClients = Config.LogBlockedClients,
                StatsRefreshInterval = Config.StatsRefreshInterval,
                IsCacheDisabled = Config.IsCacheDisabled,
                ResolverAddressV6 = Config.ResolverAddressV6,
                ForwardersV6 = Config.DnsServersV6.ToList(),
                ForwardersDoH = Config.DoHServers.ToList(),
                Port = Config.DnsPort,
                AutoUpdate = Config.AutoUpdate,
                ApplicationName = AppInfo.Name,
                ApplicationVersion = AppInfo.Version,
                WebServerIpAddress = Config.WebserverInteraceIp,
                WebServerPort = Config.WebserverPort,
                EnableAdminInterface = true,
                Resolvers = Config.DnsServers,
                DontCacheList = Config.DontCacheList.ToList(),
                ContinueWhenDohFails = Config.ContinueWhenDohFails,
                ResolveOverDoh = Config.ResolveOverDoh,
                CreateMasterFileOnBoot = Config.CreateMasterFileOnBoot,
                IgnoreWpadRequests = Config.IgnoreWpadRequests,
                IgnoreArpaRequests = Config.IgnoreArpaRequests,
                IncludeRawInLogTable = Config.IncludeRawInLogTable,
                IsMultiCastEnabled = Config.IsMultiCastEnabled,
                RouterDomain = Config.RouterDomain,
                PartialLookupName = Config.PartialLookupName,
                ProgressToConsole = Config.ProgressToConsole,
                LogInLocalTime = Config.LogInLocalTime
            };

            DllInfo.ApplicationVersion = AppInfo.Version;
            DllInfo.ApplicationName = AppInfo.Name;
            DllInfo.HideVersion = AppInfo.HideVersion;

            _eonaCatDns = new EonaCatDns(config);
            _eonaCatDns.OnUpdateSetup -= EonaCatDns_OnUpdateSetup;
            _eonaCatDns.OnUpdateSetup += EonaCatDns_OnUpdateSetup;
            _eonaCatDns.UseExceptionHandling();

            await _eonaCatDns.StartAsync().ConfigureAwait(false);
        });
    }

    private static async void EonaCatDns_OnUpdateSetup(object sender, EventArgs e)
    {
        await DownloadBlockySetupAsync(true).ConfigureAwait(false);
    }

    private static async Task SendStatisticsAsync()
    {
        var previous = Config.Loglevel;
        Config.Loglevel = ELogType.NONE;

        if (Debugger.IsAttached)
        {
            return;
        }

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(
            $"{WebHelper.GetExternalIpAddress()} started Blocky {AppInfo.VersionName} version {AppInfo.Version}");

        var slack = new SlackClient(Config.SlackUrl);
        await slack.PostMessageAsync(new Payload
            { Channel = "Blocky", Username = "Blocky", Text = stringBuilder.ToString() }).ConfigureAwait(false);
        Config.Loglevel = previous;
    }

    private static async Task StopAsync()
    {
        if (_eonaCatDns != null)
        {
            await _eonaCatDns.StopAsync().ConfigureAwait(false);
        }

        await Logger.LogAsync($"Blocky {DllInfo.Version} has been stopped.").ConfigureAwait(false);
        IsRunning = false;
    }
}