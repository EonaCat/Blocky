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

using EonaCat.Helpers.Retry;
using EonaCat.Logger;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EonaCat.Dns.Core.Clients
{
    internal class DnsClient : DnsClientBase
    {
        private const int DnsPort = 53;

        public static TimeSpan TimeoutUdp { get; set; } = TimeSpan.FromSeconds(5);

        public static TimeSpan TimeoutTcp { get; set; } = TimeSpan.FromSeconds(5);

        private IEnumerable<IPAddress> _servers;

        public IEnumerable<IPAddress> Servers
        {
            get => _servers ?? GetServers();
            set => _servers = value;
        }

        public IEnumerable<IPAddress> AvailableServers()
        {
            return Servers
                .Where(a =>
                    Socket.OSSupportsIPv4 && a.AddressFamily == AddressFamily.InterNetwork ||
                    Socket.OSSupportsIPv6 && a.AddressFamily == AddressFamily.InterNetworkV6);
        }

        public IEnumerable<IPAddress> GetServers()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.OperationalStatus == OperationalStatus.Up)
                .Where(x => x.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(x => x.GetIPProperties().DnsAddresses);
        }

        public override Task<Message> QueryAsync(
            Message request,
            CancellationToken cancel = default)
        {
            var localDnsServers = GetLocalDnsAddresses();
            if (localDnsServers.Length == 0)
            {
                throw new Exception("EonaCatDns: " + $"#{request.Header.Id} No local Dns servers configured.");
            }
            return GetResponseFromDnsAsync(request, cancel, localDnsServers);
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
            {
                try
                {
                    response = await QueryAsync(request, server, cancel).ConfigureAwait(false);
                    if (!response.HasAnswers)
                    {
                        continue;
                    }

                    forwarder = server.ToString();
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Do nothing
                }
                catch (Exception ex)
                {
                    Logger.Log(ex, $"Could not resolve Dns using {server}", writeToConsole: false);
                }
            }

            foreach (var question in request.Questions)
            {
                if (response == null)
                {
                    break;
                }

                if (response.Header.ResponseCode != ResponseCode.NoError)
                {
                    Logger.Log($"#{response.Header.Id} Dns error received for '{question.Name} '{response.Header.ResponseCode}'.",
                        ELogType.WARNING);
                }
                else if (response.HasAnswers)
                {
                    Logger.Log($"Got Dns response #{response.Header.Id} for {question.Name} from {forwarder}", ELogType.DEBUG, writeToConsole: false);
                }
            }

            return response;
        }

        private IPAddress[] GetLocalDnsAddresses()
        {
            var servers = AvailableServers()
                .OrderBy(ipAddress => ipAddress.AddressFamily)
                .ToArray();
            return servers;
        }

        private async Task<Message> QueryAsync(Message request, IPAddress server, CancellationToken cancel)
        {
            Message response = null;

            RetryHelper.Instance.DefaultMaxTryCount = 3;
            RetryHelper.Instance.DefaultMaxTryTime = TimeSpan.FromSeconds(10);
            RetryHelper.Instance.DefaultTryInterval = TimeSpan.FromMilliseconds(100);

            try
            {
                // if we don't have a valid response set the request again
                response = await QueryUdpAsync(request, server, cancel).ConfigureAwait(false) ?? request;

                // If truncated response, then use Tcp.
                if (response.Header.IsResponse && !response.Header.IsTruncated)
                {
                    return response;
                }
            }
            catch (SocketException e)
            {
                // Cannot connect, try another server.
                Logger.Log(e.Message, ELogType.WARNING);
                return request;
            }
            catch (TaskCanceledException e)
            {
                // Timeout, retry with Tcp
                Logger.Log(e.Message, ELogType.WARNING);
            }

            // If no response, then try TCP
            try
            {
                response = await QueryTcpAsync(response, server, cancel).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Log(e.Message, ELogType.WARNING);
            }
            return response;
        }

        private static async Task<Message> QueryUdpAsync(
            Message request,
            IPAddress server,
            CancellationToken cancel)
        {
            var question = request.Questions.FirstOrDefault();
            if (question == null)
            {
                // Invalid question
                return null;
            }

            var endPoint = new IPEndPoint(server, DnsPort);
            try
            {
                using var client = new UdpClient(server.AddressFamily);

                try
                {
                    client.DontFragment = true;
                }
                catch (NotSupportedException)
                {
                    // ignore and fragment the packets
                }

                client.EnableBroadcast = false;

                // Cancel the request when either the timeout is reached or the
                // task is cancelled by the caller.
                var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancel, new CancellationTokenSource(TimeoutUdp).Token);

                if (request.Header.IsResponse)
                {
                    throw new Exception("The given request already is a response, cannot send query over Udp");
                }

                var requestToSend = request.ToByteArray();
                await client.SendAsync(requestToSend, requestToSend.Length, endPoint).ConfigureAwait(false);

                var result = await client.ReceiveAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                var response = request.CreateResponse();
                Logger.Log($"Reading Dns response for '{question.Name}' ('{question.Type}') via '{server}' (UDP)", ELogType.DEBUG, writeToConsole: false);
                response.Read(result.Buffer);
                return response;
            }
            catch (OperationCanceledException)
            {
                // Do nothing
            }
            catch (Exception exception)
            {
                Logger.Log(exception);
            }

            return request;
        }


        private readonly CancellationTokenSource _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            new CancellationTokenSource().Token,
            new CancellationTokenSource(TimeoutTcp).Token);

        private readonly TcpClient _tcpClient = new TcpClient(AddressFamily.InterNetworkV6);

        private async Task<Message> QueryTcpAsync(
            Message request,
            IPAddress server,
            CancellationToken cancel)
        {
            var question = request.Questions.FirstOrDefault();
            if (question == null)
            {
                // Invalid question
                return null;
            }

            var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancel,
                _cancellationTokenSource.Token).Token;

            await _tcpClient.ConnectAsync(server, DnsPort, cancellationToken).ConfigureAwait(false);

            await using var stream = _tcpClient.GetStream();

            var requestToBeSend = request.ToByteArray();

            // The message is prefixed with a two byte length field which gives
            // the message length, excluding the two byte length field.
            var length = BitConverter.GetBytes((ushort)requestToBeSend.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(length);
            }
            await stream.WriteAsync(length.AsMemory(), cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(requestToBeSend.AsMemory(), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Read response length
            var buffer = new byte[2];
            var responseLength = 0;
            
            Logger.Log($"Reading Dns response for '{question.Name}' ('{question.Type}') via '{server}' (UDP)", ELogType.DEBUG, writeToConsole: false);
            _ = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
                responseLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
            }
            else
            {
                responseLength = BinaryPrimitives.ReadUInt16BigEndian(buffer);
            }


            // Read response message
            buffer = new byte[responseLength];
            _ = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);

            var response = request.CreateResponse();
            response.Read(buffer, 0, buffer.Length);
            return response;
        }

    }
}