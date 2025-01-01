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

using System.Collections.Generic;
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Json;

namespace EonaCat.Dns.Managers.Stats;

internal static class StatsDataSetParser
{
    internal static async Task CreateQueryTypesArrayAsync(JsonTextWriter jsonWriter,
        IDictionary<string, List<StatsLog>> data)
    {
        await jsonWriter.WritePropertyNameAsync("queryTypeChartData").ConfigureAwait(false);
        await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

        List<StatsLog> queryTypes = null;
        if (data != null && data.TryGetValue("queryTypes", out List<StatsLog> value))
        {
            queryTypes = value;
        }

        await jsonWriter.WritePropertyNameAsync("labels").ConfigureAwait(false);
        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);

        if (queryTypes != null)
        {
            foreach (var item in queryTypes)
                await jsonWriter.WriteValueAsync(item.Name).ConfigureAwait(false);
        }

        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
        await jsonWriter.WritePropertyNameAsync("datasets").ConfigureAwait(false);
        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);
        await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
        await jsonWriter.WritePropertyNameAsync("data").ConfigureAwait(false);
        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);

        if (queryTypes != null)
        {
            foreach (var item in queryTypes)
                await jsonWriter.WriteValueAsync(item.Value).ConfigureAwait(false);
        }

        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
        await jsonWriter.WritePropertyNameAsync("backgroundColor").ConfigureAwait(false);
        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);
        await jsonWriter.WriteValueAsync(StatsManagerApi.QueryType1Color).ConfigureAwait(false);
        await jsonWriter.WriteValueAsync(StatsManagerApi.QueryType2Color).ConfigureAwait(false);
        await jsonWriter.WriteValueAsync(StatsManagerApi.QueryType3Color).ConfigureAwait(false);
        await jsonWriter.WriteValueAsync(StatsManagerApi.QueryType4Color).ConfigureAwait(false);
        await jsonWriter.WriteValueAsync(StatsManagerApi.QueryType5Color).ConfigureAwait(false);

        await jsonWriter.WriteValueAsync(StatsManagerApi.QueryType6Color).ConfigureAwait(false);
        await jsonWriter.WriteValueAsync(StatsManagerApi.QueryType7Color).ConfigureAwait(false);
        await jsonWriter.WriteValueAsync(StatsManagerApi.QueryType8Color).ConfigureAwait(false);
        await jsonWriter.WriteValueAsync(StatsManagerApi.QueryType9Color).ConfigureAwait(false);
        await jsonWriter.WriteValueAsync(StatsManagerApi.QueryType10Color).ConfigureAwait(false);
        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
        await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
        await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
    }

    internal static async Task CreateLastQueriesArrayAsync(JsonTextWriter jsonWriter,
        IDictionary<string, List<StatsLog>> data)
    {
        var lastQueries = data != null && data.TryGetValue("lastQueries", out List<StatsLog> value) ? value : null;
        await jsonWriter.WritePropertyNameAsync("lastQueries").ConfigureAwait(false);
        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);

        if (lastQueries != null)
        {
            foreach (var item in lastQueries)
            {
                var domain = await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Url == item.Name)
                    .ConfigureAwait(false);
                await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                if (domain != null)
                {
                    await jsonWriter.WritePropertyNameAsync("id").ConfigureAwait(false);
                    await jsonWriter.WriteValueAsync(domain.Id).ConfigureAwait(false);

                    await jsonWriter.WritePropertyNameAsync("isBlocked").ConfigureAwait(false);
                    await jsonWriter.WriteValueAsync(domain.ListType == ListType.Blocked ? 1 : 0).ConfigureAwait(false);
                }

                await jsonWriter.WritePropertyNameAsync("name").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync(item.Name).ConfigureAwait(false);
                await jsonWriter.WritePropertyNameAsync("hits").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync(item.Value).ConfigureAwait(false);
                await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
            }
        }

        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
    }

    internal static async Task CreateTopBlockedDomainsAsync(JsonTextWriter jsonWriter,
        IDictionary<string, List<StatsLog>> data)
    {
        var topBlockedDomains = data != null && data.TryGetValue("topBlocked", out List<StatsLog> value) ? value : null;
        await jsonWriter.WritePropertyNameAsync("topBlocked").ConfigureAwait(false);
        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);

        if (topBlockedDomains != null)
        {
            foreach (var item in topBlockedDomains)
            {
                var domain = await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Url == item.Name)
                    .ConfigureAwait(false);
                await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                if (domain != null)
                {
                    await jsonWriter.WritePropertyNameAsync("id").ConfigureAwait(false);
                    await jsonWriter.WriteValueAsync(domain.Id).ConfigureAwait(false);

                    await jsonWriter.WritePropertyNameAsync("isBlocked").ConfigureAwait(false);
                    await jsonWriter.WriteValueAsync(domain.ListType == ListType.Blocked ? 1 : 0).ConfigureAwait(false);
                }

                await jsonWriter.WritePropertyNameAsync("name").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync(item.Name).ConfigureAwait(false);
                await jsonWriter.WritePropertyNameAsync("hits").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync(item.Value).ConfigureAwait(false);
                await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
            }
        }

        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
    }

    internal static async Task CreateTopDomainsAsync(JsonTextWriter jsonWriter,
        IDictionary<string, List<StatsLog>> data)
    {
        var topDomains = data != null && data.TryGetValue("topDomains", out List<StatsLog> value) ? value : null;
        await jsonWriter.WritePropertyNameAsync("topDomains").ConfigureAwait(false);
        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);

        if (topDomains != null)
        {
            foreach (var item in topDomains)
            {
                var domain = await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Url == item.Name)
                    .ConfigureAwait(false);
                await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                if (domain != null)
                {
                    await jsonWriter.WritePropertyNameAsync("id").ConfigureAwait(false);
                    await jsonWriter.WriteValueAsync(domain.Id).ConfigureAwait(false);

                    await jsonWriter.WritePropertyNameAsync("isBlocked").ConfigureAwait(false);
                    await jsonWriter.WriteValueAsync(domain.ListType == ListType.Blocked ? 1 : 0).ConfigureAwait(false);
                }

                await jsonWriter.WritePropertyNameAsync("name").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync(item.Name).ConfigureAwait(false);
                await jsonWriter.WritePropertyNameAsync("hits").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync(item.Value).ConfigureAwait(false);
                await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
            }
        }

        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
    }

    internal static async Task CreateTopClientsArrayAsync(JsonTextWriter jsonWriter,
        IDictionary<string, List<StatsLog>> data)
    {
        var topClients = data != null && data.TryGetValue("topClients", out List<StatsLog> value) ? value : null;
        await jsonWriter.WritePropertyNameAsync("topClients").ConfigureAwait(false);
        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);

        if (topClients != null)
        {
            foreach (var item in topClients)
            {
                var client = await DatabaseManager.Clients.FirstOrDefaultAsync(x => x.Ip == item.Name)
                    .ConfigureAwait(false);
                await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                await jsonWriter.WritePropertyNameAsync("name").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync(item.Name).ConfigureAwait(false);

                if (client == null)
                {
                    await jsonWriter.WritePropertyNameAsync("isBlocked").ConfigureAwait(false);
                    await jsonWriter.WriteValueAsync(0).ConfigureAwait(false);
                    await jsonWriter.WritePropertyNameAsync("alias").ConfigureAwait(false);
                    await jsonWriter.WriteValueAsync(string.Empty).ConfigureAwait(false);
                }
                else
                {
                    await jsonWriter.WritePropertyNameAsync("isBlocked").ConfigureAwait(false);
                    await jsonWriter.WriteValueAsync(client.IsBlocked ? 1 : 0).ConfigureAwait(false);
                    await jsonWriter.WritePropertyNameAsync("alias").ConfigureAwait(false);
                    await jsonWriter.WriteValueAsync(client.Name ?? string.Empty).ConfigureAwait(false);
                }

                await jsonWriter.WritePropertyNameAsync("hits").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync(item.Value).ConfigureAwait(false);
                await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
            }
        }

        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
    }

    internal static void ClearUnauthenticatedData(bool isAuthenticated, IDictionary<string, List<StatsLog>> data)
    {
        if (isAuthenticated)
        {
            return;
        }

        if (data == null)
        {
            return;
        }

        if (data.TryGetValue("topClients", out List<StatsLog> topClients))
        {
            topClients.Clear();
        }

        if (data.TryGetValue("topDomains", out List<StatsLog> topDomains))
        {
            topDomains.Clear();
        }

        if (data.TryGetValue("topBlocked", out List<StatsLog> topBlocked))
        {
            topBlocked.Clear();
        }
    }

    internal static async Task CreateStatisticsDataArrayAsync(JsonTextWriter jsonWriter,
        IDictionary<string, List<StatsLog>> data)
    {
        await jsonWriter.WritePropertyNameAsync("statisticsData").ConfigureAwait(false);
        await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
        var stats = data != null && data.TryGetValue(ConstantsDns.Stats.TotalQueries, out List<StatsLog> value)
            ? value : null;

        await jsonWriter.WritePropertyNameAsync("labels").ConfigureAwait(false);
        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);
        if (stats != null)
        {
            foreach (var item in stats)
                await jsonWriter.WriteValueAsync(item.Name).ConfigureAwait(false);
        }

        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
        await jsonWriter.WritePropertyNameAsync("datasets").ConfigureAwait(false);
        await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);

        if (data != null)
        {
            if (data.TryGetValue(ConstantsDns.Stats.TotalQueries, out List<StatsLog> totalQueries))
            {
                WriteChartDataSet(jsonWriter, "Total Queries",
                    StatsManagerApi.TotalQueriesBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.TotalQueriesBorderColor, totalQueries);
            }

            if (data.TryGetValue(ConstantsDns.Stats.TotalNoError, out List<StatsLog> totalNoErrors))
            {
                WriteChartDataSet(jsonWriter, "No Error",
                    StatsManagerApi.NoErrorBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.NoErrorBorderColor, totalNoErrors);
            }

            if (data.TryGetValue(ConstantsDns.Stats.TotalCached, out List<StatsLog> totalCached))
            {
                WriteChartDataSet(jsonWriter, "Cached",
                    StatsManagerApi.CachedBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.CachedBorderColor, totalCached);
            }

            if (data.TryGetValue(ConstantsDns.Stats.TotalServerFailure, out List<StatsLog> totalServerFailure))
            {
                WriteChartDataSet(jsonWriter, "Server Failure",
                    StatsManagerApi.ServerFailureBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.ServerFailureBorderColor, totalServerFailure);
            }

            if (data.TryGetValue(ConstantsDns.Stats.TotalNameError, out List<StatsLog> totalNameError))
            {
                WriteChartDataSet(jsonWriter, "Name Error",
                    StatsManagerApi.NameErrorBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.NameErrorBorderColor, totalNameError);
            }

            if (data.TryGetValue(ConstantsDns.Stats.TotalRefused, out List<StatsLog> totalRefused))
            {
                WriteChartDataSet(jsonWriter, "Refused",
                    StatsManagerApi.RefusedBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.RefusedBorderColor, totalRefused);
            }

            if (data.TryGetValue(ConstantsDns.Stats.TotalBlocked, out List<StatsLog> totalBlocked))
            {
                WriteChartDataSet(jsonWriter, "Blocked",
                    StatsManagerApi.BlockedBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.BlockedBorderColor, totalBlocked);
            }

            if (data.TryGetValue(ConstantsDns.Stats.TotalClients, out List<StatsLog> totalClients))
            {
                WriteChartDataSet(jsonWriter, "Clients",
                    StatsManagerApi.ClientsBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.ClientsBorderColor, totalClients);
            }
        }

        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
        await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
    }

    internal static async Task CreateStatsArrayAsync(JsonTextWriter jsonWriter,
        IDictionary<string, List<StatsLog>> data)
    {
        var stats = data != null && data.TryGetValue("stats", out List<StatsLog> value) ? value : null;
        await jsonWriter.WritePropertyNameAsync("stats").ConfigureAwait(false);
        await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

        if (stats != null)
        {
            foreach (var item in stats)
            {
                await jsonWriter.WritePropertyNameAsync(item.Name).ConfigureAwait(false);
                await jsonWriter.WriteValueAsync(item.Value).ConfigureAwait(false);
            }
        }

        await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
    }

    internal static void WriteChartDataSet(JsonTextWriter jsonWriter, string label, string backgroundColor,
        string borderColor, List<StatsLog> stats)
    {
        jsonWriter.WriteStartObject();

        jsonWriter.WritePropertyName("label");
        jsonWriter.WriteValue(label);

        jsonWriter.WritePropertyName("backgroundColor");
        jsonWriter.WriteValue(backgroundColor);

        jsonWriter.WritePropertyName("borderColor");
        jsonWriter.WriteValue(borderColor);

        jsonWriter.WritePropertyName("borderWidth");
        jsonWriter.WriteValue(2);

        jsonWriter.WritePropertyName("data");
        jsonWriter.WriteStartArray();
        foreach (var item in stats) jsonWriter.WriteValue(item.Value);

        jsonWriter.WriteEndArray();

        jsonWriter.WriteEndObject();
    }
}