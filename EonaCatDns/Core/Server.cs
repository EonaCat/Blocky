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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using EonaCat.Dns.Converters;
using EonaCat.Dns.Core.Extensions;
using EonaCat.Dns.Core.Helpers;
using EonaCat.Dns.Core.MultiCast;
using EonaCat.Dns.Core.Records;
using EonaCat.Dns.Core.Servers;
using EonaCat.Dns.Database;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Helpers.Extensions;
using EonaCat.Json;
using EonaCat.Logger;
using EonaCat.Network;

#pragma warning disable CS1591

namespace EonaCat.Dns.Core;

public class Server
{
    private static EonaCatDnsConfig _config;
    private static readonly object TotalRequestsLock = new();
    private static readonly object TotalBlockedLock = new();
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

    private async Task ProcessQueryAsync(Message message, RemoteInfo remote)
    {
        var clientIp = remote.Address;
        var clientPort = remote.Port;
        var databaseClient = await DatabaseManager.GetOrAddClientAsync(clientIp).ConfigureAwait(false);

        if (databaseClient == null)
        {
            await Logger.LogAsync($"Query rejected for '{clientIp}' (no databaseClient)", ELogType.ERROR)
                .ConfigureAwait(false);
            return;
        }

        var names = string.Join(", ", message.Questions.Select(q => $"{q.Name} {q.Type}"));
        var clientName = string.IsNullOrEmpty(databaseClient.Name) ? string.Empty : $" [{databaseClient.Name}] ";

        if (!(message.Questions.FirstOrDefault() is Question question))
            // If we dont have a question to process return
        {
            return;
        }

        if (!question.IsRouterDomain && !question.IsArpa)
        {
            await Logger.LogAsync($"Query from {clientIp}:{clientPort}{clientName}{Environment.NewLine}{names}")
                .ConfigureAwait(false);
        }

        var blockingTasks = await Task.WhenAll(
            CheckIfClientIsBlockedAsync(message, remote, databaseClient, names),
            CheckIfQuestionIsBlockedAsync(message, remote, databaseClient),
            Task.FromResult(IgnoreArpaRequests(message)),
            Task.FromResult(IgnoreWpadRequests(message))
        ).ConfigureAwait(false);

        var isBlocked = blockingTasks.Any(b => b);

        if (isBlocked)
        {
            message.IsBlocked = true;
            await UpdateStatsForQueryAsync(message, remote, question.Name.ToString(), databaseClient)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            if (!message.HasAnswers)
            {
                var localMessage = await _ns.QueryAsync(message).ConfigureAwait(false);

                if (localMessage.HasAnswerRecords && !InWontCacheList(localMessage))
                {
                    message = localMessage;
                }

                if (!message.HasAnswers)
                {
                    if (question.Type == RecordType.Ptr && question.IsArpa)
                    {
                        if (!TryGetArpaRecord(question.Name, out var records))
                        {
                            await LogAndSendMessageAsync(message, remote, databaseClient).ConfigureAwait(false);
                            return;
                        }

                        message.Answers.AddRange(records);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(_config.RouterDomain))
                        {
                            StripRouterDomain(question);
                        }

                        var ptrRecords = GetAuthoritativePtrRecords();
                        var record = GetMatchingPtrRecord(ptrRecords, question);

                        if (record != null)
                        {
                            AddIpAddressAnswer(message, record);
                        }

                        if (!message.HasAnswers && !question.IsArpa && !question.IsRouterDomain)
                        {
                            message = await ResolveHelper.ResolveOverDohAsync(message).ConfigureAwait(false);
                            message = await ResolveHelper.ResolveOverDnsAsync(message).ConfigureAwait(false);

                            if (message != null && message.HasAnswers)
                            {
                                _ns.CacheAnswer(question.ToString(), message.Answers);
                            }
                        }
                    }
                }

                await UpdateStatsForQueryAsync(message, remote, question.Name.ToString(), databaseClient)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            await Logger.LogAsync(exception).ConfigureAwait(false);
        }

        await LogAndSendMessageAsync(message, remote, databaseClient).ConfigureAwait(false);
    }

    private bool TryGetArpaRecord(DomainName name, out IEnumerable<ResourceRecord> records)
    {
        var arpaRecord = _ns.Catalog.FirstOrDefault(x => x.Key == name);
        if (arpaRecord.Key != null)
        {
            records = arpaRecord.Value.Resources;
            return records != null;
        }

        records = null;
        return false;
    }

    private IEnumerable<PtrRecord> GetAuthoritativePtrRecords()
    {
        return _ns.Catalog.Values
            .Where(node => node.IsAuthoritative)
            .SelectMany(node => node.Resources.OfType<PtrRecord>());
    }


    private static void StripRouterDomain(Question question)
    {
        var routerDomain = _config.RouterDomain;
        var questionName = question.Name.ToString();
        if (!questionName.EndsWith(routerDomain))
        {
            return;
        }

        questionName = questionName.Remove(questionName.Length - routerDomain.Length);
        if (questionName.EndsWith('.'))
        {
            questionName = questionName.Substring(0, questionName.Length - 1);
        }

        if (!string.IsNullOrEmpty(questionName))
        {
            question.Name = questionName;
        }
    }

    private static PtrRecord GetMatchingPtrRecord(IEnumerable<PtrRecord> ptrRecords, Question question)
    {
        if (_config.PartialLookupName)
        {
            return ptrRecords.FirstOrDefault(x =>
                x.DomainName.ToString().ToLower().Contains(question.Name.ToString().ToLower()));
        }

        return ptrRecords.FirstOrDefault(x =>
            x.DomainName.ToString().ToLower().StartsWith(question.Name.ToString().ToLower()));
    }

    private static void AddIpAddressAnswer(Message message, PtrRecord record)
    {
        var reversedIpAddress = record.Name.ToString()
            .Replace($"in-addr{_config.ArpaPostFix}", string.Empty);

        var parts = reversedIpAddress.Split('.');
        parts = parts.Where(x => !string.IsNullOrEmpty(x)).ToArray();
        Array.Reverse(parts);
        reversedIpAddress = string.Join('.', parts);
        var ipAddress = IPAddress.Parse(reversedIpAddress);

        message.Header.AuthoritativeAnswer = AuthoritativeAnswer.Authoritative;
        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            message.Answers.Add(new AaaaRecord
            {
                Name = record.DomainName,
                Type = RecordType.Aaaa,
                Address = ipAddress,
                Class = RecordClass.Internet,
                TimeCreated = DateTime.Now,
                Ttl = record.Ttl
            });
        }
        else
        {
            message.Answers.Add(new ARecord
            {
                Name = record.DomainName,
                Type = RecordType.A,
                Address = ipAddress,
                Class = RecordClass.Internet,
                TimeCreated = DateTime.Now,
                Ttl = record.Ttl
            });
        }
    }


    private static bool InWontCacheList(Message localMessage)
    {
        return _config.DontCacheList.Any() &&
               localMessage.Questions.Exists(x => _config.DontCacheList.Contains(x.Name.ToString().ToLower()));
    }


    private async Task<bool> CheckIfClientIsBlockedAsync(Message message, RemoteInfo remote, Client databaseClient,
        string names)
    {
        if (databaseClient == null || !databaseClient.IsBlocked)
        {
            return false;
        }

        if (_config.LogBlockedClients)
        {
            await Logger.LogAsync(
                $"Received query from {remote.Address}:{remote.Port}{Environment.NewLine}for {names}, but the client is blocked, ignoring!",
                ELogType.WARNING).ConfigureAwait(false);
        }

        // Send back to client
        await SendToClientAsync(message, remote).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> CheckIfQuestionIsBlockedAsync(Message message, RemoteInfo client, Client databaseClient)
    {
        foreach (var question in message.Questions)
        {
            var questionUrl = question.Name.ToString();
            if (!await BlockList.MatchAsync(questionUrl).ConfigureAwait(false))
            {
                continue;
            }

            if (WatchMode)
            {
                await Logger.LogAsync(
                    $"Received query from {client.Address}:{client.Port}{Environment.NewLine}for {questionUrl}, but the domain is blocked, allowing because watchMode is turned on!",
                    ELogType.WARNING).ConfigureAwait(false);
                continue;
            }

            IncrementBlockedRequests();

            // Get the question from the database
            var domain = await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Url == questionUrl)
                .ConfigureAwait(false);
            if (domain == null || !IPAddress.TryParse(domain.ForwardIp, out var redirectionAddress))
            {
                continue;
            }

            message = message.CreateResponse();

            // Construct the answer with the redirectionAddress
            message.Answers.Add(new ARecord
            {
                Address = redirectionAddress,
                Class = question.Class,
                TimeCreated = question.TimeCreated,
                Name = question.Name,
                Ttl = TimeSpan.MaxValue,
                Type = question.Type
            });

            await SendToClientAsync(message, client).ConfigureAwait(false);

            await Logger.LogAsync(
                $"Received query from {client.Address}:{client.Port}{Environment.NewLine}for {questionUrl}, but the domain is blocked, ignoring!",
                ELogType.WARNING).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static bool IgnoreWpadRequests(Message message)
    {
        var question = message.Questions.FirstOrDefault();
        return question != null && _config.IgnoreWpadRequests && question.Name.ToString().StartsWith("wpad.");
    }

    private static bool IgnoreArpaRequests(Message message)
    {
        var question = message.Questions.FirstOrDefault();
        return question != null && _config.IgnoreArpaRequests && question.Name.ToString().Contains(".in-addr.arpa");
    }

    private async Task LogAndSendMessageAsync(Message message, RemoteInfo remote, Client databaseClient)
    {
        if (message == null)
        {
            return;
        }

        if (message.HasAnswers)
            // Send back to client
        {
            await SendToClientAsync(message, remote).ConfigureAwait(false);
        }

        foreach (var question in message.Questions)
        {
            await Logger.LogAsync(
                    message.IsFromCache
                        ? $"Cached response send to {remote.Address}:{remote.Port} for {question.Name}"
                        : $"{message.ResolveType} Response send to {remote.Address}:{remote.Port} for {question.Name}")
                .ConfigureAwait(false);

            await UpdateStatsForQueryAsync(message, remote, question.Name.ToString(), databaseClient)
                .ConfigureAwait(false);
        }
    }

    private static async Task UpdateStatsForQueryAsync(Message message, RemoteInfo remote, string questionUrl,
        Client databaseClient)
    {
        // Add the domain to the database and update the statistics
        await AddDomainToDatabaseAsync(questionUrl).ConfigureAwait(false);
        await LogQueryMessageAsync(message, remote, questionUrl, databaseClient).ConfigureAwait(false);
    }

    private static async Task LogQueryMessageAsync(Message message, RemoteInfo remote, string questionUrl,
        Client databaseClient)
    {
        if (databaseClient != null)
        {
            // Update stats
            var resultType = Log.ResultType.Success;
            if (databaseClient.IsBlocked || message.IsBlocked)
                // The request was Blocked
            {
                resultType = Log.ResultType.Blocked;
            }

            foreach (var log in message.Questions.Select(question => new Log
                     {
                         ClientIp = remote.Address.ToString(),
                         Request = questionUrl,
                         RecordType = question.Type,
                         ResponseCode = message.Header.ResponseCode,
                         IsBlocked = message.IsBlocked,
                         IsFromCache = message.IsFromCache,
                         Result = resultType
                     }))
            {
                if (_config.IncludeRawInLogTable)
                {
                    log.Raw = JsonHelper.ToJson(message, Formatting.None, new IpAddressConverter());
                }

                await DatabaseManager.Logs.InsertOrUpdateAsync(log).ConfigureAwait(false);
            }
        }
    }

    private static async Task AddDomainToDatabaseAsync(string questionUrl)
    {
        var existingDomain = false;
        if (!string.IsNullOrWhiteSpace(questionUrl))
        {
            existingDomain = await DatabaseManager.Domains.AnyAsync(x => x.Url == questionUrl).ConfigureAwait(false);
        }

        if (!existingDomain)
        {
            // Add the domain to the database
            var questionDomain = new Domain
            {
                Url = questionUrl,
                ForwardIp = Blocker.Setup != null
                    ? Blocker.Setup.RedirectionAddress
                    : ConstantsDns.DefaultRedirectionAddress
            };
            await DatabaseManager.Domains.InsertOrUpdateAsync(questionDomain).ConfigureAwait(false);
        }
    }

    private async Task SendToClientAsync(RecordBase record, RemoteInfo remoteIpEndpoint)
    {
        try
        {
            var bytes = record.ToByteArray();
            if (remoteIpEndpoint.IsIPv6)
            {
                if (remoteIpEndpoint.IsTcp)
                {
                    await _socketServerTcpV6.SendToAsync(remoteIpEndpoint.Socket, bytes).ConfigureAwait(false);
                }
                else
                {
                    await _socketServerUdpV6.SendToAsync(remoteIpEndpoint.EndPoint, bytes).ConfigureAwait(false);
                }
            }
            else
            {
                if (remoteIpEndpoint.IsTcp)
                {
                    await _socketServerTcpV4.SendToAsync(remoteIpEndpoint.Socket, bytes).ConfigureAwait(false);
                }
                else
                {
                    await _socketServerUdpV4.SendToAsync(remoteIpEndpoint.EndPoint, bytes).ConfigureAwait(false);
                }
            }

            await MessageHelper.PrintPacketDetails(bytes.Length, bytes, "Outgoing response").ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await Logger.LogAsync("Could not send response: " + exception.Message, ELogType.ERROR)
                .ConfigureAwait(false);
        }
    }


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
        var hasValidIpV4 = false;
        var hasValidIpV6 = false;
        var localIpAddresses = MultiCastService.GetLinkLocalAddresses();
        var ipAddresses = localIpAddresses.ToList();

        if (!string.IsNullOrWhiteSpace(_config.ListenAddressV4))
        {
            if (!ipAddresses.Contains(IPAddress.Parse(_config.ListenAddressV4)))
            {
                await Logger.LogAsync("Invalid ipV4 address specified in the configuration file", ELogType.WARNING)
                    .ConfigureAwait(false);
                Console.WriteLine("Valid values are: ");
                ipAddresses.Where(x => x.AddressFamily != AddressFamily.InterNetworkV6)
                    .ForEach(x => Console.WriteLine(x.ToString()));
            }
            else
            {
                hasValidIpV4 = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(_config.ListenAddressV6))
        {
            if (_config.ListenAddressV6 != "::1" && !ipAddresses.Contains(IPAddress.Parse(_config.ListenAddressV6)))
            {
                await Logger.LogAsync("Invalid ipV6 address specified in the configuration file", ELogType.WARNING)
                    .ConfigureAwait(false);
                Console.WriteLine("Valid values are: ");
                Console.WriteLine("::1");
                ipAddresses.Where(x => x.AddressFamily == AddressFamily.InterNetworkV6)
                    .ForEach(x => Console.WriteLine(x.ToString()));
            }
            else
            {
                hasValidIpV6 = true;
            }
        }

        if (hasValidIpV4 || hasValidIpV6)
        {
            await Task.Run(CreateDnsServerAsync).ConfigureAwait(false);
            await Task.Run(StartMultiCastService).ConfigureAwait(false);

            if (hasValidIpV4)
            {
                await Task.Run(StartServerV4).ConfigureAwait(false);
            }

            if (hasValidIpV6)
            {
                await Task.Run(StartServerV6).ConfigureAwait(false);
            }

            await OutputStartupMessage().ConfigureAwait(false);
            IsRunning = true;
        }
        else
        {
            await Logger.LogAsync(
                "There was no valid ipV4 address or ipV6 address found to bind to, please check configuration",
                ELogType.ERROR).ConfigureAwait(false);
        }
    }

    private async Task StartServerV6()
    {
        _socketServerUdpV6 = new SocketUdpServer(_config.ListenAddressV6, 53);
        _socketServerUdpV6.OnReceive += _socketServerUdpV6_OnReceive;
        _ = Task.Run(() => _socketServerUdpV6.StartAsync().ConfigureAwait(false));

        _socketServerTcpV6 = new SocketTcpServer(_config.ListenAddressV6, 53);
        _socketServerTcpV6.OnReceive += _socketServerTcpV6_OnReceive;
        _ = Task.Run(() => _socketServerTcpV6.StartAsync().ConfigureAwait(false));
        await Logger.LogAsync($"EonaCatDns started listening on {_config.ListenAddressV6}:53").ConfigureAwait(false);
    }

    private void _socketServerTcpV6_OnReceive(RemoteInfo remoteInfo, string nickName)
    {
        Task.Run(() => StartQueryAsync(remoteInfo).ConfigureAwait(false));
    }

    private void _socketServerUdpV6_OnReceive(RemoteInfo remoteInfo)
    {
        Task.Run(() => StartQueryAsync(remoteInfo).ConfigureAwait(false));
    }

    private async Task StartServerV4()
    {
        _socketServerUdpV4 = new SocketUdpServer(_config.ListenAddressV4, 53);
        _socketServerUdpV4.OnReceive += _socketServerUdpV4_OnReceive;
        _ = Task.Run(() => _socketServerUdpV4.StartAsync().ConfigureAwait(false));

        _socketServerTcpV4 = new SocketTcpServer(_config.ListenAddressV4, 53);
        _socketServerTcpV4.OnReceive += _socketServerTcpV4_OnReceive;
        _ = Task.Run(() => _socketServerTcpV4.StartAsync().ConfigureAwait(false));
        await Logger.LogAsync($"EonaCatDns started listening on {_config.ListenAddressV4}:53").ConfigureAwait(false);
    }

    private void _socketServerTcpV4_OnReceive(RemoteInfo remoteInfo, string nickName)
    {
        Task.Run(() => StartQueryAsync(remoteInfo).ConfigureAwait(false));
    }

    private void _socketServerUdpV4_OnReceive(RemoteInfo remoteInfo)
    {
        Task.Run(() => StartQueryAsync(remoteInfo).ConfigureAwait(false));
    }

    private static void IncrementTotalRequests()
    {
        lock (TotalRequestsLock)
        {
            TotalRequests++;
        }
    }

    private static void IncrementBlockedRequests()
    {
        lock (TotalBlockedLock)
        {
            TotalBlocked++;
        }
    }

    private async Task StartQueryAsync(RemoteInfo remoteInfo)
    {
        try
        {
            if (!(remoteInfo.EndPoint is IPEndPoint))
            {
                return;
            }

            // Check for test-client
            // Check if device-only request
            if (MessageHelper.TestDeviceOnly(remoteInfo.Address))
            {
                return;
            }

            if (remoteInfo.Data.Length < DnsHeader.HeaderSize)
                // Invalid package
            {
                return;
            }

            var data = remoteInfo.Data;
            var message = await MessageHelper.GetMessageFromBytes(data).ConfigureAwait(false);

            if (!message.Questions.Any())
            {
                // We need to have at least 1 question
                await MessageHelper.PrintPacketDetails(data.Length, data, "Invalid record", true, true)
                    .ConfigureAwait(false);
                return;
            }

            await MessageHelper.PrintPacketDetails(data.Length, data, $"Incoming request {message.OperationCode}")
                .ConfigureAwait(false);

            // Process the message
            switch (message.OperationCode)
            {
                case OperationCode.Query:
                    await Task.Run(() => { ProcessQueryAsync(message, remoteInfo).ConfigureAwait(false); });
                    IncrementTotalRequests();
                    break;

                case OperationCode.InverseQuery:
                    await Logger.LogAsync("InverseQuery ignored").ConfigureAwait(false);
                    break;

                case OperationCode.Status:
                    await Logger.LogAsync("Status ignored").ConfigureAwait(false);
                    break;

                case OperationCode.Notify:
                    await Logger.LogAsync("Notify ignored").ConfigureAwait(false);
                    break;

                case OperationCode.Update:
                    await Logger.LogAsync("Update ignored").ConfigureAwait(false);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            await Logger.LogAsync(ex, writeToConsole: false).ConfigureAwait(false);
        }
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
            if (_ns.Catalog.ToList().Any(x => x.Value.Name == clientIp.GetArpaName()))
            {
                return;
            }
        }

        if (e.Name.EndsWith(".local"))
        {
            e.Name = e.Name.Remove(e.Name.LastIndexOf(".local", StringComparison.Ordinal));
        }

        ARecord aRecord = null;
        AaaaRecord aaaRecord = null;

        var ipv4 = clientIp.AddressFamily == AddressFamily.InterNetwork;

        if (ipv4)
        {
            aRecord = new ARecord
            {
                Name = e.Name,
                Type = clientIp.AddressFamily == AddressFamily.InterNetwork ? RecordType.A : RecordType.Aaaa,
                Class = RecordClass.Internet,
                Address = clientIp
            };
        }
        else
        {
            aaaRecord = new AaaaRecord
            {
                Name = e.Name,
                Type = clientIp.AddressFamily == AddressFamily.InterNetwork ? RecordType.A : RecordType.Aaaa,
                Class = RecordClass.Internet,
                Address = clientIp
            };
        }

        lock (_ns.Catalog)
        {
            try
            {
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
        }

        if (ipv4)
        {
            _ns.Catalog.AddReverseLookupRecord(aRecord);
        }
        else
        {
            _ns.Catalog.AddReverseLookupRecord(aaaRecord);
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