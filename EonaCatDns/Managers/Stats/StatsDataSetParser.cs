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

using System.Collections.Generic;
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Json;

namespace EonaCat.Dns.Managers.Stats;

public class StatsLog
{
    public string Name { get; set; }
    public long Value { get; set; }
}

internal static class StatsDataSetParser
{
    internal static async Task CreateQueryTypesArrayAsync(JsonTextWriter jsonWriter,
        IDictionary<string, List<StatsLog>> data)
    {
        await jsonWriter.WritePropertyNameAsync("queryTypeChartData").ConfigureAwait(false);
        await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

        List<StatsLog> queryTypes = null;
        if (data != null && data.ContainsKey("queryTypes"))
        {
            queryTypes = data["queryTypes"];
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
        var lastQueries = data != null && data.ContainsKey("lastQueries") ? data["lastQueries"] : null;
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
        var topBlockedDomains = data != null && data.ContainsKey("topBlocked") ? data["topBlocked"] : null;
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
        var topDomains = data != null && data.ContainsKey("topDomains") ? data["topDomains"] : null;
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
        var topClients = data != null && data.ContainsKey("topClients") ? data["topClients"] : null;
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

        if (data.ContainsKey("topClients"))
        {
            data["topClients"].Clear();
        }

        if (data.ContainsKey("topDomains"))
        {
            data["topDomains"].Clear();
        }

        if (data.ContainsKey("topBlocked"))
        {
            data["topBlocked"].Clear();
        }
    }

    internal static async Task CreateStatisticsDataArrayAsync(JsonTextWriter jsonWriter,
        IDictionary<string, List<StatsLog>> data)
    {
        await jsonWriter.WritePropertyNameAsync("statisticsData").ConfigureAwait(false);
        await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
        var stats = data != null && data.ContainsKey(ConstantsDns.Stats.TotalQueries)
            ? data[ConstantsDns.Stats.TotalQueries]
            : null;

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
            if (data.ContainsKey(ConstantsDns.Stats.TotalQueries))
            {
                WriteChartDataSet(jsonWriter, "Total Queries",
                    StatsManagerApi.TotalQueriesBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.TotalQueriesBorderColor,
                    data[ConstantsDns.Stats.TotalQueries]);
            }

            if (data.ContainsKey(ConstantsDns.Stats.TotalNoError))
            {
                WriteChartDataSet(jsonWriter, "No Error",
                    StatsManagerApi.NoErrorBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.NoErrorBorderColor,
                    data[ConstantsDns.Stats.TotalNoError]);
            }

            if (data.ContainsKey(ConstantsDns.Stats.TotalCached))
            {
                WriteChartDataSet(jsonWriter, "Cached",
                    StatsManagerApi.CachedBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.CachedBorderColor,
                    data[ConstantsDns.Stats.TotalCached]);
            }

            if (data.ContainsKey(ConstantsDns.Stats.TotalServerFailure))
            {
                WriteChartDataSet(jsonWriter, "Server Failure",
                    StatsManagerApi.ServerFailureBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.ServerFailureBorderColor,
                    data[ConstantsDns.Stats.TotalServerFailure]);
            }

            if (data.ContainsKey(ConstantsDns.Stats.TotalNameError))
            {
                WriteChartDataSet(jsonWriter, "Name Error",
                    StatsManagerApi.NameErrorBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.NameErrorBorderColor,
                    data[ConstantsDns.Stats.TotalNameError]);
            }

            if (data.ContainsKey(ConstantsDns.Stats.TotalRefused))
            {
                WriteChartDataSet(jsonWriter, "Refused",
                    StatsManagerApi.RefusedBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.RefusedBorderColor,
                    data[ConstantsDns.Stats.TotalRefused]);
            }

            if (data.ContainsKey(ConstantsDns.Stats.TotalBlocked))
            {
                WriteChartDataSet(jsonWriter, "Blocked",
                    StatsManagerApi.BlockedBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.BlockedBorderColor,
                    data[ConstantsDns.Stats.TotalBlocked]);
            }

            if (data.ContainsKey(ConstantsDns.Stats.TotalClients))
            {
                WriteChartDataSet(jsonWriter, "Clients",
                    StatsManagerApi.ClientsBackgroundColor + StatsManagerApi.Transparency,
                    StatsManagerApi.ClientsBorderColor,
                    data[ConstantsDns.Stats.TotalClients]);
            }
        }

        await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
        await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
    }

    internal static async Task CreateStatsArrayAsync(JsonTextWriter jsonWriter,
        IDictionary<string, List<StatsLog>> data)
    {
        var stats = data != null && data.ContainsKey("stats") ? data["stats"] : null;
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