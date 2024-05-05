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

namespace EonaCat.Dns.Core;

public enum ResponseCode : byte
{
    NoError = 0,

    FormatError = 1,

    ServerFailure = 2,

    NameError = 3,

    NotImplemented = 4,

    Refused = 5,

    YxDomain = 6,

    YxrrSet = 7,

    NxrrSet = 8,

    NotAuthoritative = 9,

    NotZone = 10,

    BadVersion = 16,

    BadSignature = 16,

    BadKey = 17,

    BadTime = 18,

    Badmode = 19,

    Badname = 20,

    Badalg = 21
}