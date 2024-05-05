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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EonaCat.Helpers.Retry;
using EonaCat.Logger;

namespace EonaCat.Dns.Core.Clients;

internal class DnsClient : DnsClientBase
{
    private const int DnsPort = 53;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly TcpClient _tcpClient = new(AddressFamily.InterNetworkV6);
    private IEnumerable<IPAddress> _servers;

    public static TimeSpan TimeoutUdp { get; set; } = TimeSpan.FromSeconds(5);
    public static TimeSpan TimeoutTcp { get; set; } = TimeSpan.FromSeconds(5);

    public IEnumerable<IPAddress> Servers
    {
        get => _servers ?? GetServers();
        set => _servers = value;
    }

    public IEnumerable<IPAddress> AvailableServers()
    {
        return Servers.Where(a =>
            (Socket.OSSupportsIPv4 && a.AddressFamily == AddressFamily.InterNetwork) ||
            (Socket.OSSupportsIPv6 && a.AddressFamily == AddressFamily.InterNetworkV6));
    }

    public IEnumerable<IPAddress> GetServers()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up)
            .Where(x => x.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(x => x.GetIPProperties().DnsAddresses);
    }

    public override async Task<Message> QueryAsync(Message request, CancellationToken cancel = default)
    {
        var localDnsServers = GetLocalDnsAddresses();
        if (localDnsServers.Length == 0)
        {
            throw new Exception("EonaCatDns: No local Dns servers configured.");
        }

        return await GetResponseFromDnsAsync(request, cancel, localDnsServers).ConfigureAwait(false);
    }

    private async Task<Message> GetResponseFromDnsAsync(Message request, CancellationToken cancel,
        IEnumerable<IPAddress> localDnsServers)
    {
        var forwarder = string.Empty;

        if (!request.Questions.Any())
        {
            throw new Exception("EonaCatDns: Request must have at least 1 question");
        }

        Message response = null;
        foreach (var server in localDnsServers)
            try
            {
                response = await QueryAsync(request, server, cancel).ConfigureAwait(false);
                if (response?.HasAnswers == true)
                {
                    forwarder = server.ToString();
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                // Do nothing
            }
            catch (Exception ex)
            {
                await Logger.LogAsync(ex, $"Could not resolve Dns using {server}", false);
            }

        foreach (var question in request.Questions)
        {
            if (response == null)
            {
                break;
            }

            if (response.Header.ResponseCode != ResponseCode.NoError)
            {
                await Logger.LogAsync($"Dns error received for '{question.Name} '{response.Header.ResponseCode}'.",
                    ELogType.WARNING);
            }
            else if (response.HasAnswers)
            {
                await Logger.LogAsync($"Got Dns response for {question.Name} from {forwarder}", ELogType.DEBUG, false);
            }
        }

        return response;
    }

    private IPAddress[] GetLocalDnsAddresses()
    {
        var servers = AvailableServers().OrderBy(ipAddress => ipAddress.AddressFamily).ToArray();
        return servers;
    }

    private async Task<Message> QueryAsync(Message request, IPAddress server, CancellationToken cancel)
    {
        RetryHelper.Instance.DefaultMaxTryCount = 3;
        RetryHelper.Instance.DefaultMaxTryTime = TimeSpan.FromSeconds(10);
        RetryHelper.Instance.DefaultTryInterval = TimeSpan.FromMilliseconds(100);
        var response = new Message();

        try
        {
            response = await QueryUdpAsync(request, server, cancel).ConfigureAwait(false) ?? request;
            if (response.Header.IsResponse && !response.Header.IsTruncated)
            {
                return response;
            }
        }
        catch (SocketException e)
        {
            await Logger.LogAsync(e.Message, ELogType.WARNING);
        }
        catch (TaskCanceledException e)
        {
            await Logger.LogAsync(e.Message, ELogType.WARNING);
        }

        try
        {
            return await QueryTcpAsync(response, server, cancel).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            await Logger.LogAsync(e.Message, ELogType.WARNING);
        }

        return response;
    }

    private async Task<Message> QueryUdpAsync(Message request, IPAddress server, CancellationToken cancel)
    {
        var endPoint = new IPEndPoint(server, DnsPort);
        using var client = new UdpClient(server.AddressFamily)
        {
            DontFragment = true
        };

        var requestToSend = request.ToByteArray();
        await client.SendAsync(requestToSend, requestToSend.Length, endPoint).ConfigureAwait(false);

        var result = await client.ReceiveAsync(cancel).ConfigureAwait(false);
        var response = request.CreateResponse();
        await Logger.LogAsync(
            $"Reading DNS response for '{request.Questions.FirstOrDefault()?.Name}' ('{request.Questions.FirstOrDefault()?.Type}') via '{server}' (UDP)",
            ELogType.DEBUG, false);
        await response.Read(result.Buffer).ConfigureAwait(false);
        return response;
    }

    private async Task<Message> QueryTcpAsync(Message request, IPAddress server, CancellationToken cancel)
    {
        await _tcpClient.ConnectAsync(server, DnsPort).ConfigureAwait(false);
        await using var stream = _tcpClient.GetStream();

        var requestToBeSend = request.ToByteArray();
        var length = BitConverter.GetBytes((ushort)requestToBeSend.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(length);
        }

        await stream.WriteAsync(length.AsMemory(), cancel).ConfigureAwait(false);
        await stream.WriteAsync(requestToBeSend.AsMemory(), cancel).ConfigureAwait(false);
        await stream.FlushAsync(cancel).ConfigureAwait(false);

        var buffer = new byte[2];
        var responseLength = 0;

        await Logger.LogAsync(
            $"Reading DNS response for '{request.Questions.FirstOrDefault()?.Name}' ('{request.Questions.FirstOrDefault()?.Type}') via '{server}' (TCP)",
            ELogType.DEBUG, false);
        await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancel).ConfigureAwait(false);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(buffer);
            responseLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }
        else
        {
            responseLength = BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }

        buffer = new byte[responseLength];
        await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancel).ConfigureAwait(false);

        var response = request.CreateResponse();
        await response.Read(buffer, 0, buffer.Length).ConfigureAwait(false);
        return response;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellationTokenSource.Dispose();
            _tcpClient.Dispose();
        }

        base.Dispose(disposing);
    }
}