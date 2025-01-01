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
using EonaCat.Logger;

namespace EonaCat.Dns;

public class EonaCatDnsConfig
{
    public ELogType LogLevel { get; set; } = ELogType.DEBUG;
    public string ArpaPostFix => ".arpa";
    public int StatsRefreshInterval { get; set; }
    public bool LogBlockedClients { get; set; }
    public string ListenAddressV6 { get; set; }
    public string ResolverAddressV6 { get; set; }
    public List<string> ForwardersV6 { get; set; }
    public List<string> ForwardersDoH { get; set; }
    public string ListenAddressV4 { get; set; }
    public string ResolverAddressV4 { get; set; }
    public List<string> ForwardersV4 { get; set; }
    public bool AutoUpdate { get; set; }
    public int Port { get; set; }
    public string ApplicationName { get; set; }
    public string ApplicationVersion { get; set; }
    public string WebServerIpAddress { get; set; }
    public int WebServerPort { get; set; }
    public string[] Resolvers { get; set; }
    public List<string> DontCacheList { get; set; }
    public bool EnableAdminInterface { get; set; } = true;
    public bool IsCacheDisabled { get; set; }
    public bool ResolveOverDoh { get; set; }
    public bool ContinueWhenDohFails { get; set; }
    public bool CreateMasterFileOnBoot { get; set; }
    public bool IgnoreWpadRequests { get; set; }
    public bool IgnoreArpaRequests { get; set; }
    public bool IncludeRawInLogTable { get; set; }
    public bool IsMultiCastEnabled { get; set; }
    public string RouterDomain { get; set; }
    public bool PartialLookupName { get; set; }
    public bool ProgressToConsole { get; set; }
    public bool LogInLocalTime { get; set; }
}