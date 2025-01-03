﻿/*
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
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using EonaCat.Logger;

namespace EonaCat.Dns.Core.MultiCast;

internal class MultiCastService : IDisposable
{
    public const int PacketOverhead = 48;
    private const int MaxSize = 65507;
    private static readonly TimeSpan LastMaxLegacyUniCastTtl = TimeSpan.FromSeconds(10);
    private readonly Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> _networkInterfacesFilter;
    private readonly RecentMessages _receivedMessages = new();

    private readonly RecentMessages _sentMessages = new();
    private readonly UdpClient _unicastClientIp4 = new(AddressFamily.InterNetwork);
    private readonly UdpClient _unicastClientIp6 = new(AddressFamily.InterNetworkV6);
    private MultiCastClient _client;

    private List<NetworkInterface> _knownNetworkInterfaces = new();
    private int _maxPacketSize;

    static MultiCastService()
    {
        RecordBase.TTLDefaultRecords = TimeSpan.FromMinutes(75);
        RecordBase.TTLDefaultHosts = TimeSpan.FromSeconds(120);
    }

    internal MultiCastService(Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> filter = null)
    {
        _networkInterfacesFilter = filter;
        UseIpv4 = Socket.OSSupportsIPv4;
        UseIpv6 = Socket.OSSupportsIPv6;
        IgnoreDuplicateMessages = true;
    }

    public bool UseIpv4 { get; set; }
    public bool UseIpv6 { get; set; }
    public bool IgnoreDuplicateMessages { get; set; }

    public bool IsRunning { get; private set; }

    public void Dispose()
    {
        Dispose(true);
    }

    public event EventHandler<MessageEventArgs> QueryReceived;
    public event EventHandler<MessageEventArgs> AnswerReceived;
    public event EventHandler<byte[]> MalformedMessage;
    public event EventHandler<NetworkInterfaceEventArgs> NetworkInterfaceDiscovered;

    public static IEnumerable<NetworkInterface> GetNetworkInterfaces()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic =>
                nic.OperationalStatus == OperationalStatus.Up &&
                !(nic is { NetworkInterfaceType: NetworkInterfaceType.Loopback }));
    }

    public static IEnumerable<IPAddress> GetIpAddresses()
    {
        return GetNetworkInterfaces()
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .Select(u => u.Address);
    }

    public static IEnumerable<IPAddress> GetLinkLocalAddresses()
    {
        return GetIpAddresses()
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork ||
                        (a.AddressFamily == AddressFamily.InterNetworkV6 && a.IsIPv6LinkLocal));
    }

    public static IEnumerable<IPAddress> GetAllIPAddresses()
    {
        var addresses = new List<IPAddress>();

        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                         ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        foreach (var networkInterface in networkInterfaces)
        {
            var ipProperties = networkInterface.GetIPProperties();

            var unicastAddresses = ipProperties.UnicastAddresses
                .Select(ua => ua.Address)
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork || // IPv4
                             ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6); // IPv6

            addresses.AddRange(unicastAddresses);
        }

        return addresses;
    }

    internal async Task StartAsync()
    {
        _maxPacketSize = MaxSize - PacketOverhead;
        _knownNetworkInterfaces.Clear();
        await FindNetworkInterfaces().ConfigureAwait(false);
        IsRunning = true;
    }

    internal void Stop()
    {
        QueryReceived = null;
        AnswerReceived = null;
        NetworkInterfaceDiscovered = null;
        _client?.Dispose();
        _client = null;
        IsRunning = false;
    }

    private async void OnNetworkAddressChangedAsync(object sender, EventArgs e)
    {
        await FindNetworkInterfaces().ConfigureAwait(false);
    }

    private async Task FindNetworkInterfaces()
    {
        await LoggerMultiCast.LogAsync("Finding network interfaces").ConfigureAwait(false);

        try
        {
            var currentNics = GetNetworkInterfaces().ToList();
            var newNics = _knownNetworkInterfaces.Where(k => currentNics.All(n => k.Id != n.Id)).ToList();
            var oldNics = currentNics.Where(nic => _knownNetworkInterfaces.All(k => k.Id != nic.Id)).ToList();

            foreach (var nic in oldNics)
                await LoggerMultiCast.LogAsync($"Removed nic '{nic.Name}'.").ConfigureAwait(false);

            foreach (var nic in newNics)
                await LoggerMultiCast.LogAsync($"Found nic '{nic.Name}'.").ConfigureAwait(false);

            _knownNetworkInterfaces = currentNics;

            if (newNics.Any() || oldNics.Any())
            {
                _client?.Dispose();
                _client = new MultiCastClient(UseIpv4, UseIpv6,
                    _networkInterfacesFilter?.Invoke(_knownNetworkInterfaces) ?? _knownNetworkInterfaces);
                _client.MessageReceived += OnDnsMessage;
            }

            if (newNics.Any())
            {
                NetworkInterfaceDiscovered?.Invoke(this, new NetworkInterfaceEventArgs
                {
                    NetworkInterfaces = newNics
                });
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChangedAsync;
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChangedAsync;
            }
        }
        catch (Exception e)
        {
            await LoggerMultiCast.LogAsync(e, "Nic resolving failed").ConfigureAwait(false);
        }
    }

    public Task SendQueryAsync(DomainName name, RecordClass klass = RecordClass.Internet,
        RecordType type = RecordType.Any)
    {
        var message = new Message
        {
            Header = new DnsHeader
            {
                MessageType = MessageType.Query
            },
            OperationCode = OperationCode.Query
        };
        message.Questions.Add(new Question
        {
            Name = name,
            Class = klass,
            Type = type
        });
        message.IsReceivedViaMultiCast = true;
        return SendQueryAsync(message);
    }

    public Task SendUnicastQueryAsync(DomainName name, RecordClass klass = RecordClass.Internet,
        RecordType type = RecordType.Any)
    {
        var message = new Message
        {
            Header = new DnsHeader
            {
                MessageType = MessageType.Query
            },
            OperationCode = OperationCode.Query
        };
        message.Questions.Add(new Question
        {
            Name = name,
            Class = (RecordClass)((ushort)klass | 0x8000),
            Type = type
        });

        return SendQueryAsync(message);
    }

    public Task SendQueryAsync(Message msg)
    {
        return SendAsync(msg, false);
    }

    public Task SendAnswerAsync(Message answer, bool checkDuplicate = true)
    {
        answer.Header.AuthoritativeAnswer = AuthoritativeAnswer.Authoritative;
        answer.Header.Id = 0;
        answer.Questions.Clear();
        answer.Truncate(_maxPacketSize);
        return SendAsync(answer, checkDuplicate);
    }

    public async Task SendAnswerAsync(Message answer, MessageEventArgs query, bool checkDuplicate = true)
    {
        if (!query.IsLegacyUnicast)
        {
            await SendAsync(answer, checkDuplicate).ConfigureAwait(false);
            return;
        }

        answer.Header.AuthoritativeAnswer = AuthoritativeAnswer.Authoritative;
        answer.Header.Id = query.Message.Header.Id;
        answer.Questions.Clear();
        answer.Questions.AddRange(query.Message.Questions);
        answer.Truncate(_maxPacketSize);

        foreach (var resourceRecord in answer.Answers.Concat(answer.AdditionalRecords))
            resourceRecord.Ttl = resourceRecord.Ttl > LastMaxLegacyUniCastTtl
                ? LastMaxLegacyUniCastTtl
                : resourceRecord.Ttl;

        await SendAsync(answer, checkDuplicate, query.RemoteEndPoint).ConfigureAwait(false);
    }

    private async Task SendAsync(Message message, bool checkDuplicate, IPEndPoint remoteEndPoint = null)
    {
        var packet = message.ToByteArray();
        if (packet.Length > _maxPacketSize)
        {
            throw new ArgumentOutOfRangeException($"Exceeds max packet size of {_maxPacketSize}.");
        }

        if (checkDuplicate && !_sentMessages.TryAdd(packet))
        {
            return;
        }

        if (remoteEndPoint == null)
        {
            await _client.SendAsync(packet).ConfigureAwait(false);
        }
        else
        {
            var uniCastClient = remoteEndPoint.Address.AddressFamily == AddressFamily.InterNetwork
                ? _unicastClientIp4
                : _unicastClientIp6;
            await uniCastClient.SendAsync(packet, packet.Length, remoteEndPoint).ConfigureAwait(false);
        }
    }

    public async void OnDnsMessage(object sender, UdpReceiveResult result)
    {
        if (IgnoreDuplicateMessages && !_receivedMessages.TryAdd(result.Buffer))
        {
            return;
        }

        var message = new Message
        {
            IsReceivedViaMultiCast = true
        };
        try
        {
            await message.Read(result.Buffer, 0, result.Buffer.Length).ConfigureAwait(false);
        }
        catch (Exception)
        {
            message.HasPacketError = true;
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Corrupted package received:");

            stringBuilder.AppendLine("=======");
            stringBuilder.AppendLine("HEADER:");
            stringBuilder.AppendLine("=======");
            stringBuilder.AppendLine(message.Header.ToString());

            await LoggerMultiCast.LogAsync(stringBuilder.ToString(), ELogType.ERROR).ConfigureAwait(false);
            MalformedMessage?.Invoke(this, result.Buffer);
            return;
        }

        if (message.OperationCode != OperationCode.Query || message.Header.ResponseCode != ResponseCode.NoError)
        {
            return;
        }

        try
        {
            if (message.Header.IsQuery && message.Questions.Count > 0)
            {
                QueryReceived?.Invoke(this,
                    new MessageEventArgs { Message = message, RemoteEndPoint = result.RemoteEndPoint });
            }
            else if (message.Header.IsResponse && message.Answers.Count > 0)
            {
                AnswerReceived?.Invoke(this,
                    new MessageEventArgs { Message = message, RemoteEndPoint = result.RemoteEndPoint });
            }
        }
        catch (Exception e)
        {
            await LoggerMultiCast.LogAsync(e, "Receive handler failed").ConfigureAwait(false);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
        }
    }
}