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

namespace EonaCat.Dns.Managers.Stats;

internal class StatsPerHour
{
    public StatsPerHour(DateTime statsByHour)
    {
        StatsByHour = new StatsTotals(statsByHour);
    }

    internal static int HoursInDay => 24;
    internal static int DaysInYear => 365;
    internal static int DaysInMonth => 31;
    internal static int DaysInWeek => 7;
    internal static int MinutesInHour => 60;

    public StatsTotals StatsByHour { get; }

    public StatsTotals[] StatsByMinute { get; } = new StatsTotals[MinutesInHour];

    public void UpdateStats(DateTime dateTime, StatsTotals statsPerMinute)
    {
        StatsByHour.Merge(statsPerMinute);
        StatsByMinute[dateTime.Minute] = statsPerMinute;
    }
}