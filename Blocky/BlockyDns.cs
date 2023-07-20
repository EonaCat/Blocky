using EonaCat.Blocky.Helpers;
using EonaCat.Dns;
using EonaCat.Dns.Managers.Stats;
using EonaCat.Helpers.Helpers;
using EonaCat.Logger;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DllInfo = EonaCat.Dns.DllInfo;

namespace EonaCat.Blocky
{
    // Blocky
    // Blocking domains the way you want it.
    // Copyright EonaCat (Jeroen Saey) 2017-2023
    // https://blocky.EonaCat.com

    public static class BlockyDns
    {
        private static EonaCatDns _eonaCatDns;
        public static bool IsRunning { get; private set; }

        public static Task<StatsOverview> GetDnsStatsAsync()
        {
            return _eonaCatDns.GetStatsOverviewAsync();
        }

        public static async Task StartAsync(ELogType maxLogType = ELogType.INFO)
        {
            if (!IsRunning)
            {
                IsRunning = true;

                Console.Title = $"〠 Blocky {AppInfo.Version} - Blocking domains the way you want it 〠";

                // Initialise blocky
                Logger.UseLocalTime = Config.LogInLocalTime;
                Logger.MaxLogType = maxLogType;

                Logger.Configure();
                Logger.Log(Config.GetHeader());

                // Hide the version (if needed)
                AppInfo.HideVersion = Config.HideVersion;

                await StartEonaCatDnsAsync().ConfigureAwait(false);

                if (_eonaCatDns.IsRunning)
                {
                    if (_eonaCatDns.IsFirstTime && Config.LoadDefaultBlockySetup)
                    {
                        var fileTask = DownloadBlockySetupAsync();
                        await Task.WhenAll(fileTask).ConfigureAwait(false);
                    }
                }

                Console.WriteLine(Config.GetHeader());
            }
            else
            {
                Logger.Log("Blocky Dns is already running", ELogType.DEBUG);
            }
        }

        private static async Task DownloadBlockySetupAsync(bool forceReload = false)
        {
            Logger.Log("Retrieving blockList for Blocky", ELogType.DEBUG);
            var setup = Config.GetBlockySetup(forceReload);

            if (setup.Any())
            {
                await _eonaCatDns.AddBlockedEntriesAsync(setup, Config.BlockyRedirectionserver).ConfigureAwait(false);
                Logger.Log("Retrieved blockList for Blocky", ELogType.DEBUG);
            }
        }

        private static Task StartEonaCatDnsAsync()
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
                LogInLocalTime = Config.LogInLocalTime,
            };

            DllInfo.ApplicationVersion = AppInfo.Version;
            DllInfo.ApplicationName = AppInfo.Name;
            DllInfo.HideVersion = AppInfo.HideVersion;

            _eonaCatDns = new EonaCatDns(config);
            _eonaCatDns.OnUpdateSetup -= EonaCatDns_OnUpdateSetup;
            _eonaCatDns.OnUpdateSetup += EonaCatDns_OnUpdateSetup;
            _eonaCatDns.UseExceptionHandling();
            return _eonaCatDns.StartAsync();
        }

        private static void EonaCatDns_OnUpdateSetup(object sender, EventArgs e)
        {
            DownloadBlockySetupAsync(true).ConfigureAwait(false);
        }

        private static void SendStatistics()
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
            slack.PostMessage(new Payload { Channel = "Blocky", Username = "Blocky", Text = stringBuilder.ToString() });
            Config.Loglevel = previous;
        }

        private static void Stop()
        {
            _eonaCatDns.Stop();
            Logger.Log($"Blocky {DllInfo.Version} has been stopped.");
            IsRunning = false;
        }
    }
}