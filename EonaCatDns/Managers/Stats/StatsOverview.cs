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

namespace EonaCat.Dns.Managers.Stats;

public class StatsOverview
{
    public StatsOverview(long totalBlocked, long totalCached, long totalQueries, long clients, long totalRefused,
        long totalServerFailure, long totalNoError)
    {
        TotalBlockList = Blocker.TotalBlocked;
        TotalAllowList = Blocker.TotalAllowed;
        TotalQueries = totalQueries;
        TotalBlocked = totalBlocked;
        TotalCached = totalCached;
        TotalRefused = totalRefused;
        TotalServerFailure = totalServerFailure;
        TotalNoError = totalNoError;
        TotalClients = clients;
    }

    public long TotalNoError { get; set; }

    public long TotalServerFailure { get; set; }

    public long TotalRefused { get; set; }

    public long TotalQueries { get; }
    public long TotalBlocked { get; }
    public long TotalCached { get; }
    public long TotalBlockList { get; }
    public long TotalAllowList { get; }
    public long TotalClients { get; }
}