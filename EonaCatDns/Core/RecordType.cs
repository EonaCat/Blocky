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

public enum RecordType : ushort
{
    A = 1,
    Ns = 2,
    Md = 3,
    Mf = 4,
    Cname = 5,
    Soa = 6,
    Mb = 7,
    Mg = 8,
    Mr = 9,
    Null = 10,
    Wks = 11,
    Ptr = 12,
    Hinfo = 13,
    Minfo = 14,
    Mx = 15,
    Txt = 16,
    Rp = 17,
    Afsdb = 18,
    X25 = 19,
    Isdn = 20,
    Rt = 21,
    Nsap = 22,
    Nsapptr = 23,
    Sig = 24,
    Key = 25,
    Px2 = 26,
    Gpos = 27,
    Aaaa = 28,
    Loc = 29,
    Nxt = 30,
    Eid = 31,
    Nimloc = 32,
    Srv = 33,
    Atma = 34,
    Naptr = 35,
    Kx = 36,
    Cert = 37,
    A6 = 38,
    Dname = 39,
    Sink = 40,
    Opt = 41,
    Apl = 42,
    Ds = 43,
    Sshfp = 44,
    Ipseckey = 45,
    Rrsig = 46,
    Nsec = 47,
    Dnskey = 48,
    Dhcid = 49,
    Nsec3 = 50,
    Nsec3Param = 51,
    Tlsa = 52,
    Smimea = 53,
    Hip = 55,
    Ninfo = 56,
    Rkey = 57,
    Talink = 58,
    Cds = 59,
    Cdnskey = 60,
    Openpgpkey = 61,
    Csync = 62,
    Zonemd = 63,
    Svcb = 64,
    Https = 65,
    Spf = 99,
    Uinfo = 100,
    Uid = 101,
    Gid = 102,
    Unspec = 103,
    Tkey = 249,
    Tsig = 250,
    Ixfr = 251,
    Axfr = 252,
    Mailb = 253,
    Maila = 254,
    Any = 255,
    Uri = 256,
    Caa = 257,
    Avc = 258,
    Doa = 259,
    Amtrelay = 260,
    Ta = 32768,
    Dlv = 32769,
    Unknown = 59999
}