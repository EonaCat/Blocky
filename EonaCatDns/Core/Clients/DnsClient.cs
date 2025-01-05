using EonaCat.Dns.Core.Clients;
using EonaCat.Logger;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;
using EonaCat.Dns;
using EonaCat.Dns.Core;

internal class DnsClient : DnsClientBase
{
    private const int DnsPort = 53;

    private readonly UdpClient _udpClient = new();
    private readonly ConcurrentDictionary<IPAddress, Lazy<TcpClient>> _tcpClients = new();
    private Lazy<List<IPAddress>> LazyServers = new(GetServers);

    public IEnumerable<IPAddress> Servers
    {
        get => LazyServers.Value;
        set => LazyServers = new(() => value.ToList());
    }

    public override async Task<Message> QueryAsync(Message request, CancellationToken cancel = default)
    {
        // Filter servers based on OS support for IP families
        var localDnsServers = Servers
            .Where(a => (Socket.OSSupportsIPv4 && a.AddressFamily == AddressFamily.InterNetwork) ||
                        (Socket.OSSupportsIPv6 && a.AddressFamily == AddressFamily.InterNetworkV6))
            .ToArray();

        if (localDnsServers.Length == 0)
        {
            throw new Exception("EonaCatDns: No local DNS servers configured.");
        }

        await Logger.LogAsync("Starting DNS query.", ELogType.DEBUG, false).ConfigureAwait(false);

        var response = await GetResponseFromDnsAsync(request, localDnsServers, cancel).ConfigureAwait(false);

        await Logger.LogAsync("DNS query completed.", ELogType.DEBUG, false).ConfigureAwait(false);
        return response;
    }

    private async Task<Message> GetResponseFromDnsAsync(Message request, IEnumerable<IPAddress> localDnsServers, CancellationToken cancel)
    {
        // Split servers by address family for parallelism (IPv4 and IPv6)
        var ipv4Servers = localDnsServers.Where(x => x.AddressFamily == AddressFamily.InterNetwork);
        var ipv6Servers = localDnsServers.Where(x => x.AddressFamily == AddressFamily.InterNetworkV6);

        // Create tasks to handle querying multiple servers in parallel
        var tasks = ipv4Servers/*.Concat(ipv6Servers)*/.Select(server => QueryServerAsync(request, server, cancel));

        // Use `Task.WhenAny` to return the first valid response (faster response handling)
        var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
        var response = await completedTask.ConfigureAwait(false);

        return response?.HasAnswers == true ? response : null;
    }

    private async Task<Message> QueryServerAsync(Message request, IPAddress server, CancellationToken cancel)
    {
        try
        {
            await Logger.LogAsync($"Querying server: {server}", ELogType.DEBUG, false).ConfigureAwait(false);
            var response = await QueryAsync(request, server, cancel).ConfigureAwait(false);

            if (response?.HasAnswers == true)
            {
                await Logger.LogAsync($"Successful response from server: {server}", ELogType.INFO, false).ConfigureAwait(false);
            }

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await Logger.LogAsync(ex, $"Error querying server: {server}", false).ConfigureAwait(false);
            return null;
        }
    }

    private async Task<Message> QueryAsync(Message request, IPAddress server, CancellationToken cancel)
    {
        var response = await QueryUdpAsync(request, server, cancel).ConfigureAwait(false);

        if (response?.Header.IsResponse == true && !response.Header.IsTruncated)
        {
            await Logger.LogAsync($"UDP query successful for server: {server}", ELogType.DEBUG, false).ConfigureAwait(false);
            return response;
        }

        await Logger.LogAsync($"UDP response truncated; switching to TCP for server: {server}", ELogType.WARNING, false).ConfigureAwait(false);

        return await QueryTcpAsync(response ?? request, server, cancel).ConfigureAwait(false);
    }

    private async Task<Message> QueryUdpAsync(Message request, IPAddress server, CancellationToken cancel)
    {
        try
        {
            var endPoint = new IPEndPoint(server, DnsPort);
            var requestBytes = request.ToByteArray();

            await Logger.LogAsync($"Sending UDP query to {server}", ELogType.DEBUG, false).ConfigureAwait(false);
            await _udpClient.SendAsync(requestBytes, requestBytes.Length, endPoint).WithCancellation(cancel).ConfigureAwait(false);

            var result = await _udpClient.ReceiveAsync(cancel).ConfigureAwait(false);

            var response = request.CreateResponse();
            await response.Read(result.Buffer).ConfigureAwait(false);

            await Logger.LogAsync($"Received UDP response from {server}", ELogType.DEBUG, false).ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            await Logger.LogAsync(ex, $"UDP query failed for server: {server}", false).ConfigureAwait(false);
            return null;
        }
    }

    private async Task<Message> QueryTcpAsync(Message request, IPAddress server, CancellationToken cancel)
    {
        try
        {
            // Get or create a TcpClient using lazy initialization
            var tcpClientLazy = _tcpClients.GetOrAdd(server, s => new Lazy<TcpClient>(() => new TcpClient(s.AddressFamily)));
            var tcpClient = tcpClientLazy.Value;

            if (!tcpClient.Connected)
            {
                await Logger.LogAsync($"Establishing TCP connection to {server}", ELogType.DEBUG, false).ConfigureAwait(false);
                await tcpClient.ConnectAsync(server, DnsPort, cancel).ConfigureAwait(false);
            }

            var stream = tcpClient.GetStream();
            var requestBytes = request.ToByteArray();
            var lengthPrefix = BitConverter.GetBytes((ushort)requestBytes.Length);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthPrefix);

            await Logger.LogAsync($"Sending TCP query to {server}", ELogType.DEBUG, false).ConfigureAwait(false);
            await stream.WriteAsync(lengthPrefix, cancel).ConfigureAwait(false);
            await stream.WriteAsync(requestBytes, cancel).ConfigureAwait(false);
            await stream.FlushAsync(cancel).ConfigureAwait(false);

            var lengthBuffer = new byte[2];
            await stream.ReadAsync(lengthBuffer, cancel).ConfigureAwait(false);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBuffer);

            var responseLength = BinaryPrimitives.ReadUInt16BigEndian(lengthBuffer);
            var responseBuffer = new byte[responseLength];
            await stream.ReadAsync(responseBuffer, cancel).ConfigureAwait(false);

            var response = request.CreateResponse();
            await response.Read(responseBuffer).ConfigureAwait(false);

            await Logger.LogAsync($"Received TCP response from {server}", ELogType.DEBUG, false).ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            await Logger.LogAsync(ex, $"TCP query failed for server: {server}", false).ConfigureAwait(false);
            return null;
        }
    }

    private static List<IPAddress> GetServers()
    {
        // Optimize server retrieval by directly accessing DNS addresses
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(ni => ni.GetIPProperties().DnsAddresses)
            .ToList();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _udpClient.Dispose();

            foreach (var client in _tcpClients.Values.Select(lazy => lazy.Value))
            {
                client.Close();
                client.Dispose();
            }

            _tcpClients.Clear();
        }

        base.Dispose(disposing);
    }
}
