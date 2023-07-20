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

internal class StatsTotals
{
    private static readonly object UpdateLock = new();

    public StatsTotals(DateTime dateTime)
    {
        DateTime = dateTime;
    }

    public int TotalClients;

    public long TotalQueries;

    public long TotalNoError;

    public long TotalServerFailure;

    public long TotalNameError;

    public long TotalRefused;

    public long TotalBlocked;

    public long TotalCached;

    public DateTime DateTime { get; }

    public void Merge(StatsTotals totalStats, bool skipQueryUpdate = false)
    {
        lock (UpdateLock)
        {
            if (totalStats == null)
            {
                return;
            }

            if (!skipQueryUpdate)
            {
                TotalQueries += totalStats.TotalQueries;
                TotalNoError += totalStats.TotalNoError;
                TotalServerFailure += totalStats.TotalServerFailure;
                TotalNameError += totalStats.TotalNameError;
                TotalRefused += totalStats.TotalRefused;
                TotalBlocked += totalStats.TotalBlocked;
                TotalCached += totalStats.TotalCached;
            }
        }
    }
}