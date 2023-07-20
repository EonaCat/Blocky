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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
#pragma warning disable CS1591

namespace EonaCat.Dns.Core;

public class Server
{
    private static EonaCatDnsConfig _config;
    private Ns _ns;
    private SocketUdpServer _socketServerUdpV4;
    private SocketTcpServer _socketServerTcpV4;
    private SocketUdpServer _socketServerUdpV6;
    private SocketTcpServer _socketServerTcpV6;

    public static long TotalRequests { get; set; }
    private static readonly object TotalRequestsLock = new object();
    private static readonly object TotalBlockedLock = new object();
    public static long TotalBlocked { get; set; }
    public bool IsRunning { get; private set; }

    private async Task ProcessQueryAsync(Message message, RemoteInfo remote)
    {
        var clientIp = remote.Address;
        var clientPort = remote.Port;
        var databaseClient = await DatabaseManager.GetOrAddClientAsync(clientIp).ConfigureAwait(false);

        if (databaseClient == null)
        {
            Logger.Log($"Query rejected for '{clientIp}' (no databaseClient)", ELogType.ERROR);
            return;
        }

        var names = string.Join(", ", message.Questions.Select(currentQuestion => $"{currentQuestion.Name} {currentQuestion.Type}"));
        var clientName = !string.IsNullOrEmpty(databaseClient.Name) ? $" [{databaseClient.Name}] " : string.Empty;

        if (message.Questions.FirstOrDefault() is not Question question)
        {
            return;
        }

        if (!question.IsRouterDomain && !question.IsArpa)
        {
            Logger.Log($"Query from {clientIp}:{clientPort} {clientName} {Environment.NewLine}{names}");
        }

        var blockingTasks = new Task<bool>[]
        {
        CheckIfClientIsBlockedAsync(message, remote, databaseClient, names),
        CheckIfQuestionIsBlockedAsync(message, remote, databaseClient),
        IgnoreArpaRequests(message) ? Task.FromResult(true) : Task.FromResult(false),
        IgnoreWpadRequests(message) ? Task.FromResult(true) : Task.FromResult(false)
        };

        var isBlockedResults = await Task.WhenAll(blockingTasks).ConfigureAwait(false);
        var isClientBlocked = isBlockedResults[0];
        var isQuestionBlocked = isBlockedResults[1];
        var ignoreArpa = isBlockedResults[2];
        var ignoreWpad = isBlockedResults[3];

        if (isClientBlocked || isQuestionBlocked || ignoreArpa || ignoreWpad)
        {
            message.IsBlocked = true;
            await UpdateStatsForQueryAsync(message, remote, question.Name.ToString(), databaseClient).ConfigureAwait(false);
            return;
        }

        try
        {
            if (!message.HasAnswers)
            {
                var originalMessage = message;
                var localMessageTask = await _ns.QueryAsync(message).ConfigureAwait(false);

                if (localMessageTask.HasAnswerRecords && !InWontCacheList(localMessageTask))
                {
                    message = localMessageTask;
                }

                if (!message.HasAnswers)
                {
                    if (question.Type == RecordType.Ptr && question.IsArpa)
                    {
                        if (TryGetArpaRecord(question.Name, out var records))
                        {
                            message.Answers.AddRange(records);
                        }
                        else
                        {
                            await LogAndSendMessageAsync(message, remote, databaseClient).ConfigureAwait(false);
                            return;
                        }
                    }

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

                if (message == null)
                {
                    await SendToClientAsync(originalMessage, remote).ConfigureAwait(false);
                    await UpdateStatsForQueryAsync(originalMessage, remote, question.Name.ToString(), databaseClient).ConfigureAwait(false);
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            Logger.Log(exception);
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
        if (!questionName.EndsWith(routerDomain)) return;

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
            message.Answers.Add(new AaaaRecord()
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
        return _config.DontCacheList.Any() && localMessage.Questions.Exists(x => _config.DontCacheList.Contains(x.Name.ToString().ToLower()));
    }


    private async Task<bool> CheckIfClientIsBlockedAsync(Message message, RemoteInfo remote, Client databaseClient, string names)
    {
        if (databaseClient == null || !databaseClient.IsBlocked)
        {
            return false;
        }

        if (_config.LogBlockedClients)
        {
            Logger.Log($"{message.Header.Id} Received query from {remote.Address}:{remote.Port}{Environment.NewLine}for {names}, but the client is blocked, ignoring!", ELogType.WARNING);
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
                Logger.Log($"{message.Header.Id} Received query from {client.Address}:{client.Port}{Environment.NewLine}for {questionUrl}, but the domain is blocked, allowing because watchMode is turned on!",
                    ELogType.WARNING);
                continue;
            }

            IncrementBlockedRequests();

            // Get the question from the database
            var domain = await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Url == questionUrl).ConfigureAwait(false);
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
                Type = question.Type,
            });

            await SendToClientAsync(message, client).ConfigureAwait(false);

            Logger.Log($"{message.Header.Id} Received query from {client.Address}:{client.Port}{Environment.NewLine}for {questionUrl}, but the domain is blocked, ignoring!",
                ELogType.WARNING);
            return true;
        }

        return false;
    }

    public static bool WatchMode { get; set; }

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
        {
            // Send back to client
            await SendToClientAsync(message, remote).ConfigureAwait(false);
        }

        foreach (var question in message.Questions)
        {
            Logger.Log(
                message.IsFromCache
                    ? $"{message.Header.Id} Cached response send to {remote.Address}:{remote.Port} for {question.Name}"
                    : $"{message.Header.Id} {message.ResolveType} Response send to {remote.Address}:{remote.Port} for {question.Name}");

            await UpdateStatsForQueryAsync(message, remote, question.Name.ToString(), databaseClient).ConfigureAwait(false);
        }
    }

    public string MasterFile => "masterFile.txt";

    private async Task UpdateStatsForQueryAsync(Message message, RemoteInfo remote, string questionUrl, Client databaseClient)
    {
        // Add the domain to the database and update the statistics
        await AddDomainToDatabaseAsync(questionUrl).ConfigureAwait(false);
        await LogQueryMessageAsync(message, remote, questionUrl, databaseClient).ConfigureAwait(false);
    }

    private static async Task LogQueryMessageAsync(Message message, RemoteInfo remote, string questionUrl, Client databaseClient)
    {
        if (databaseClient != null)
        {
            // Update stats
            var resultType = Log.ResultType.Success;
            if (databaseClient.IsBlocked || message.IsBlocked)
            {
                // The request was Blocked
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
                    log.Raw = JsonHelper.ToJson(message, Formatting.None, converters: new IpAddressConverter());
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
                    : ConstantsDns.DefaultRedirectionAddress,
            };
            await DatabaseManager.Domains.InsertOrUpdateAsync(questionDomain).ConfigureAwait(false);
        }
    }

    private async Task SendToClientAsync(Message message, RemoteInfo remoteIpEndpoint)
    {
        try
        {

            var bytes = message.ToByteArray();
            if (remoteIpEndpoint.IsIpv6)
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

            MessageHelper.PrintPacketDetails(bytes.Length, bytes, "Outgoing response");
        }
        catch (Exception exception)
        {
            Logger.Log("Could not send response: " + exception.Message, ELogType.ERROR);
        }
    }


    public Server WithConfig(EonaCatDnsConfig serverConfig)
    {
        _config = serverConfig;
        ResolveHelper.Config = _config;
        return this;
    }

    public Task RestartAsync()
    {
        Stop();
        return StartAsync();
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
                Logger.Log("Invalid ipV4 address specified in the configuration file", ELogType.WARNING);
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
                Logger.Log("Invalid ipV6 address specified in the configuration file", ELogType.WARNING);
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
            await CreateDnsServerAsync().ConfigureAwait(false);
            StartMultiCastService();

            if (hasValidIpV4)
            {
                StartServerV4();
            }

            if (hasValidIpV6)
            {
                StartServerV6();
            }

            OutputStartupMessage();
            IsRunning = true;
        }
        else
        {
            Logger.Log("There was no valid ipV4 address or ipV6 address found to bind to, please check configuration", ELogType.ERROR);
        }
    }

    private void StartServerV6()
    {
        _socketServerUdpV6 = new SocketUdpServer(_config.ListenAddressV6, 53);
        _socketServerUdpV6.OnReceive += SocketServer_OnReceived;
        _ = Task.Run(() => _socketServerUdpV6.StartAsync().ConfigureAwait(false));

        _socketServerTcpV6 = new SocketTcpServer(_config.ListenAddressV6, 53);
        _socketServerTcpV6.OnReceive += SocketServer_OnReceived;
        _ = Task.Run(() => _socketServerTcpV6.StartAsync().ConfigureAwait(false));
        Logger.Log($"EonaCatDns started listening on {_config.ListenAddressV6}:53");
    }

    private void StartServerV4()
    {
        _socketServerUdpV4 = new SocketUdpServer(_config.ListenAddressV4, 53);
        _socketServerUdpV4.OnReceive += SocketServer_OnReceived;
        _ = Task.Run(() => _socketServerUdpV4.StartAsync().ConfigureAwait(false));

        _socketServerTcpV4 = new SocketTcpServer(_config.ListenAddressV4, 53);
        _socketServerTcpV4.OnReceive += SocketServer_OnReceived;
        _ = Task.Run(() => _socketServerTcpV4.StartAsync().ConfigureAwait(false));
        Logger.Log($"EonaCatDns started listening on {_config.ListenAddressV4}:53");
    }

    private void SocketServer_OnReceived(RemoteInfo remoteInfo)
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
            {
                // Invalid package
                return;
            }

            var data = remoteInfo.Data;
            var message = MessageHelper.GetMessageFromBytes(data);

            if (!message.Questions.Any())
            {
                // We need to have at least 1 question
                MessageHelper.PrintPacketDetails(data.Length, data, "Invalid record", true, true);
                return;
            }

            MessageHelper.PrintPacketDetails(data.Length, data, $"Incoming request {message.OperationCode}");

            // Process the message
            switch (message.OperationCode)
            {
                case OperationCode.Query:
                    await ProcessQueryAsync(message, remoteInfo).ConfigureAwait(false);
                    IncrementTotalRequests();
                    break;

                case OperationCode.InverseQuery:
                    Logger.Log("InverseQuery ignored");
                    break;

                case OperationCode.Status:
                    Logger.Log("Status ignored");
                    break;

                case OperationCode.Notify:
                    Logger.Log("Notify ignored");
                    break;

                case OperationCode.Update:
                    Logger.Log("Update ignored");
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex, writeToConsole: false);
        }
    }


    private static void StartMultiCastService()
    {
        if (!_config.IsMultiCastEnabled)
        {
            return;
        }

        if (MultiCastDnsHelper.IsMultiCastEnabled)
        {
            return;
        }

        Task.Run(() => MultiCastDnsHelper.StartAsync(_config.LogLevel, _config.LogInLocalTime));
        Logger.Log("MultiCast service started");
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
            Logger.Log($"Could not load MasterFile {exception.Message}");
        }

        _ns = new Ns
        {
            IsCacheDisabled = _config.IsCacheDisabled
        };

        _ns.Catalog = Catalog.GenerateCatalog(masterFileContents);
        DatabaseManager.OnHostNameResolve -= DatabaseManager_OnHostNameResolve;
        DatabaseManager.OnHostNameResolve += DatabaseManager_OnHostNameResolve;
    }

    private void DatabaseManager_OnHostNameResolve(object sender, Client e)
    {
        if (!IPAddress.TryParse(e.Ip, out var clientIp))
        {
            return;
        }

        if (_ns.Catalog.Any(x => x.Value.Name == clientIp.GetArpaName()))
        {
            return;
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

        if (_ns.Catalog.Any(x => x.Value.Name == clientIp.GetArpaName()))
        {
            return;
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

    private static void OutputStartupMessage()
    {
        var cacheDisabledMessage = _config.IsCacheDisabled ? "(Caching disabled)" : string.Empty;
        var resolveType = _config.ResolveOverDoh ? "(Resolving queries over DoH) " : string.Empty;
        var multiCastMessage = _config.IsMultiCastEnabled ? "(MultiCast enabled) " : string.Empty;
        Logger.Log($"{DllInfo.Name} started {cacheDisabledMessage} {resolveType} {multiCastMessage}");
    }

    public void Stop()
    {
        StopMultiCastService();
        Logger.Log($"{DllInfo.Name} stopped");
        IsRunning = false;
    }

    private static void StopMultiCastService()
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
        Logger.Log("MultiCast service stopped");
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

    public static Task NsLookupAsync(string[] args, bool resolveOverDoh = true, bool appExitAfterCommand = true, bool onlyExitAfterKeyPress = true)
    {
        return NsLookupHelper.ResolveAsync(args, resolveOverDoh, appExitAfterCommand, onlyExitAfterKeyPress);
    }
}