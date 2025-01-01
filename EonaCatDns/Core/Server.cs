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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using EonaCat.Dns.Core.Extensions;
using EonaCat.Dns.Core.Helpers;
using EonaCat.Dns.Core.MultiCast;
using EonaCat.Dns.Core.Records;
using EonaCat.Dns.Core.Servers;
using EonaCat.Dns.Database;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Helpers.Extensions;
using EonaCat.Logger;
using EonaCat.Network;

#pragma warning disable CS1591

namespace EonaCat.Dns.Core;

public class Server
{
    private static EonaCatDnsConfig _config;
    private static readonly object TotalRequestsLock = new();
    private Ns _ns;
    private SocketTcpServer _socketServerTcpV4;
    private SocketTcpServer _socketServerTcpV6;
    private SocketUdpServer _socketServerUdpV4;
    private SocketUdpServer _socketServerUdpV6;

    public static long TotalRequests { get; set; }
    public static long TotalBlocked { get; set; }
    public bool IsRunning { get; private set; }

    public static bool WatchMode { get; set; }

    public string MasterFile => "masterFile.txt";

    public Server WithConfig(EonaCatDnsConfig serverConfig)
    {
        _config = serverConfig;
        ResolveHelper.Config = _config;
        return this;
    }

    public async Task RestartAsync()
    {
        await Stop().ConfigureAwait(false);
        await StartAsync().ConfigureAwait(false);
    }

    public async Task StartAsync()
    {
        var ipAddresses = MultiCastService.GetAllIPAddresses().ToList();

        var hasValidIpV4 = await ValidateIpAddressAsync(_config.ListenAddressV4, ipAddresses, AddressFamily.InterNetwork).ConfigureAwait(false);
        var hasValidIpV6 = await ValidateIpAddressAsync(_config.ListenAddressV6, ipAddresses, AddressFamily.InterNetworkV6).ConfigureAwait(false);

        if (hasValidIpV4 || hasValidIpV6)
        {
            await CreateDnsServerAsync().ConfigureAwait(false);
            await StartMultiCastService().ConfigureAwait(false);

            if (hasValidIpV4) await StartServerAsync(_config.ListenAddressV4, AddressFamily.InterNetwork).ConfigureAwait(false);
            if (hasValidIpV6) await StartServerAsync(_config.ListenAddressV6, AddressFamily.InterNetworkV6).ConfigureAwait(false);

            await OutputStartupMessage().ConfigureAwait(false);
            IsRunning = true;
        }
        else
        {
            await Logger.LogAsync("No valid IP addresses found to bind to. Check configuration.", ELogType.ERROR).ConfigureAwait(false);
        }
    }

    private static async Task<bool> ValidateIpAddressAsync(string address, List<IPAddress> validAddresses, AddressFamily family)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;

        if (!validAddresses.Contains(IPAddress.Parse(address)))
        {
            var familyString = family == AddressFamily.InterNetwork ? "IPv4" : "IPv6";
            await Logger.LogAsync($"Invalid {familyString} address specified in the configuration.", ELogType.WARNING).ConfigureAwait(false);
            Console.WriteLine($"Valid {familyString} addresses:");
            validAddresses.Where(x => x.AddressFamily == family).ForEach(Console.WriteLine);
            return false;
        }

        return true;
    }

    private async Task StartServerAsync(string listenAddress, AddressFamily family)
    {
        var udpServer = new SocketUdpServer(listenAddress, 53);
        udpServer.OnReceive += (remoteInfo) => HandleIncomingQuery(remoteInfo);
        _ = udpServer.StartAsync(); // Fire-and-forget.

        var tcpServer = new SocketTcpServer(listenAddress, 53);
        tcpServer.OnReceive += (remoteInfo, _) => HandleIncomingQuery(remoteInfo);
        _ = tcpServer.StartAsync(); // Fire-and-forget.

        await Logger.LogAsync($"EonaCatDns started listening on {listenAddress}:53").ConfigureAwait(false);

        if (family == AddressFamily.InterNetwork)
        {
            _socketServerUdpV4 = udpServer;
            _socketServerTcpV4 = tcpServer;
        }
        else
        {
            _socketServerUdpV6 = udpServer;
            _socketServerTcpV6 = tcpServer;
        }
    }

    private void HandleIncomingQuery(RemoteInfo remoteInfo)
    {
        _ = StartQueryAsync(remoteInfo); // Fire-and-forget to handle asynchronously.
    }

    private async Task StartQueryAsync(RemoteInfo remoteInfo)
    {
        try
        {
            if (remoteInfo.EndPoint is not IPEndPoint) return;

            if (MessageHelper.TestDeviceOnly(remoteInfo.Address) || remoteInfo.Data.Length < DnsHeader.HeaderSize)
                return; // Skip invalid or test-only requests.

            var message = await MessageHelper.GetMessageFromBytes(remoteInfo.Data).ConfigureAwait(false);
            if (!message.Questions.Any())
            {
                await MessageHelper.PrintPacketDetails(remoteInfo.Data.Length, remoteInfo.Data, "Invalid record", true, true).ConfigureAwait(false);
                return;
            }

            await MessageHelper.PrintPacketDetails(remoteInfo.Data.Length, remoteInfo.Data, $"Incoming request {message.OperationCode}").ConfigureAwait(false);

            await ProcessQueryAsync(message, remoteInfo).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Logger.LogAsync(ex, writeToConsole: false).ConfigureAwait(false);
        }
    }

    private async Task ProcessQueryAsync(Message message, RemoteInfo remoteInfo)
    {
        switch (message.OperationCode)
        {
            case OperationCode.Query:
                QueryHelper.Configure(_config, _ns, _socketServerTcpV4, _socketServerUdpV4, _socketServerTcpV6, _socketServerUdpV6);
                await QueryHelper.ProcessQueryAsync(message, remoteInfo).ConfigureAwait(false);
                IncrementTotalRequests();
                break;

            case OperationCode.InverseQuery:
            case OperationCode.Status:
            case OperationCode.Notify:
            case OperationCode.Update:
                await Logger.LogAsync($"{message.OperationCode} ignored").ConfigureAwait(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(message.OperationCode), $"Unknown operation code: {message.OperationCode}");
        }
    }

    private static void IncrementTotalRequests()
    {
        lock (TotalRequestsLock) TotalRequests++;
    }



    private static async Task StartMultiCastService()
    {
        if (!_config.IsMultiCastEnabled)
        {
            return;
        }

        if (MultiCastDnsHelper.IsMultiCastEnabled)
        {
            return;
        }

        await MultiCastDnsHelper.StartAsync(_config.LogLevel, _config.LogInLocalTime).ConfigureAwait(false);
        await Logger.LogAsync("MultiCast service started").ConfigureAwait(false);
    }

    private async Task CreateDnsServerAsync()
    {
        var masterFileContents = string.Empty;

        try
        {
            if (!File.Exists(MasterFile) && _config.CreateMasterFileOnBoot)
            {
                masterFileContents = MasterWriter.GetDefaultMasterFile(_config.ListenAddressV4);
                await File.WriteAllTextAsync(MasterFile, masterFileContents).ConfigureAwait(false);
            }

            if (File.Exists(MasterFile))
            {
                masterFileContents = await File.ReadAllTextAsync(MasterFile).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            await Logger.LogAsync($"Could not load MasterFile {exception.Message}").ConfigureAwait(false);
        }

        _ns = new Ns
        {
            IsCacheDisabled = _config.IsCacheDisabled,
            Catalog = await Catalog.GenerateCatalog(masterFileContents).ConfigureAwait(false)
        };

        DatabaseManager.OnHostNameResolve -= DatabaseManager_OnHostNameResolve;
        DatabaseManager.OnHostNameResolve += DatabaseManager_OnHostNameResolve;
    }

    private void DatabaseManager_OnHostNameResolve(object sender, Client e)
    {
        if (!IPAddress.TryParse(e.Ip, out var clientIp))
        {
            return;
        }

        lock (_ns.Catalog)
        {
            // Create a copy of the catalog for enumeration
            var catalogCopy = _ns.Catalog.ToList();

            // Check if any item in the copy has the same Name as the clientIp's ArpaName
            if (catalogCopy.Any(x => x.Value.Name == clientIp.GetArpaName()))
            {
                return;
            }
        }

        if (e.Name.EndsWith(".local"))
        {
            e.Name = e.Name.Remove(e.Name.LastIndexOf(".local", StringComparison.Ordinal));
        }

        AddressRecordBase record = null;
        if (clientIp.AddressFamily == AddressFamily.InterNetwork)
        {
            record = new ARecord
            {
                Name = e.Name,
                Type = RecordType.A,
                Class = RecordClass.Internet,
                Address = clientIp
            };
        }
        else if (clientIp.AddressFamily == AddressFamily.InterNetworkV6)
        {
            record = new AaaaRecord
            {
                Name = e.Name,
                Type = RecordType.Aaaa,
                Class = RecordClass.Internet,
                Address = clientIp
            };
        }

        lock (_ns.Catalog)
        {
            try
            {
                // Check if any item in the catalog has the same Name as the clientIp's ArpaName
                if (_ns.Catalog.Any(x => x.Value.Name == clientIp.GetArpaName()))
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                _ = Logger.LogAsync(exception);
                return;
            }

            // Add the record to the catalog
            _ns.Catalog.AddReverseLookupRecord(record);
        }
    }

    private static async Task OutputStartupMessage()
    {
        var cacheDisabledMessage = _config.IsCacheDisabled ? "(Caching disabled)" : string.Empty;
        var resolveType = _config.ResolveOverDoh ? "(Resolving queries over DoH) " : string.Empty;
        var multiCastMessage = _config.IsMultiCastEnabled ? "(MultiCast enabled) " : string.Empty;
        await Logger.LogAsync($"{DllInfo.Name} started {cacheDisabledMessage} {resolveType} {multiCastMessage}")
            .ConfigureAwait(false);
    }

    public async Task Stop()
    {
        await StopMultiCastService().ConfigureAwait(false);
        await Logger.LogAsync($"{DllInfo.Name} stopped").ConfigureAwait(false);
        IsRunning = false;
    }

    private static async Task StopMultiCastService()
    {
        if (!_config.IsMultiCastEnabled)
        {
            return;
        }

        if (MultiCastDnsHelper.IsMultiCastEnabled)
        {
            return;
        }

        MultiCastDnsHelper.Stop();
        await Logger.LogAsync("MultiCast service stopped").ConfigureAwait(false);
    }

    public static Task MultiCastAsync(string[] args, bool appExitAfterCommand = true,
        bool onlyExitAfterKeypress = true)
    {
        return MultiCastDnsHelper.ResolveAsync(args, appExitAfterCommand, onlyExitAfterKeypress);
    }

    public static Task NetworkScanAsync(string[] args, bool appExitAfterCommand = true,
        bool onlyExitAfterKeypress = true)
    {
        return NetworkScanHelper.ResolveAsync(args, appExitAfterCommand, onlyExitAfterKeypress);
    }

    public static Task NsLookupAsync(string[] args, bool resolveOverDoh = true, bool appExitAfterCommand = true,
        bool onlyExitAfterKeyPress = true)
    {
        return NsLookupHelper.ResolveAsync(args, resolveOverDoh, appExitAfterCommand, onlyExitAfterKeyPress);
    }
}