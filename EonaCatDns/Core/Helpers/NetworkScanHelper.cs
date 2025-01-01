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

using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace EonaCat.Dns.Core.Helpers;

internal class NetworkScanHelper
{
    internal static async Task ResolveAsync(string[] args, bool appExitAfterCommand, bool onlyExitAfterKeyPress)
    {
        if (args.Length <= 0)
        {
            return;
        }

        if (!args[0].ToLower().StartsWith("--scan"))
        {
            return;
        }

        var networkRangeStart = args[1];
        string networkRangeEnd = null;

        if (args.Length > 2)
        {
            networkRangeEnd = args[1];
        }

        var ipReachable = new List<string>();

        var ips = GetIps(networkRangeStart, networkRangeEnd);

        Console.WriteLine("Scanning local network...");

        Parallel.ForEach(ips, async ip =>
        {
            using var ping = new Ping();
            try
            {
                var response = await ping.SendPingAsync(ip).ConfigureAwait(false);
                if (response.Status == IPStatus.Success)
                {
                    ipReachable.Add(ip);
                }
            }
            catch (Exception)
            {
                // Do nothing
            }
        });

        ipReachable.Sort();

        Console.Clear();
        Console.WriteLine($"IPs found ({ipReachable.Count}):");

        foreach (var ip in ipReachable)
        {
            var result = string.Empty;
            try
            {
                result = (await System.Net.Dns.GetHostEntryAsync(ip).ConfigureAwait(false)).HostName;
            }
            catch (SocketException)
            {
            }

            Console.WriteLine($"{ip} {result}");
        }

        ToolHelper.ExitAfterTool(appExitAfterCommand, onlyExitAfterKeyPress);
    }

    private static string[] GetIps(string networkRangeStart, string networkRangeEnd)
    {
        var ips = new List<string>();
        try
        {
            var rangeStart = networkRangeStart.Split('.');
            var rangeEnd = networkRangeEnd?.Split('.');

            var startJ = int.Parse(rangeStart[2]);
            var startI = int.Parse(rangeStart[3]);
            var endI = 256;
            var endJ = 256;

            if (rangeEnd != null)
            {
                int.TryParse(rangeEnd[2], out endJ);
                int.TryParse(rangeEnd[3], out endI);
            }

            for (var j = startJ; j <= endJ; j++)
            {
                for (var i = startI; i <= endI; i++)
                {
                    ips.Add($"{rangeStart[0]}.{rangeStart[1]}.{j}.{i}");
                }
            }
        }
        catch (Exception)
        {
            // Do nothing
        }

        return ips.ToArray();
    }
}