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
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Exceptions;
using EonaCat.Dns.Managers.Stats;
using EonaCat.Helpers.Helpers;
using EonaCat.Json;

namespace EonaCat.Dns.Managers;

public class StatsManagerApi
{
    private readonly StatsManager _statsManager;

    internal StatsManagerApi(StatsManager statsManager)
    {
        _statsManager = statsManager;
    }

    public static string QueryType1Color { get; set; }
    public static string QueryType2Color { get; set; }
    public static string QueryType3Color { get; set; }
    public static string QueryType4Color { get; set; }
    public static string QueryType5Color { get; set; }
    public static string QueryType6Color { get; set; }
    public static string QueryType7Color { get; set; }
    public static string QueryType8Color { get; set; }
    public static string QueryType9Color { get; set; }
    public static string QueryType10Color { get; set; }

    public static string Transparency => "50";

    public static string ClientsBorderColor { get; set; }
    public static string ClientsBackgroundColor { get; set; }
    public static string BlockedBorderColor { get; set; }
    public static string BlockedBackgroundColor { get; set; }
    public static string AllowedBackgroundColor { get; set; }
    public static string RefusedBorderColor { get; set; }
    public static string RefusedBackgroundColor { get; set; }
    public static string NameErrorBorderColor { get; set; }
    public static string NameErrorBackgroundColor { get; set; }
    public static string ServerFailureBorderColor { get; set; }
    public static string ServerFailureBackgroundColor { get; set; }
    public static string CachedBorderColor { get; set; }
    public static string CachedBackgroundColor { get; set; }
    public static string NoErrorBorderColor { get; set; }
    public static string NoErrorBackgroundColor { get; set; }
    public static string TotalQueriesBorderColor { get; set; }
    public static string TotalQueriesBackgroundColor { get; set; }

    internal static async Task LoadStatsColorsAsync()
    {
        var settings = new List<Setting>
        {
            new() { Name = SettingName.ClientsBorderColor, Value = "#9639c3" },
            new() { Name = SettingName.ClientsBackgroundColor, Value = "#9639c3" },
            new() { Name = SettingName.BlockedBorderColor, Value = "#870808" },
            new() { Name = SettingName.AllowedBackgroundColor, Value = "#006442" },
            new() { Name = SettingName.BlockedBackgroundColor, Value = "#870808" },
            new() { Name = SettingName.RefusedBorderColor, Value = "#a11717" },
            new() { Name = SettingName.RefusedBackgroundColor, Value = "#a11717" },
            new() { Name = SettingName.NameErrorBorderColor, Value = "#c60606" },
            new() { Name = SettingName.NameErrorBackgroundcolor, Value = "#c60606" },
            new() { Name = SettingName.ServerFailureBorderColor, Value = "#1f1f1f" },
            new() { Name = SettingName.ServerFailureBackgroundColor, Value = "#1f1f1f" },
            new() { Name = SettingName.CachedBorderColor, Value = "#007542" },
            new() { Name = SettingName.CachedBackgroundColor, Value = "#007542" },
            new() { Name = SettingName.NoErrorBorderColor, Value = "#377901" },
            new() { Name = SettingName.NoErrorBackgroundColor, Value = "#377901" },
            new() { Name = SettingName.TotalQueriesBorderColor, Value = "#015579" },
            new() { Name = SettingName.TotalQueriesBackgroundColor, Value = "#015579" }
        };

        var queryTypeColors = new List<Setting>
        {
            new() { Name = SettingName.QueryType1Color, Value = "#6e66ff80" },
            new() { Name = SettingName.QueryType2Color, Value = "#ffa76680" },
            new() { Name = SettingName.QueryType3Color, Value = "#ff66be80" },
            new() { Name = SettingName.QueryType4Color, Value = "#ffed6680" },
            new() { Name = SettingName.QueryType5Color, Value = "#66e4ff80" },
            new() { Name = SettingName.QueryType6Color, Value = "#3fcc6f80" },
            new() { Name = SettingName.QueryType7Color, Value = "#cbcc3f80" },
            new() { Name = SettingName.QueryType8Color, Value = "#3f8acc80" },
            new() { Name = SettingName.QueryType9Color, Value = "#73cc3f80" },
            new() { Name = SettingName.QueryType10Color, Value = "#cc3f8480" }
        };

        await SaveSettingsIfNotExistAsync(settings).ConfigureAwait(false);
        await SaveSettingsIfNotExistAsync(queryTypeColors).ConfigureAwait(false);

        await SetColorAsync(SettingName.ClientsBorderColor, v => ClientsBorderColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.ClientsBackgroundColor, v => ClientsBackgroundColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.BlockedBorderColor, v => BlockedBorderColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.BlockedBackgroundColor, v => BlockedBackgroundColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.AllowedBackgroundColor, v => AllowedBackgroundColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.RefusedBorderColor, v => RefusedBorderColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.RefusedBackgroundColor, v => RefusedBackgroundColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.NameErrorBorderColor, v => NameErrorBorderColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.NameErrorBackgroundcolor, v => NameErrorBackgroundColor = v)
            .ConfigureAwait(false);
        await SetColorAsync(SettingName.ServerFailureBackgroundColor, v => ServerFailureBackgroundColor = v)
            .ConfigureAwait(false);
        await SetColorAsync(SettingName.CachedBorderColor, v => CachedBorderColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.CachedBackgroundColor, v => CachedBackgroundColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.NoErrorBorderColor, v => NoErrorBorderColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.NoErrorBackgroundColor, v => NoErrorBackgroundColor = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.TotalQueriesBorderColor, v => TotalQueriesBorderColor = v)
            .ConfigureAwait(false);
        await SetColorAsync(SettingName.TotalQueriesBackgroundColor, v => TotalQueriesBackgroundColor = v)
            .ConfigureAwait(false);

        await SetColorAsync(SettingName.QueryType1Color, v => QueryType1Color = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.QueryType2Color, v => QueryType2Color = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.QueryType3Color, v => QueryType3Color = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.QueryType4Color, v => QueryType4Color = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.QueryType5Color, v => QueryType5Color = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.QueryType6Color, v => QueryType6Color = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.QueryType7Color, v => QueryType7Color = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.QueryType8Color, v => QueryType8Color = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.QueryType9Color, v => QueryType9Color = v).ConfigureAwait(false);
        await SetColorAsync(SettingName.QueryType10Color, v => QueryType10Color = v).ConfigureAwait(false);
    }

    private static async Task SaveSettingsIfNotExistAsync(List<Setting> settings)
    {
        foreach (var setting in settings)
            if (await DatabaseManager.Settings.CountAllAsync(x => x.Name == setting.Name.ToUpper())
                  .ConfigureAwait(false) == 0)
            {
                await DatabaseManager.SetSettingAsync(setting).ConfigureAwait(false);
            }
    }

    private static async Task SetColorAsync(string settingName, Action<string> setter)
    {
        var setting = await DatabaseManager.GetSettingAsync(settingName).ConfigureAwait(false);
        setter(setting.Value);
    }

    internal Task<IDictionary<string, List<StatsLog>>> GetStatsAsync(string type, JsonTextWriter jsonWriter,
        bool isAuthenticated, bool forceNew)
    {
        type = string.IsNullOrEmpty(type) ? "lastHour" : type;
        var statsType = EnumHelper<StatsType>.Parse(type, true, StatsType.Invalid);

        if (statsType == StatsType.Invalid)
        {
            throw new WebException("EonaCatDns: " + "EonaCatDns: Unknown stats type requested: " + type);
        }

        forceNew = forceNew || statsType == StatsType.LastHour;
        return _statsManager.GetStatsDataAsync(statsType, forceNew, isAuthenticated);
    }
}