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

namespace EonaCat.Dns;

public static partial class ConstantsDns
{
    public static class Stats
    {
        public static int Top { get; internal set; } = 50;
        public static int RefreshInterval { get; internal set; }
        public static int ExtraMinutesTime { get; internal set; } = 1;
        public static int ExtraHourTime { get; internal set; } = 1;
        public static string TopDomains => "topDomains";
        public static string LastQueries => "lastQueries";
        public static string TopBlockedDomains => "topBlocked";
        public static string TopClients => "topClients";
        public static string QueryTypes => "queryTypes";
        public static string TotalQueries => "totalQueries";
        public static string TotalNoError => "totalNoError";
        public static string TotalServerFailure => "totalServerFailure";
        public static string TotalNameError => "totalNameError";
        public static string TotalRefused => "totalRefused";
        public static string TotalBlocked => "totalBlocked";
        public static string TotalCached => "totalCached";
        public static string TotalClients => "totalClients";
        public static string TotalStats => "stats";
    }
}