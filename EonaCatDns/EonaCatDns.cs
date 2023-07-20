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
using EonaCat.Dns.Managers;
using EonaCat.Dns.Managers.Stats;
using EonaCat.Helpers.Helpers;
using EonaCat.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using EonaCat.Dns.Models;
using EonaCat.Dns.Exceptions;
using EonaCat.Logger.Extensions;

namespace EonaCat.Dns;

public class EonaCatDns : IDisposable
{
    private Blocker _blocker;

    private EonaCatDnsConfig _config;

    public event EventHandler OnUpdateSetup;

    /*Comodo Secure Dns "8.26.56.26" */
    /*DNS.WATCH "84.200.70.40" */
    /*Google "8.8.8.8, 8.8.4.4" */
    /*Level3 "209.244.0.4" */
    /*OpenDNS "208.67.222.222" */
    /*Verisign "64.6.65.6" */

    public string[] DefaultResolvers =
    {
        "8.8.8.8",
        "8.8.4.4",
        "8.26.56.26",
        "84.200.70.40",
        "209.244.0.4",
        "208.67.222.222",
        "64.6.65.6"
    };

    public EonaCatDns(EonaCatDnsConfig config)
    {
        SetConfig(config);
    }

    public EonaCatDns(string ipAddress, int port = 53, string applicationName = null, string applicationVersion = null,
        string webInterface = "127.0.0.1", int webServicePort = 9999, bool enableAdminInterface = true,
        string[] resolverEndpoints = null)
    {
        var config = new EonaCatDnsConfig();
        Config.ListenAddressV4 = ipAddress;
        Config.Port = port;
        Config.ApplicationName = applicationName;
        Config.WebServerIpAddress = webInterface;
        Config.WebServerPort = webServicePort;
        Config.EnableAdminInterface = enableAdminInterface;
        Config.Resolvers = resolverEndpoints;
        config.ApplicationVersion = applicationVersion;
        SetConfig(config);
    }

    public EonaCatDns UseExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        AppDomain.CurrentDomain.FirstChanceException -= CurrentDomain_FirstChanceException;
        AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
        return this;
    }

    private static void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
    {
        Debug.WriteLine(e.Exception.FormatExceptionToMessage(), ELogType.ERROR, false);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Logger.Log(e.ExceptionObject.ToString(), ELogType.ERROR, false);
    }

    private void SetConfig(EonaCatDnsConfig config)
    {
        Config = config;
        CreateConfigurationFolders();
        SetStatsInterval();
    }

    private void SetStatsInterval()
    {
        ConstantsDns.Stats.RefreshInterval = _config.StatsRefreshInterval;
    }

    public bool IsFirstTime { get; private set; }
    public bool IsAdminEnabled { get; private set; }
    public bool IsRunning { get; private set; }
    internal DnsServer InternalDnsServer { get; private set; }

    public static int WebServicePort { get; private set; }

    private EonaCatDnsWebServer EonaCatDnsWebServer { get; set; }
    internal static Managers.Managers Managers { get; set; }

    public bool IsDisposed { get; private set; }

    public EonaCatDnsConfig Config
    {
        get => _config;

        set
        {
            if (IsRunning)
            {
                throw new Exception("EonaCatDns: " + "EonaCatDns: Cannot change configuration, please stop the EonaCatDns Server first");
            }

            _config = ParseConfig(value);
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }

    public Task<StatsOverview> GetStatsOverviewAsync()
    {
        return DatabaseManager.GetStatsOverviewAsync();
    }

    private EonaCatDnsConfig ParseConfig(EonaCatDnsConfig value)
    {
        if (value == null)
        {
            throw new ArgumentNullException("EonaCatDns: " + "EonaCatDns: The configuration could not be parsed, the config is NULL");
        }

        if (string.IsNullOrEmpty(value.ApplicationName))
        {
            value.ApplicationName = DllInfo.Name;
        }

        if (string.IsNullOrEmpty(value.ApplicationVersion))
        {
            value.ApplicationVersion = DllInfo.Version;
        }

        IsAdminEnabled = value.EnableAdminInterface;
        WebServicePort = value.WebServerPort;

        EonaCatDnsWebServer = new EonaCatDnsWebServer(value.LogInLocalTime, value.WebServerIpAddress, WebServicePort);
        value.ListenAddressV4.GetHostIpAddress(out _);

        var resolvers = new List<string>();
        if (value.Resolvers != null && value.Resolvers.Any())
        {
            foreach (var endpoint in value.Resolvers)
            {
                if (!WebHelper.IsIpValid(endpoint))
                {
                    Logger.Log($"Invalid ipAddress '{endpoint}' detected, not using as a resolver");
                }
                else
                {
                    resolvers.Add(endpoint);
                }
            }
        }

        if (!resolvers.Any())
        {
            Logger.Log("No valid resolvers found, using defaults");
            resolvers = DefaultResolvers.ToList();
        }

        value.Resolvers = resolvers.ToArray();
        return value;
    }

    private static async Task CreateManagersAsync()
    {
        await DatabaseManager.CreateTablesAndIndexesAsync().ConfigureAwait(false);
        var statsManager = new StatsManager();
        var sessionManager = new SessionManager();
        var userManager = new UserManager();
        var apiStatsManager = new StatsManagerApi(statsManager);
        Managers = new Managers.Managers(statsManager, sessionManager, userManager, apiStatsManager);
    }

    private async Task InitialiseAsync()
    {
        await CreateManagersAsync().ConfigureAwait(false);
        await LoadSettingsAsync().ConfigureAwait(false);
        await LoadBlockerSettingsAsync().ConfigureAwait(false);
        await StatsManagerApi.LoadStatsColorsAsync().ConfigureAwait(false);

        if (IsAdminEnabled)
        {
            EonaCatDnsWebServer.Start();
        }

        DatabaseManager.OnUpdateBlockList -= DatabaseManager_OnUpdateBlockList;
        DatabaseManager.OnUpdateBlockList += DatabaseManager_OnUpdateBlockList;
    }

    private Task LoadBlockerSettingsAsync()
    {
        Blocker.OnUpdateSetup += Blocker_OnUpdateSetup;
        _blocker.ProgressToConsole = Config.ProgressToConsole;
        return Task.FromResult(true);
    }

    private void Blocker_OnUpdateSetup(object sender, EventArgs e)
    {
        OnUpdateSetup?.Invoke(sender, e);
    }

    private void DatabaseManager_OnUpdateBlockList(object sender, string e)
    {
        var blockListItem = new BlockListItem
        {
            Entries = new HashSet<Uri>() { new(e) },
            RedirectionAddress = ConstantsDns.DefaultRedirectionAddress
        };
        _blocker?.UpdateBlockedEntriesAsync(new List<BlockListItem> { blockListItem });
    }

    public async Task StartAsync()
    {
        if (!IsRunning)
        {
            Logger.MaxLogType = _config.LogLevel;
            Logger.UseLocalTime = _config.LogInLocalTime;
            Logger.Configure();

            Logger.Log($"Starting Dns for {_config.ApplicationName}");

            IsRunning = true;
            _blocker = new Blocker();
            InternalDnsServer = new DnsServer(Config);

            // Initialise the application
            await InitialiseAsync().ConfigureAwait(false);

            await InternalDnsServer.StartAsync().ConfigureAwait(false);

            if (InternalDnsServer.IsRunning)
            {
                // Initialise the blocker
                await _blocker.InitialiseAsync(_config.AutoUpdate).ConfigureAwait(false);
            }
            else
            {
                IsRunning = false;
            }
        }
    }

    public void Stop()
    {
        EonaCatDnsWebServer.Stop();
        IsRunning = false;
        Logger.Log($"{DllInfo.ApplicationName} stopped");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (disposing)
        {
            Stop();
            _blocker.Dispose();
        }

        IsDisposed = true;
    }

    private static void CreateConfigurationFolders()
    {
        DllInfo.LogFolder = Path.Combine(DllInfo.ConfigFolder, "logs");
        if (!Directory.Exists(DllInfo.LogFolder))
        {
            Directory.CreateDirectory(DllInfo.LogFolder);
        }
    }

    private Task UpdateBlockedEntriesAsync(HashSet<Uri> blockedEntries, string address = null)
    {
        return _blocker.UpdateBlockedEntriesAsync(new List<BlockListItem> { new BlockListItem { RedirectionAddress = address, Entries = blockedEntries } });
    }

    private async Task LoadSettingsAsync()
    {
        var settingsAmount = 0;

        try
        {
            settingsAmount = await DatabaseManager.SettingsCountAsync().ConfigureAwait(false);
            if (settingsAmount == 0)
            {
                throw new DatabaseException("EonaCatDns: " + "EonaCatDns: Settings not found");
            }

            var setting = await DatabaseManager.GetSettingAsync(SettingName.WebservicePort).ConfigureAwait(false);
            if (setting != null)
            {
                WebServicePort = Convert.ToInt32(setting.Value);
            }

            var users = await DatabaseManager.GetUsersAsync().ConfigureAwait(false);
            users.ForEach(x => UserManager.Instance.LoadCredentials(x));
        }
        catch (DatabaseException databaseException)
        {
            Logger.Log(databaseException.Message);

            IsFirstTime = true;
            await _blocker.WriteDefaultAllowListAsync().ConfigureAwait(false);

            if (settingsAmount == 0)
            {
                await CreateDefaultSettingsAsync().ConfigureAwait(false);
                Logger.Log("Default settings loaded");
            }
        }
    }

    private static async Task CreateDefaultSettingsAsync()
    {
        var user = UserManager.Instance.ClearAndSetDefaultUser();
        await DatabaseManager.AddUserAsync(user).ConfigureAwait(false);

        WebServicePort = 9999;
        await DatabaseManager.SetSettingAsync(new Setting
        { Name = SettingName.WebservicePort, Value = WebServicePort.ToString() }).ConfigureAwait(false);
    }

    internal async Task SaveSettingsAsync()
    {
        var setting = await DatabaseManager.GetSettingAsync(SettingName.WebservicePort).ConfigureAwait(false);
        setting.Value = WebServicePort.ToString();
        await DatabaseManager.SetSettingAsync(setting).ConfigureAwait(false);
        UserManager.Instance.Users.Values.ToList().ForEach(AddUserAsync);

        if (Blocker.Setup != null)
        {
            var blockedAddressSetting = await DatabaseManager.GetSettingAsync(SettingName.Blockedaddress).ConfigureAwait(false);
            blockedAddressSetting.Value = Blocker.Setup.RedirectionAddress;
            await DatabaseManager.SetSettingAsync(blockedAddressSetting).ConfigureAwait(false);
        }
    }

    private static void AddUserAsync(User x)
    {
        DatabaseManager.AddUserAsync(x);
    }

    public async Task AddBlockedEntriesAsync(HashSet<Database.Models.Entities.BlockList> blockedList,
        string address = null)
    {
        if (string.IsNullOrEmpty(address))
        {
            address = ConstantsDns.DefaultRedirectionAddress;
        }

        Blocker.Setup = new BlockedSetup
        {
            Urls = blockedList,
            RedirectionAddress = address
        };

        await SaveSettingsAsync().ConfigureAwait(false);

        var blockedEntries = new List<Uri>();
        blockedEntries.AddRange(blockedList.Where(x => x.IsEnabled).Select(x => new Uri(x.Url)));
        await UpdateBlockedEntriesAsync(blockedEntries.ToHashSet(), address).ConfigureAwait(false);
    }
}