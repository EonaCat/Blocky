using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Helpers;
using EonaCat.Helpers.Helpers;
using EonaCat.Logger;
using Microsoft.Extensions.Configuration;

namespace EonaCat.Blocky;
// Blocky
// Blocking domains the way you want it.
// Copyright EonaCat (Jeroen Saey) 2017-2023
// https://blocky.EonaCat.com

public static class Config
{
    public static IConfiguration Configuration = AppInfo.Configuration.GetSection("Blocky");

    public static string[] BlockyInteraceV4Ips = Configuration.GetValue("ListenV4", "127.0.0.1").Split(",");
    public static string ResolverAddressV4 = Configuration.GetValue("ResolveV4", "127.0.0.1");
    public static string[] BlockyInteraceV6Ips = Configuration.GetValue("LocalV6", "::1").Split(",");
    public static string ResolverAddressV6 = Configuration.GetValue("ResolveV6", "::1");
    public static bool IsV6Enabled = Configuration.GetValue("IsV6enabled", true);
    public static bool IsCacheDisabled = Configuration.GetValue("IsCacheDisabled", false);
    public static bool LogBlockedClients = Configuration.GetValue("LogBlockedClients", true);
    public static int StatsRefreshInterval = Configuration.GetValue("StatsRefreshIntervalInSeconds", 10);

    public static int DnsPort = Configuration.GetValue("DnsPort", 53);

    public static bool AutoUpdate = Configuration.GetValue("AutoUpdate", true);

    public static string WebserverInteraceIp = Configuration.GetValue("WebInterface", BlockyInteraceV4Ips[0]);
    public static bool HideVersion = Configuration.GetValue("HideVersion", false);

    public static string BlockyRedirectionserver = string.Empty;
    public static int WebserverPort = Configuration.GetValue("WebServerPort", 9999);
    public static ELogType Loglevel = Configuration.GetValue("LogLevel", ELogType.DEBUG);
    public static bool ResolveOverDoh = Configuration.GetValue("ResolveOverDoh", false);
    public static bool ContinueWhenDohFails = Configuration.GetValue("ContinueWhenDohFails", true);
    public static bool CreateMasterFileOnBoot = Configuration.GetValue("CreateMasterFileOnBoot", true);
    public static bool IgnoreWpadRequests = Configuration.GetValue("IgnoreWpadRequests", true);
    public static bool IgnoreArpaRequests = Configuration.GetValue("IgnoreArpaRequests", true);
    public static bool IncludeRawInLogTable = Configuration.GetValue("Raw", false);
    public static bool IsMultiCastEnabled = Configuration.GetValue("IsMultiCastEnabled", false);
    public static string RouterDomain = Configuration.GetValue("RouterDomain", string.Empty);
    public static bool PartialLookupName = Configuration.GetValue("PartialLookupName", false);
    public static bool ProgressToConsole = Configuration.GetValue("ProgressToConsole", false);
    public static bool LogInLocalTime = Configuration.GetValue("LogInLocalTime", false);

    /*Google "8.8.8.8, 8.8.4.4" */
    /*Comodo Secure Dns "8.26.56.26" */
    /*DNS.WATCH "84.200.70.40" */
    /*Level3 "209.244.0.4" */
    /*OpenDNS "208.67.222.222" */
    /*Verisign "64.6.65.6" */

    public static string[] DnsServers = Configuration.GetValue("DNSServers",
        new[]
        {
            "8.8.8.8",
            "8.8.4.4",
            "8.26.56.26",
            "84.200.70.40",
            "209.244.0.4",
            "208.67.222.222",
            "64.6.65.6"
        });

    public static string[] DontCacheList = Configuration.GetValue("DontCacheList",
        new[]
        {
            "www-www.bing.com.trafficmanager.net"
        });

    public static string[] DnsServersV6 = Configuration.GetValue("DNSServersV6",
        new[]
        {
            "fe80::e675:dcff:fea5:3ada%8"
        });

    public static string[] DoHServers = Configuration.GetValue("DoHServers",
        new[]
        {
            "https://dns.google/dns-query",
            "https://dns.quad9.net/dns-query",
            "https://cloudflare-dns.com/dns-query"
        });

    static Config()
    {
        if (!Directory.Exists(LogFolder))
        {
            Directory.CreateDirectory(LogFolder);
        }
    }

    public static string Year => DateTime.Now.Date.Year.ToString();

    public static string BlockyRulesUrl { get; internal set; } =
        @"https://git.saey.me/EonaCat/BlockyRules/raw/master/blockyrules";

    public static string BlockySetupSpec { get; internal set; } = "blocky.setup";

    public static string SlackUrl => "https://hooks.slack.com/services/SECRET_HOOK";

    public static string SeparatorLine => Environment.NewLine;

    public static bool LoadDefaultBlockySetup { get; set; } = Configuration.GetValue("LoadDefaultSetup", true);
    public static string Appfolder { get; internal set; } = AppDomain.CurrentDomain.BaseDirectory;
    public static string LogFolder { get; internal set; } = $"{Appfolder}logs";

    public static async Task<HashSet<BlockList>> GetBlockySetupAsync(bool forceReload = false)
    {
        if (forceReload || !File.Exists(BlockySetupSpec))
        {
            if (File.Exists(BlockySetupSpec))
            {
                File.Delete(BlockySetupSpec);
            }

            await Logger.LogAsync("Downloading blocky setup").ConfigureAwait(false);
            var downloadSetup = await DownloadBlockySetupAsync().ConfigureAwait(false);
            await Logger.LogAsync(downloadSetup
                ? "The blocky setup was downloaded successfully"
                : "The blocky setup could not be downloaded").ConfigureAwait(false);
        }
        else
        {
            await Logger.LogAsync("The blocky setup already exists").ConfigureAwait(false);
        }

        return await ParseBlockySetupAsync().ConfigureAwait(false);
    }

    private static async Task<HashSet<BlockList>> ParseBlockySetupAsync()
    {
        if (!File.Exists(BlockySetupSpec))
        {
            return new HashSet<BlockList>();
        }

        using (var reader = new StreamReader(BlockySetupSpec))
        {
            var result = new HashSet<BlockList>();
            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                var currentLine = line.StripComments();
                if (string.IsNullOrEmpty(currentLine))
                {
                    continue;
                }

                result.Add(new BlockList
                {
                    Url = currentLine,
                    Name = currentLine,
                    CreationDate = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                    LastUpdated = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                    IsEnabled = true,
                    TotalEntries = 0
                });
            }

            return result;
        }
    }

    private static async Task<bool> DownloadBlockySetupAsync()
    {
        var result = false;

        try
        {
            var content = WebHelper.DownloadUrlAsString(BlockyRulesUrl);
            if (!string.IsNullOrWhiteSpace(content))
            {
                File.AppendAllText(BlockySetupSpec, content);
                result = true;
            }
        }
        catch (Exception exception)
        {
            await Logger
                .LogAsync($"Exception occurred during blocky setup download : {exception.Message}", ELogType.ERROR)
                .ConfigureAwait(false);
        }

        return result;
    }

    public static string GetHeader()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append(SeparatorLine);
        stringBuilder.Append($"Blocky {AppInfo.Version}{SeparatorLine}");
        stringBuilder.Append($"Blocking domains the way you want it.{SeparatorLine}");
        stringBuilder.Append($"(c) EonaCat (Jeroen Saey) 2017-{Year}{SeparatorLine}");
        stringBuilder.Append($"https://blocky.EonaCat.com{SeparatorLine}");
        return stringBuilder.ToString();
    }
}