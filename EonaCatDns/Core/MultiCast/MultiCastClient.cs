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

using EonaCat.Logger;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace EonaCat.Dns.Core.MultiCast
{
    internal class MultiCastClient : IDisposable
    {
        public static readonly int MultiCastPort = 5353;

        private static readonly IPAddress MultiCastAddressIp4 = IPAddress.Parse("224.0.1.251");
        private static readonly IPAddress MultiCastAddressIp6 = IPAddress.Parse("FF02::FB");
        private static readonly IPEndPoint MultiCastDnsEndpointIp6 = new IPEndPoint(MultiCastAddressIp6, MultiCastPort);
        private static readonly IPEndPoint MultiCastDnsEndpointIp4 = new IPEndPoint(MultiCastAddressIp4, MultiCastPort);

        private readonly List<UdpClient> _receivers;
        private readonly ConcurrentDictionary<IPAddress, UdpClient> _senders = new ConcurrentDictionary<IPAddress, UdpClient>();

        public event EventHandler<UdpReceiveResult> MessageReceived;

        internal MultiCastClient(bool useIPv4, bool useIpv6, IEnumerable<NetworkInterface> nics)
        {
            // Setup the receivers.
            _receivers = new List<UdpClient>();

            UdpClient receiver4 = null;
            if (useIPv4)
            {
                receiver4 = new UdpClient(AddressFamily.InterNetwork);
                receiver4.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                receiver4.Client.Bind(new IPEndPoint(IPAddress.Any, MultiCastPort));
                _receivers.Add(receiver4);
            }

            UdpClient receiver6 = null;
            if (useIpv6)
            {
                receiver6 = new UdpClient(AddressFamily.InterNetworkV6);
                receiver6.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                receiver6.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, MultiCastPort));
                _receivers.Add(receiver6);
            }

            // Get the IP addresses that we should send to.
            var addresses = nics
                .SelectMany(GetNetworkInterfaceLocalAddresses)
                .Where(a => (useIPv4 && a.AddressFamily == AddressFamily.InterNetwork) || (useIpv6 && a.AddressFamily == AddressFamily.InterNetworkV6));
            foreach (var address in addresses)
            {
                if (_senders.Keys.Contains(address))
                {
                    continue;
                }

                var localEndpoint = new IPEndPoint(address, MultiCastPort);
                var sender = new UdpClient(address.AddressFamily);
                try
                {
                    switch (address.AddressFamily)
                    {
                        case AddressFamily.InterNetwork:
                            if (receiver4 == null)
                                break;
                            receiver4.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(MultiCastAddressIp4, address));
                            sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                            sender.Client.Bind(localEndpoint);
                            sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(MultiCastAddressIp4));
                            sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                            break;

                        case AddressFamily.InterNetworkV6:
                            if (receiver6 == null)
                                break;
                            receiver6.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(MultiCastAddressIp6, address.ScopeId));
                            sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                            sender.Client.Bind(localEndpoint);
                            sender.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(MultiCastAddressIp6));
                            sender.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, true);
                            break;

                        default:
                            throw new NotSupportedException($"Address family {address.AddressFamily}.");
                    }

                    LoggerMultiCast.Log($"Will send via {localEndpoint}", ELogType.DEBUG);
                    if (!_senders.TryAdd(address, sender)) // Should not fail
                    {
                        sender.Dispose();
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                {
                    sender.Dispose();
                }
                catch (Exception e)
                {
                    LoggerMultiCast.Log($"Cannot setup send socket for {address}: {e.Message}", ELogType.ERROR);
                    sender.Dispose();
                }
            }

            // Start listening for messages.
            foreach (var receiver in _receivers)
            {
                Listen(receiver);
            }
        }

        public async Task SendAsync(byte[] message)
        {
            foreach (var sender in _senders)
            {
                try
                {
                    var endpoint = sender.Key.AddressFamily == AddressFamily.InterNetwork ? MultiCastDnsEndpointIp4 : MultiCastDnsEndpointIp6;
                    await sender.Value.SendAsync(message, message.Length, endpoint).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    LoggerMultiCast.Log($"Sender {sender.Key} failure: {e.Message}", ELogType.ERROR);
                }
            }
        }

        private void Listen(UdpClient receiver)
        {
            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var result = await receiver.ReceiveAsync().ConfigureAwait(false);
                        MessageReceived?.Invoke(this, result);
                    }
                }
                catch (Exception)
                {
                    // do nothing
                }
            });
        }

        private static IEnumerable<IPAddress> GetNetworkInterfaceLocalAddresses(NetworkInterface nic)
        {
            return nic
                .GetIPProperties()
                .UnicastAddresses
                .Where(x => x.Address.AddressFamily != AddressFamily.InterNetworkV6 || x.Address.IsIPv6LinkLocal)
                .Select(x => x.Address);
        }

        private bool _disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue)
            {
                return;
            }

            if (disposing)
            {
                MessageReceived = null;

                foreach (var receiver in _receivers)
                {
                    try
                    {
                        receiver.Dispose();
                    }
                    catch
                    {
                        // do nothing
                    }
                }
                _receivers.Clear();

                foreach (var address in _senders.Keys)
                {
                    if (!_senders.TryRemove(address, out var sender))
                    {
                        continue;
                    }

                    try
                    {
                        sender.Dispose();
                    }
                    catch
                    {
                        // do nothing
                    }
                }
                _senders.Clear();
            }

            _disposedValue = true;
        }

        ~MultiCastClient()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
