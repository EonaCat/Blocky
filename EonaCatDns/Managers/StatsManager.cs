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
using EonaCat.Dns.Managers.Stats;
using EonaCat.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EonaCat.Dns.Database.Models.Entities;

namespace EonaCat.Dns.Managers
{
    internal class StatsManager
    {
        private static readonly int HoursInDayStats = StatsPerHour.HoursInDay;
        private static readonly int HoursInWeekStats = HoursInDayStats * StatsPerHour.DaysInWeek;
        private static readonly int HoursInMonthStats = HoursInDayStats * StatsPerHour.DaysInMonth;
        private static readonly int HoursInYearStats = HoursInDayStats * StatsPerHour.DaysInYear;

        public async Task<IDictionary<string, List<StatsLog>>> GetStatsDataAsync(
            StatsType statsType, bool forceNew = false, bool isAuthenticated = false)
        {
            var cache = StatsCache.GetCacheAsync(statsType.ToString());
            if (!forceNew && cache != null)
            {
                return cache;
            }

            if (IsStatsRunning)
            {
                return cache;
            }

            if (!isAuthenticated && (statsType == StatsType.LastMonth || statsType == StatsType.LastYear))
            {
                statsType = StatsType.LastHour;
            }

            IsStatsRunning = true;
            var statsTotal = new StatsTotals(DateTime.Now);
            var statsCollection = new StatsCollection
            {
                StatsType = statsType,
                StatsTotal = statsTotal,
                Queries = new List<StatsLog>(),
                Success = new List<StatsLog>(),
                ServerError = new List<StatsLog>(),
                NameError = new List<StatsLog>(),
                Refused = new List<StatsLog>(),
                Blocked = new List<StatsLog>(),
                Cached = new List<StatsLog>(),
                Clients = new List<StatsLog>()
            };

            await UpdateStatsAsync(statsCollection).ConfigureAwait(false);
            var statsDictionary = await GenerateStatsDictionaryAsync(statsCollection, isAuthenticated).ConfigureAwait(false);
            StatsCache.SetCache($"{statsType}", new Dictionary<string, List<StatsLog>>(statsDictionary), TimeSpan.FromMinutes(30));

            IsStatsRunning = false;
            return statsDictionary;
        }

        private static async Task<IDictionary<string, List<StatsLog>>> GenerateStatsDictionaryAsync(StatsCollection statsCollection, bool isAuthenticated = false)
        {
            var data = new Dictionary<string, List<StatsLog>>
            {
                { ConstantsDns.Stats.TotalStats,  statsCollection.GetStatsTotalHashSet() },
                { ConstantsDns.Stats.TotalQueries, statsCollection.Queries},
                { ConstantsDns.Stats.TotalNoError, statsCollection.Success},
                { ConstantsDns.Stats.TotalServerFailure, statsCollection.ServerError},
                { ConstantsDns.Stats.TotalNameError, statsCollection.NameError},
                { ConstantsDns.Stats.TotalRefused, statsCollection.Refused},
                { ConstantsDns.Stats.TotalBlocked, statsCollection.Blocked},
                { ConstantsDns.Stats.TotalCached, statsCollection.Cached},
                { ConstantsDns.Stats.TotalClients, statsCollection.Clients},
            };

            if (!isAuthenticated)
            {
                return data;
            }

            data.Add(ConstantsDns.Stats.TopDomains, await statsCollection.GetTopDomainsAsync().ConfigureAwait(false));
            data.Add(ConstantsDns.Stats.LastQueries, await statsCollection.GetLastQueriesAsync().ConfigureAwait(false));
            data.Add(ConstantsDns.Stats.TopBlockedDomains, await statsCollection.GetTopBlockedDomainsAsync().ConfigureAwait(false));
            data.Add(ConstantsDns.Stats.TopClients, await statsCollection.GetTopClientsAsync().ConfigureAwait(false));
            data.Add(ConstantsDns.Stats.QueryTypes, await statsCollection.GetTopRecordTypesAsync().ConfigureAwait(false));

            return data;
        }

        public bool IsStatsRunning { get; set; }

        private static async Task UpdateStatsAsync(StatsCollection statsCollection)
        {
            var hours = GetTotalHours(statsCollection.StatsType);
            var endDateTime = DateTime.Now;
            var startDateTime = endDateTime.AddHours(-hours);

            for (var i = 0; i < hours; i++)
            {
                var hourStats = await LoadStatsForHourAsync(startDateTime.AddHours(i), startDateTime.AddHours(i + 1), statsCollection).ConfigureAwait(false);

                MergeStats(statsCollection.StatsTotal, hourStats.StatsByHour);
                AddStatsForDateTimeLabel(statsCollection, hourStats.StatsByHour, hourStats.StatsByHour.DateTime.ToString(statsCollection.LabelFormat));
            }
        }

        private static void MergeStats(StatsTotals statsTotal, StatsTotals hourStats)
        {
            statsTotal.TotalBlocked += hourStats.TotalBlocked;
            statsTotal.TotalCached += hourStats.TotalCached;
            statsTotal.TotalClients += hourStats.TotalClients;
            statsTotal.TotalNameError += hourStats.TotalNameError;
            statsTotal.TotalNoError += hourStats.TotalNoError;
            statsTotal.TotalQueries += hourStats.TotalQueries;
            statsTotal.TotalRefused += hourStats.TotalRefused;
            statsTotal.TotalServerFailure += hourStats.TotalServerFailure;
        }

        internal static int GetTotalHours(StatsType statsType)
        {
            return statsType switch
            {
                StatsType.LastHour => 1,
                StatsType.LastDay => HoursInDayStats,
                StatsType.LastWeek => HoursInWeekStats,
                StatsType.LastMonth => HoursInMonthStats,
                StatsType.LastYear => HoursInYearStats,
                _ => 1
            };
        }

        private static async Task<StatsPerHour> LoadStatsForHourAsync(DateTime startDateTime, DateTime endDateTime, StatsCollection statsCollection)
        {
            var hourStats = new StatsPerHour(startDateTime);
            var hourStatistics = await GetHourStatisticsAsync(startDateTime, endDateTime).ConfigureAwait(false);
            var statsTotals = CalculateStatsTotalsPerMinute(startDateTime, hourStatistics);

            if (!hourStatistics.Any()) return hourStats;

            foreach (var statsTotal in statsTotals)
            {
                hourStats.UpdateStats(statsTotal.DateTime, statsTotal);
                AddStatsForDateTimeLabel(statsCollection, statsTotal,
                    statsTotal.DateTime.ToString(statsCollection.LabelFormat));
            }

            return hourStats;
        }

        private static Task<List<Log>> GetHourStatisticsAsync(DateTime startDateTime, DateTime endDateTime)
        {
            var databaseStartTime = startDateTime.ToUnixTime();
            var databaseEndTime = endDateTime.ToUnixTime();
            return DatabaseManager.Logs
                .Where(x => x.DateTime >= databaseStartTime && x.DateTime <= databaseEndTime)
                .ToListAsync();
        }

        private static List<StatsTotals> CalculateStatsTotalsPerMinute(DateTime startDateTime, List<Log> hourStatistics)
        {
            var statsTotals = new List<StatsTotals>(StatsPerHour.MinutesInHour);

            for (var i = 1; i < StatsPerHour.MinutesInHour; i++)
            {
                var startDatePerMinute = startDateTime.AddMinutes(i);
                var endDatePerMinute = startDatePerMinute.AddMinutes(1);

                var databaseStartTime = startDatePerMinute.ToUnixTime();
                var databaseEndTime = endDatePerMinute.ToUnixTime();

                var statsListPerMinute = hourStatistics
                    .Where(x => x.DateTime >= databaseStartTime && x.DateTime <= databaseEndTime)
                    .ToList();

                var statsTotal = new StatsTotals(startDatePerMinute)
                {
                    TotalBlocked = statsListPerMinute.Count(x => x.IsBlocked),
                    TotalCached = statsListPerMinute.Count(x => x.IsFromCache && !x.IsBlocked),
                    TotalClients = statsListPerMinute.Select(x => x.ClientIp).Distinct().Count(),
                    TotalNameError = statsListPerMinute.Count(x => x.ResponseCode == ResponseCode.NameError && !x.IsBlocked),
                    TotalNoError = statsListPerMinute.Count(x => x.ResponseCode == ResponseCode.NoError && !x.IsBlocked),
                    TotalQueries = statsListPerMinute.Count,
                    TotalRefused = statsListPerMinute.Count(x => x.ResponseCode == ResponseCode.Refused && !x.IsBlocked),
                    TotalServerFailure = statsListPerMinute.Count(x => x.ResponseCode == ResponseCode.ServerFailure && !x.IsBlocked)
                };

                statsTotals.Add(statsTotal);
            }

            return statsTotals;
        }

        private static void AddStatsForDateTimeLabel(StatsCollection statsCollection, StatsTotals statsCounter, string label)
        {
            UpdateOrAddValue(statsCollection.Queries, label, statsCounter.TotalQueries);
            UpdateOrAddValue(statsCollection.Success, label, statsCounter.TotalNoError);
            UpdateOrAddValue(statsCollection.ServerError, label, statsCounter.TotalServerFailure);
            UpdateOrAddValue(statsCollection.NameError, label, statsCounter.TotalNameError);
            UpdateOrAddValue(statsCollection.Refused, label, statsCounter.TotalRefused);
            UpdateOrAddValue(statsCollection.Blocked, label, statsCounter.TotalBlocked);
            UpdateOrAddValue(statsCollection.Cached, label, statsCounter.TotalCached);
            UpdateOrAddValue(statsCollection.Clients, label, statsCounter.TotalClients);
        }

        private static void UpdateOrAddValue(List<StatsLog> dictionary, string key, long value)
        {
            var currentItem = dictionary.FirstOrDefault(x => x.Name == key);
            if (currentItem != null)
            {
                // Key already exists, so update the value
                currentItem.Value = value;
            }
            else
            {
                // Key doesn't exist, so add a new entry
                dictionary.Add(new StatsLog { Name = key, Value = value });
            }
        }
    }
}
