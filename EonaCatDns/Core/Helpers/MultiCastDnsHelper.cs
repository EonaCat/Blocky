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

using EonaCat.Dns.Core.MultiCast;
using EonaCat.Dns.Core.Records;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using EonaCat.Logger;

namespace EonaCat.Dns.Core.Helpers
{
    internal class MultiCastDnsHelper
    {
        internal static ConcurrentDictionary<string, string> Hosts = new ConcurrentDictionary<string, string>();
        public static bool IsMultiCastEnabled => MultiCastService is { IsRunning: true };
        internal static MultiCastService MultiCastService { get; private set; }
        private static ServiceDiscovery ServiceDiscovery { get; set; }

        internal static async Task StartAsync(ELogType maxLogType = ELogType.INFO, bool useLocalTime = false)
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

            MultiCastService.AnswerReceived += (_, e) =>
            {
                // Is this an answer to a service instance details?
                var servers = e.Message.Answers.OfType<SrvRecord>();
                foreach (var server in servers)
                {
                    LoggerMultiCast.Log($"host '{server.Target}' for '{server.Name}'");

                    // Ask for the host IP addresses.
                    MultiCastService.SendQueryAsync(server.Target, type: RecordType.A);
                    MultiCastService.SendQueryAsync(server.Target, type: RecordType.Aaaa);
                }

                // Is this an answer to host addresses?
                var addresses = e.Message.Answers.OfType<AddressRecordBase>();
                foreach (var address in addresses)
                {
                    LoggerMultiCast.Log($"host '{address.Name}' at {address.Address}");
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
                    MultiCastService.Start();
                }
            }
            catch
            {
                Stop();
            }

            while (IsMultiCastEnabled)
            {
                await Task.Delay(200).ConfigureAwait(false);
            }
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
}