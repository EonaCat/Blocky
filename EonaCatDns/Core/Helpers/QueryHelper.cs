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

using EonaCat.Dns.Database;
using EonaCat.Logger;
using EonaCat.Network;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using EonaCat.Dns.Core.Records;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Converters;
using EonaCat.Dns.Core.Servers;
using EonaCat.Json;
using System.Text;

namespace EonaCat.Dns.Core.Helpers
{
    internal static class QueryHelper
    {
        private static EonaCatDnsConfig _config;
        internal static async Task ProcessQueryAsync(Message message, RemoteInfo remote)
        {
            if (_config == null)
            {
                await Logger.LogAsync("Cannot process query, the configuration is NULL", ELogType.ERROR).ConfigureAwait(false);
                return;
            }

            if (_nameServer == null)
            {
                await Logger.LogAsync("Cannot process query, the nameServer is NULL", ELogType.ERROR).ConfigureAwait(false);
                return;
            }

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

            if (isBlocked && !Server.WatchMode)
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
                    var localMessage = await _nameServer.QueryAsync(message).ConfigureAwait(false);

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
                                    _nameServer.CacheAnswer(question.ToString(), message.Answers);
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

        private static bool TryGetArpaRecord(DomainName name, out IEnumerable<ResourceRecord> records)
        {
            var arpaRecord = _nameServer.Catalog.FirstOrDefault(x => x.Key == name);
            if (arpaRecord.Key != null)
            {
                records = arpaRecord.Value.Resources;
                return records != null;
            }

            records = null;
            return false;
        }

        private static IEnumerable<PtrRecord> GetAuthoritativePtrRecords()
        {
            return _nameServer.Catalog.Values
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


        private static async Task<bool> CheckIfClientIsBlockedAsync(Message message, RemoteInfo remote, Client databaseClient,
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

        private static async Task<bool> CheckIfQuestionIsBlockedAsync(Message message, RemoteInfo client, Client databaseClient)
        {
            foreach (var question in message.Questions)
            {
                var questionUrl = question.Name.ToString();
                if (!await BlockList.MatchAsync(questionUrl).ConfigureAwait(false))
                {
                    continue;
                }

                if (Server.WatchMode)
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
                    Ttl = TimeSpan.Zero,
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

        static readonly object TotalBlockedLock = new();
        private static SocketTcpServer _socketServerTcpV4;
        private static SocketUdpServer _socketServerUdpV4;
        private static SocketTcpServer _socketServerTcpV6;
        private static SocketUdpServer _socketServerUdpV6;
        private static Ns _nameServer;

        private static void IncrementBlockedRequests()
        {
            lock (TotalBlockedLock)
            {
               Server.TotalBlocked++;
            }
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

        private static async Task SendToClientAsync(RecordBase record, RemoteInfo remoteIpEndpoint)
        {
            try
            {
                var bytes = record.ToByteArray();
                if (remoteIpEndpoint.IsIPv6)
                {
                    if (remoteIpEndpoint.IsTcp)
                    {
                        if (_socketServerTcpV6 != null)
                        {
                            await _socketServerTcpV6.SendToAsync(remoteIpEndpoint.Socket, bytes).ConfigureAwait(false);
                        }
                        else
                        {
                            await Logger.LogAsync("Cannot send message via TCPv6, the socketServer is not set!", ELogType.WARNING).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        if (_socketServerUdpV6 != null)
                        {
                            await _socketServerUdpV6.SendToAsync(remoteIpEndpoint.EndPoint, bytes)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            await Logger.LogAsync("Cannot send message via UDPv6, the socketServer is not set!", ELogType.WARNING).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    if (remoteIpEndpoint.IsTcp)
                    {
                        if (_socketServerTcpV4 != null)
                        {
                            await _socketServerTcpV4.SendToAsync(remoteIpEndpoint.Socket, bytes).ConfigureAwait(false);
                        }
                        else
                        {
                            await Logger.LogAsync("Cannot send message via TCPv4, the socketServer is not set!", ELogType.WARNING).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        if (_socketServerUdpV4 != null)
                        {
                            await _socketServerUdpV4.SendToAsync(remoteIpEndpoint.EndPoint, bytes)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            await Logger.LogAsync("Cannot send message via UDPv4, the socketServer is not set!", ELogType.WARNING).ConfigureAwait(false);
                        }
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

        private static async Task LogAndSendMessageAsync(Message message, RemoteInfo remote, Client databaseClient)
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
                StringBuilder stringBuilder = new StringBuilder();
                var blocked = message.IsBlocked ? "blocked " : string.Empty;
                if (message.IsFromCache)
                {
                    stringBuilder.AppendLine($"Cached {blocked}response send to {remote.Address}:{remote.Port} for {question.Name}");
                }
                else
                {
                    stringBuilder.AppendLine($"{message.ResolveType} {blocked}response send to {remote.Address}:{remote.Port} for {question.Name}");
                }

                await Logger.LogAsync(stringBuilder.ToString()).ConfigureAwait(false);
                await UpdateStatsForQueryAsync(message, remote, question.Name.ToString(), databaseClient).ConfigureAwait(false);
            }
        }

        internal static void Configure(EonaCatDnsConfig config, Ns ns, SocketTcpServer socketServerTcpV4, SocketUdpServer socketServerUdpV4, SocketTcpServer socketServerTcpV6, SocketUdpServer socketServerUdpV6)
        {
            _config = config;
            _nameServer = ns;
            _socketServerTcpV4 = socketServerTcpV4;
            _socketServerTcpV6 = socketServerTcpV6;
            _socketServerUdpV4 = socketServerUdpV4;
            _socketServerUdpV6 = socketServerUdpV6;
        }
    }
}
