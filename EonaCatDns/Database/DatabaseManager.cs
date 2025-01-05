using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Dns.Core.Helpers;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Managers.Stats;
using EonaCat.Logger;
using SQLite;
using BlockList = EonaCat.Dns.Database.Models.Entities.BlockList;

namespace EonaCat.Dns.Database
{
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

        private static readonly ConcurrentQueue<Task> _taskQueue = new();
        private static readonly SemaphoreSlim _taskSemaphore = new(10); // Limit concurrent tasks

        public static long TotalClients { get; private set; }
        public static long TotalBlocked { get; private set; }
        public static long TotalCached { get; private set; }
        public static long TotalQueries { get; private set; }

        public static long TotalNoError { get; set; }

        public static long TotalServerFailure { get; set; }

        public static long TotalRefused { get; set; }

        static DatabaseManager()
        {
            Database = CreateDatabaseConnection();
            DatabaseDomain = CreateDomainDatabaseConnection();
            DatabaseLogs = CreateLogsDatabaseConnection();
        }

        internal static event EventHandler<string> OnUpdateBlockList;
        internal static event EventHandler<Client> OnHostNameResolve;

        private static SQLiteAsyncConnection CreateDatabaseConnection() => new SQLiteAsyncConnection(DatabasePath);
        private static SQLiteAsyncConnection CreateDomainDatabaseConnection() => new SQLiteAsyncConnection(DomainDatabasePath);
        private static SQLiteAsyncConnection CreateLogsDatabaseConnection() => new SQLiteAsyncConnection(LogsDatabasePath);

        internal static async Task CreateTablesAndIndexesAsync()
        {
            await Task.WhenAll(
                Database.CreateTableAsync<BlockList>(),
                Database.CreateTableAsync<Setting>(),
                Database.CreateTableAsync<Client>(),
                DatabaseLogs.CreateTableAsync<Log>(),
                DatabaseDomain.CreateTableAsync<Domain>(),
                Database.CreateTableAsync<Category>(),
                Database.CreateTableAsync<User>()
            ).ConfigureAwait(false);

            await Task.WhenAll(
                Database.CreateIndexAsync<Category>(x => x.Name),
                Database.CreateIndexAsync<Client>(x => x.Name),
                Database.CreateIndexAsync<Client>(x => x.Ip),
                DatabaseDomain.CreateIndexAsync<Domain>(x => x.Url),
                DatabaseDomain.CreateIndexAsync<Domain>(x => x.ListType),
                DatabaseLogs.CreateIndexAsync<Log>(x => x.ClientIp),
                DatabaseLogs.CreateIndexAsync<Log>(x => x.DateTime),
                DatabaseLogs.CreateIndexAsync<Log>(x => x.Raw)
            ).ConfigureAwait(false);
        }

        internal static async Task<int> GetAllowedDomainsCountAsync() => await Domains.CountAllAsync(x => x.ListType == ListType.Allowed).ConfigureAwait(false);

        internal static async Task<HashSet<string>> GetAllowedDomainsAsync()
        {
            var allowedDomains = await Domains.Where(x => x.ListType == ListType.Allowed).ToArrayAsync().ConfigureAwait(false);
            return allowedDomains.Select(x => x.Url).ToHashSet();
        }

        internal static async Task<int> GetBlockedDomainsCountAsync() => await Domains.CountAllAsync(x => x.ListType == ListType.Blocked).ConfigureAwait(false);

        internal static async Task<HashSet<string>> GetBlockedDomainsAsync()
        {
            var blockedDomains = await Domains.Where(x => x.ListType == ListType.Blocked).ToArrayAsync().ConfigureAwait(false);
            return blockedDomains.Select(x => x.Url).ToHashSet();
        }

        internal static Task<int> SettingsCountAsync() => Settings.CountAllAsync();

        internal static async Task<Setting> GetSettingAsync(string name)
        {
            name = name.ToUpper();
            try
            {
                return await Settings.FirstOrDefaultAsync(x => x.Name == name).ConfigureAwait(false) ?? new Setting { Name = name };
            }
            catch (Exception exception)
            {
                await Logger.LogAsync(exception.ToString(), ELogType.ERROR).ConfigureAwait(false);
                return null;
            }
        }

        internal static async Task<User> UpdateUserAsync(User user)
        {
            return user != null ? await Users.InsertOrUpdateAsync(user).ConfigureAwait(false) : null;
        }

        internal static async Task<StatsOverview> GetStatsOverviewAsync()
        {
            TotalBlocked = await Logs.CountAllAsync(x => x.IsBlocked).ConfigureAwait(false);
            TotalCached = await Logs.CountAllAsync(x => x.IsFromCache).ConfigureAwait(false);
            TotalRefused = await Logs.CountAllAsync(x => x.ResponseCode == ResponseCode.Refused).ConfigureAwait(false);
            TotalServerFailure = await Logs.CountAllAsync(x => x.ResponseCode == ResponseCode.ServerFailure).ConfigureAwait(false);
            TotalNoError = await Logs.CountAllAsync(x => x.ResponseCode == ResponseCode.NoError).ConfigureAwait(false);
            TotalClients = await Logs.CountAsync("Log", "ClientIp", true).ConfigureAwait(false);
            TotalQueries = await Logs.CountAllAsync().ConfigureAwait(false);

            return new StatsOverview(TotalBlocked, TotalCached, TotalQueries, TotalClients, TotalRefused, TotalServerFailure, TotalNoError);
        }

        internal static Task<List<User>> GetUsersAsync() => Users.GetAll().ToListAsync();

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
            return await Clients.InsertOrUpdateAsync(client).ConfigureAwait(false);
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
                if (string.IsNullOrWhiteSpace(client.Name))
                {
                    if (MultiCastDnsHelper.IsMultiCastEnabled)
                    {
                        client.Name = MultiCastDnsHelper.Hosts.FirstOrDefault(x => x.Key == ipAddress.ToString()).Value;
                    }
                }

                if (string.IsNullOrWhiteSpace(client.Name))
                {
                    client.Name = ipAddress.ToString();
                }

                await Clients.InsertOrUpdateAsync(client).ConfigureAwait(false);
                OnHostNameResolve?.Invoke(null, client);
            }
            catch (Exception)
            {
                // Handle the error without crashing
            }
        }

        internal static Task<User> AddUserAsync(User user) => Users.InsertOrUpdateAsync(user);

        internal static Task<Setting> SetSettingAsync(Setting setting)
        {
            setting.Name = setting?.Name.ToUpper();
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
}
