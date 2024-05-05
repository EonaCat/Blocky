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

namespace EonaCat.Dns.Core.Records;

public enum EdnsOptionType : ushort
{
    Nsid = 3,

    Dau = 5,

    Dhu = 6,

    N3U = 7,

    ClientSubnet = 8,

    Expire = 9,

    Cookie = 10,

    Keepalive = 11,

    Padding = 12,

    Chain = 13,

    KeyTag = 14,

    ExperimentalMin = 65001,

    ExperimentalMax = 65534,

    FutureExpansion = 65535
}