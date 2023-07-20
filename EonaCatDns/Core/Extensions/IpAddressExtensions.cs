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

using EonaCat.Dns.Core.Helpers;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EonaCat.Dns.Core.Extensions
{
    public static class IpAddressExtensions
    {
        public static string GetArpaName(this IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            Array.Reverse(bytes);

            switch (ip.AddressFamily)
            {
                // check IP6
                case AddressFamily.InterNetworkV6:
                    {
                        // reversed bytes need to be split into 4 bit parts and separated by '.'
                        var newBytes = bytes
                            .SelectMany(b => new[] { b >> 0 & 15, b >> 4 & 15 })
                            .Aggregate(new StringBuilder(), (s, b) => s.Append(b.ToString("x")).Append(".")) + "ip6" + ResolveHelper.Config.ArpaPostFix;

                        return newBytes;
                    }
                case AddressFamily.InterNetwork:
                    // else IP4
                    return string.Join(".", bytes) + ".in-addr" + ResolveHelper.Config.ArpaPostFix;

                default:
                    throw new ArgumentException("EonaCatDns: " + $"Unsupported address family '{ip.AddressFamily}'.", nameof(ip));
            }
        }
    }
}