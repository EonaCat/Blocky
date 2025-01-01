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

namespace EonaCat.Dns.Core.Records;

public enum SecurityAlgorithm : byte
{
    Delete = 0,

    Rsamd5 = 1,

    Dh = 2,

    Dsa = 3,

    Rsasha1 = 5,

    Dsansec3Sha1 = 6,

    Rsasha1Nsec3Sha1 = 7,

    Rsasha256 = 8,

    Rsasha512 = 10,

    Eccgost = 12,

    Ecdsap256Sha256 = 13,

    Ecdsap384Sha384 = 14,

    Ed25519 = 15,

    Ed448 = 16,

    Indirect = 252,

    Privatedns = 253,

    Privateoid = 254
}