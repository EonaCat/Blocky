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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Helpers;

namespace EonaCat.Dns.Managers.Stats
{
    internal class StatsCollection
    {
        public StatsType StatsType { get; set; }
        public StatsTotals StatsTotal { get; set; }
        public List<StatsLog> Queries { get; set; }
        public List<StatsLog> Success { get; set; }
        public List<StatsLog> ServerError { get; set; }
        public List<StatsLog> NameError { get; set; }
        public List<StatsLog> Refused { get; set; }
        public List<StatsLog> Blocked { get; set; }
        public List<StatsLog> Cached { get; set; }
        public List<StatsLog> Clients { get; set; }
        public string LabelFormat => GetLabelFormat();

        public async ValueTask<List<StatsLog>> GetTopRecordTypesAsync()
        {
            var queryTypes = await DatabaseManager.Logs.GetTopRecordTypesAsync(DateTime.UtcNow.AddHours(-StatsManager.GetTotalHours(StatsType)).ToUnixTime()).ConfigureAwait(false);
            return GetTopList(queryTypes.Select(x => new StatsLog { Name = Enum.GetName(x.RecordType), Value = x.Total }));
        } 

        public async ValueTask<List<StatsLog>> GetTopClientsAsync()
        {
            var clients = await DatabaseManager.Logs.GetTopClientsAsync(DateTime.UtcNow.AddHours(-StatsManager.GetTotalHours(StatsType)).ToUnixTime()).ConfigureAwait(false);
            return GetTopList(clients.Select(x => new StatsLog { Name = x.Client, Value = x.Total }));
        }

        public async ValueTask<List<StatsLog>> GetTopDomainsAsync()
        {
            var domains = await DatabaseManager.Logs.GetTopDomainsAsync(DateTime.UtcNow.AddHours(-StatsManager.GetTotalHours(StatsType)).ToUnixTime()).ConfigureAwait(false);
            return GetTopList(domains.Select(x => new StatsLog { Name = x.Domain, Value = x.Total }));
        }

        public async ValueTask<List<StatsLog>> GetLastQueriesAsync()
        {
            var domains = await DatabaseManager.Logs.GetLastQueriesAsync(DateTime.UtcNow.AddHours(-StatsManager.GetTotalHours(StatsType)).ToUnixTime()).ConfigureAwait(false);
            return GetTopList(domains.Select(x => new StatsLog { Name = x.Domain, Value = x.Total }));
        }

        public async ValueTask<List<StatsLog>> GetTopBlockedDomainsAsync()
        {
            var domains = await DatabaseManager.Logs.GetTopDomainsAsync(DateTime.UtcNow.AddHours(-StatsManager.GetTotalHours(StatsType)).ToUnixTime(), true).ConfigureAwait(false);
            return GetTopList(domains.Select(x => new StatsLog { Name = x.Domain, Value = x.Total }));
        }

        private static List<StatsLog> GetTopList(IEnumerable<StatsLog> list)
        {
            var topList = list.OrderByDescending(item => item.Value).Take(ConstantsDns.Stats.Top).ToList();
            return topList;
        }

        private string GetLabelFormat()
        {
            return StatsType switch
            {
                StatsType.LastMonth => ConstantsDns.DateTimeFormats.DateTimeMonthStats + ":00",
                StatsType.LastDay => ConstantsDns.DateTimeFormats.DateTimeDayStats + ":00",
                StatsType.LastHour => ConstantsDns.DateTimeFormats.DateTimeHourStats,
                StatsType.LastWeek => ConstantsDns.DateTimeFormats.DateTimeWeekStats + ":00",
                StatsType.LastYear => ConstantsDns.DateTimeFormats.DateTimeYearStats,
                _ => string.Empty,
            };
        }

        public List<StatsLog> GetStatsTotalHashSet()
        {
            return new List<StatsLog>
            {
                new() { Name = ConstantsDns.Stats.TotalQueries, Value = Queries.Sum(x => x.Value)},
                new() { Name = ConstantsDns.Stats.TotalNoError, Value = Success.Sum(x => x.Value)},
                new() { Name = ConstantsDns.Stats.TotalServerFailure, Value = ServerError.Sum(x => x.Value)},
                new() { Name = ConstantsDns.Stats.TotalNameError, Value = NameError.Sum(x => x.Value)},
                new() { Name = ConstantsDns.Stats.TotalRefused, Value = Refused.Sum(x => x.Value)},
                new() { Name = ConstantsDns.Stats.TotalBlocked, Value = Blocked.Sum(x => x.Value)},
                new() { Name = ConstantsDns.Stats.TotalCached, Value = Cached.Sum(x => x.Value)},
                new() { Name = ConstantsDns.Stats.TotalClients, Value = Clients.Sum(x => x.Value)},
            };
        }
    }
}
