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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Dns.Core.Helpers;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Managers.Stats;
using EonaCat.Logger;
using SQLite;
using BlockList = EonaCat.Dns.Database.Models.Entities.BlockList;

namespace EonaCat.Dns.Database;

internal static class DatabaseManager
{
    private static readonly string DatabasePath = ConstantsDns.Database.Databasepath;
    private static readonly string LogsDatabasePath = ConstantsDns.Database.LogsDatabasepath;
    private static readonly string DomainDatabasePath = ConstantsDns.Database.DomainDatabasepath;


    private static readonly SQLiteAsyncConnection Database = new(DatabasePath);
    private static readonly SQLiteAsyncConnection DatabaseDomain = new(DomainDatabasePath);
    private static readonly SQLiteAsyncConnection DatabaseLogs = new(LogsDatabasePath);

    internal static readonly SqliteRepository<Domain> Domains = new(DatabaseDomain);
    internal static readonly SqliteRepository<Setting> Settings = new(Database);
    internal static readonly SqliteRepository<BlockList> BlockLists = new(Database);
    internal static readonly SqliteRepository<Client> Clients = new(Database);
    internal static readonly SqliteRepository<Log> Logs = new(DatabaseLogs);
    internal static readonly SqliteRepository<Category> Categories = new(Database);
    internal static readonly SqliteRepository<User> Users = new(Database);

    static DatabaseManager()
    {
        Database = CreateDatabaseConnection();
        DatabaseDomain = CreateDomainDatabaseConnection();
        DatabaseLogs = CreateLogsDatabaseConnection();
    }

    public static long TotalClients { get; private set; }
    public static long TotalBlocked { get; private set; }
    public static long TotalCached { get; private set; }
    public static long TotalQueries { get; private set; }

    public static long TotalNoError { get; set; }

    public static long TotalServerFailure { get; set; }

    public static long TotalRefused { get; set; }

    internal static event EventHandler<string> OnUpdateBlockList;
    internal static event EventHandler<Client> OnHostNameResolve;

    private static SQLiteAsyncConnection CreateDatabaseConnection()
    {
        return new SQLiteAsyncConnection(DatabasePath);
    }

    private static SQLiteAsyncConnection CreateDomainDatabaseConnection()
    {
        return new SQLiteAsyncConnection(DomainDatabasePath);
    }

    private static SQLiteAsyncConnection CreateLogsDatabaseConnection()
    {
        return new SQLiteAsyncConnection(LogsDatabasePath);
    }

    internal static async Task CreateTablesAndIndexesAsync()
    {
        await Database.CreateTableAsync<BlockList>().ConfigureAwait(false);
        await Database.CreateTableAsync<Setting>().ConfigureAwait(false);
        await Database.CreateTableAsync<Client>().ConfigureAwait(false);
        await DatabaseLogs.CreateTableAsync<Log>().ConfigureAwait(false);
        await DatabaseDomain.CreateTableAsync<Domain>().ConfigureAwait(false);
        await Database.CreateTableAsync<Category>().ConfigureAwait(false);
        await Database.CreateTableAsync<User>().ConfigureAwait(false);

        await Database.CreateIndexAsync<Category>(x => x.Name).ConfigureAwait(false);
        await Database.CreateIndexAsync<Client>(x => x.Name).ConfigureAwait(false);
        await Database.CreateIndexAsync<Client>(x => x.Ip).ConfigureAwait(false);
        await DatabaseDomain.CreateIndexAsync<Domain>(x => x.Url).ConfigureAwait(false);
        await DatabaseDomain.CreateIndexAsync<Domain>(x => x.ListType).ConfigureAwait(false);
        await DatabaseLogs.CreateIndexAsync<Log>(x => x.ClientIp).ConfigureAwait(false);
        await DatabaseLogs.CreateIndexAsync<Log>(x => x.DateTime).ConfigureAwait(false);
        await DatabaseLogs.CreateIndexAsync<Log>(x => x.Raw).ConfigureAwait(false);
    }

    internal static Task<int> GetAllowedDomainsCountAsync()
    {
        return Domains.CountAllAsync(x => x.ListType == ListType.Allowed);
    }

    internal static async Task<HashSet<string>> GetAllowedDomainsAsync()
    {
        return (await Domains.Where(x => x.ListType == ListType.Allowed).ToArrayAsync().ConfigureAwait(false))
            .Select(x => x.Url).ToHashSet();
    }

    internal static Task<int> GetBlockedDomainsCountAsync()
    {
        return Domains.CountAllAsync(x => x.ListType == ListType.Blocked);
    }

    internal static async Task<HashSet<string>> GetBlockedDomainsAsync()
    {
        return (await Domains.Where(x => x.ListType == ListType.Blocked).ToArrayAsync().ConfigureAwait(false))
            .Select(x => x.Url).ToHashSet();
    }

    internal static Task<int> SettingsCountAsync()
    {
        return Settings.CountAllAsync();
    }

    internal static async Task<Setting> GetSettingAsync(string name)
    {
        Setting result = null;

        try
        {
            name = name.ToUpper();
            result = await Settings.FirstOrDefaultAsync(x => x.Name == name).ConfigureAwait(false) ??
                     new Setting { Name = name };
        }
        catch (Exception exception)
        {
            await Logger.LogAsync(exception.ToString(), ELogType.ERROR).ConfigureAwait(false);
        }

        return result;
    }

    internal static async Task<User> UpdateUserAsync(User user)
    {
        if (user == null)
        {
            return null;
        }

        return await Users.InsertOrUpdateAsync(user).ConfigureAwait(false);
    }

    internal static async Task<StatsOverview> GetStatsOverviewAsync()
    {
        TotalBlocked = await Logs.CountAllAsync(x => x.IsBlocked).ConfigureAwait(false);
        TotalCached = await Logs.CountAllAsync(x => x.IsFromCache).ConfigureAwait(false);
        TotalRefused = await Logs.CountAllAsync(x => x.ResponseCode == ResponseCode.Refused).ConfigureAwait(false);
        TotalServerFailure = await Logs.CountAllAsync(x => x.ResponseCode == ResponseCode.ServerFailure)
            .ConfigureAwait(false);
        TotalNoError = await Logs.CountAllAsync(x => x.ResponseCode == ResponseCode.NoError).ConfigureAwait(false);
        TotalClients = await Logs.CountAsync("Log", "ClientIp", true).ConfigureAwait(false);
        TotalQueries = await Logs.CountAllAsync().ConfigureAwait(false);
        return new StatsOverview(TotalBlocked, TotalCached, TotalQueries, TotalClients, TotalRefused,
            TotalServerFailure, TotalNoError);
    }

    internal static Task<List<User>> GetUsersAsync()
    {
        return Users.GetAll().ToListAsync();
    }

    internal static async Task<Client> GetOrAddClientAsync(IPAddress ipAddress)
    {
        var ipAsString = ipAddress.ToString();
        var client = await Clients.FirstOrDefaultAsync(x => x.Ip == ipAsString).ConfigureAwait(false);
        if (client != null)
        {
            await Task.Run(() => CheckIfClientHasNameAsync(client, ipAddress)).ConfigureAwait(false);
            return client;
        }

        client = new Client { Ip = ipAsString };
        client = await Clients.InsertOrUpdateAsync(client).ConfigureAwait(false);
        return client;
    }

    private static async Task CheckIfClientHasNameAsync(Client client, IPAddress ipAddress)
    {
        if (!string.IsNullOrWhiteSpace(client.Name) && !client.Name.Equals(ipAddress.ToString()))
        {
            OnHostNameResolve?.Invoke(null, client);
            return;
        }

        try
        {
            //TODO: How can we make PING work?  Why do we need to add the .
            //client.Name = WebHelper.GetMachineNameFromIpAddress(ipAddress);
            if (string.IsNullOrWhiteSpace(client.Name))
            {
                if (MultiCastDnsHelper.IsMultiCastEnabled)
                {
                    client.Name = MultiCastDnsHelper.Hosts.FirstOrDefault(x => x.Key == ipAddress.ToString()).Value;
                }
            }

            if (string.IsNullOrWhiteSpace(client.Name))
            {
                // Could not resolve the ipAddress via multiCast or DnsEntry
                client.Name = ipAddress.ToString();
                await Clients.InsertOrUpdateAsync(client).ConfigureAwait(false);
                return;
            }

            OnHostNameResolve?.Invoke(null, client);

            await Clients.InsertOrUpdateAsync(client).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Do nothing
        }
    }

    internal static Task<User> AddUserAsync(User user)
    {
        return Users.InsertOrUpdateAsync(user);
    }

    internal static Task<Setting> SetSettingAsync(Setting setting)
    {
        if (setting != null)
        {
            setting.Name = setting.Name.ToUpper();
        }

        return Settings.InsertOrUpdateAsync(setting);
    }

    internal static async Task<Log> ReadStatsAsync(long dateTimeTicks)
    {
        try
        {
            return await Logs.FirstOrDefaultAsync(x => x.DateTime == dateTimeTicks).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await Logger.LogAsync(exception.ToString(), ELogType.ERROR).ConfigureAwait(false);
        }

        return null;
    }

    public static void UpdateBlockList(string blockListUrl)
    {
        OnUpdateBlockList?.Invoke(null, blockListUrl);
    }
}