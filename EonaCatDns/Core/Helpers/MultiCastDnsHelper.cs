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

using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using EonaCat.Dns.Core.MultiCast;
using EonaCat.Dns.Core.Records;
using EonaCat.Logger;

namespace EonaCat.Dns.Core.Helpers;

internal class MultiCastDnsHelper
{
    internal static ConcurrentDictionary<string, string> Hosts = new();
    public static bool IsMultiCastEnabled => MultiCastService is { IsRunning: true };
    internal static MultiCastService MultiCastService { get; private set; }
    private static ServiceDiscovery ServiceDiscovery { get; set; }

    internal static Task StartAsync(ELogType maxLogType = ELogType.DEBUG, bool useLocalTime = false)
    {
        Task.Run(async () =>
        {
            LoggerMultiCast.MaxLogType = maxLogType;
            LoggerMultiCast.UseLocalTime = useLocalTime;
            LoggerMultiCast.Configure();

            Hosts.Clear();
            MultiCastService = new MultiCastService();
            ServiceDiscovery = new ServiceDiscovery(MultiCastService);

            MultiCastService.NetworkInterfaceDiscovered += (_, _) =>
            {
                // Ask for the name of all services.
                ServiceDiscovery.QueryAllServices();
            };

            ServiceDiscovery.ServiceDiscovered += (_, serviceName) =>
            {
                // Ask for the name of instances of the service.
                MultiCastService.SendQueryAsync(serviceName, type: RecordType.Ptr);
            };

            ServiceDiscovery.ServiceInstanceDiscovered += (_, e) =>
            {
                // Ask for the service instance details.
                MultiCastService.SendQueryAsync(e.ServiceInstanceName, type: RecordType.Srv);
            };

            MultiCastService.AnswerReceived += async (_, e) =>
            {
                // Is this an answer to a service instance details?
                var servers = e.Message.Answers.OfType<SrvRecord>();
                foreach (var server in servers)
                {
                    await LoggerMultiCast.LogAsync($"host '{server.Target}' for '{server.Name}'").ConfigureAwait(false);

                    // Ask for the host IP addresses.
                    await MultiCastService.SendQueryAsync(server.Target, type: RecordType.A).ConfigureAwait(false);
                    await MultiCastService.SendQueryAsync(server.Target, type: RecordType.Aaaa).ConfigureAwait(false);
                }

                // Is this an answer to host addresses?
                var addresses = e.Message.Answers.OfType<AddressRecordBase>();
                foreach (var address in addresses)
                {
                    await LoggerMultiCast.LogAsync($"host '{address.Name}' at {address.Address}").ConfigureAwait(false);
                    if (!Hosts.TryGetValue(address.Address.ToString(), out var name))
                    {
                        Hosts.TryAdd(address.Address.ToString(), address.Name.ToString());
                    }
                    else
                    {
                        Hosts[address.Address.ToString()] = name;
                    }
                }
            };

            try
            {
                if (!MultiCastService.IsRunning)
                {
                    await MultiCastService.StartAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                Stop();
            }

            while (IsMultiCastEnabled) await Task.Delay(200).ConfigureAwait(false);
        });
        return Task.CompletedTask;
    }

    internal static void Stop()
    {
        ServiceDiscovery?.Dispose();
        MultiCastService?.Stop();
    }

    public static async Task ResolveAsync(string[] args, bool appExitAfterCommand, bool onlyExitAfterKeyPress = true)
    {
        if (args.Length <= 0)
        {
            return;
        }

        if (!args[0].ToLower().StartsWith("--mdns"))
        {
            return;
        }

        await StartAsync(ELogType.TRACE).ConfigureAwait(false);

        ToolHelper.ExitAfterTool(appExitAfterCommand, onlyExitAfterKeyPress);
    }
}