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

using EonaCat.Dns.Core.Records;
using EonaCat.Dns.Core.Servers;
using EonaCat.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EonaCat.Dns.Core.MultiCast
{
    internal class ServiceDiscovery : IDisposable
    {
        private static readonly DomainName LocalDomain = new("local");
        private static readonly DomainName SubName = new("_sub");

        public static readonly DomainName ServiceName = new("_services._dns-sd._udp.local");

        private readonly bool _ownsMultiCastDns;
        private readonly List<ServiceProfile> _profiles = new();

        internal ServiceDiscovery()
            : this(new MultiCastService())
        {
            _ownsMultiCastDns = true;

            // Auto start.
            MultiCastService.Start();
        }

        internal ServiceDiscovery(MultiCastService multiCastService)
        {
            MultiCastService = multiCastService;
            multiCastService.QueryReceived += OnQueryAsync;
            multiCastService.AnswerReceived += OnAnswer;
        }

        internal MultiCastService MultiCastService { get; private set; }

        internal bool AnswersContainsAdditionalRecords { get; set; }

        internal Ns NameServer { get; } = new()
        {
            Catalog = new Catalog(),
            AnswerAllQuestions = true
        };

        internal event EventHandler<DomainName> ServiceDiscovered;
        internal event EventHandler<ServiceDiscoveryEventArgs> ServiceInstanceDiscovered;
        internal event EventHandler<ServiceDiscoveryShutdownEventArgs> ServiceInstanceShutdown;

        internal void QueryAllServices()
        {
            MultiCastService.SendQueryAsync(ServiceName, type: RecordType.Ptr);
        }

        internal void QueryUniCastAllServices()
        {
            MultiCastService.SendUnicastQueryAsync(ServiceName, type: RecordType.Ptr);
        }

        internal void QueryServiceInstances(DomainName service)
        {
            MultiCastService.SendQueryAsync(DomainName.Join(service, LocalDomain), type: RecordType.Ptr);
        }

        internal void QueryServiceInstances(DomainName service, string subtype)
        {
            var name = DomainName.Join(
                new DomainName(subtype),
                SubName,
                service,
                LocalDomain);
            MultiCastService.SendQueryAsync(name, type: RecordType.Ptr);
        }

        internal void QueryUnicastServiceInstances(DomainName service)
        {
            MultiCastService.SendUnicastQueryAsync(DomainName.Join(service, LocalDomain), type: RecordType.Ptr);
        }

        internal void Advertise(ServiceProfile service)
        {
            _profiles.Add(service);

            var catalog = NameServer.Catalog;
            catalog.AddOrUpdate(
                new PtrRecord { Name = ServiceName, DomainName = service.QualifiedServiceName },
                authoritative: true);
            catalog.AddOrUpdate(
                new PtrRecord { Name = service.QualifiedServiceName, DomainName = service.FullyQualifiedName },
                authoritative: true);

            foreach (var subtype in service.Subtypes)
            {
                var ptr = new PtrRecord
                {
                    Name = DomainName.Join(
                        new DomainName(subtype),
                        SubName,
                        service.QualifiedServiceName),
                    DomainName = service.FullyQualifiedName
                };
                catalog.AddOrUpdate(ptr, authoritative: true);
            }

            foreach (var r in service.Resources)
            {
                catalog.AddOrUpdate(r, authoritative: true);
            }

            catalog.WithReverseLookupRecords();
        }

        internal async Task AnnounceAsync(ServiceProfile profile)
        {
            var message = new Message { Header = new DnsHeader { MessageType = MessageType.Response } };

            // Add the shared records.
            var ptrRecord = new PtrRecord { Name = profile.QualifiedServiceName, DomainName = profile.FullyQualifiedName };
            message.Answers.Add(ptrRecord);

            // Add the resource records.
            profile.Resources.ForEach(resource =>
            {
                message.Answers.Add(resource);
            });

            await MultiCastService.SendAnswerAsync(message, checkDuplicate: false).ConfigureAwait(false);
            await Task.Delay(1000).ConfigureAwait(false);
            await MultiCastService.SendAnswerAsync(message, checkDuplicate: false).ConfigureAwait(false);
        }

        internal void UnAdvertise(ServiceProfile profile)
        {
            var message = new Message { Header = new DnsHeader { MessageType = MessageType.Response } };
            var ptrRecord = new PtrRecord
            {
                Name = profile.QualifiedServiceName,
                DomainName = profile.FullyQualifiedName,
                Ttl = TimeSpan.Zero
            };

            message.Answers.Add(ptrRecord);
            profile.Resources.ForEach(resource =>
            {
                resource.Ttl = TimeSpan.Zero;
                message.AdditionalRecords.Add(resource);
            });

            MultiCastService.SendAnswerAsync(message);

            NameServer.Catalog.Remove(profile.QualifiedServiceName, out _);
        }

        internal void UnAdvertise()
        {
            _profiles.ForEach(UnAdvertise);
        }

        private void OnAnswer(object sender, MessageEventArgs e)
        {
            LoggerMultiCast.Log($"Answer from {e.RemoteEndPoint} {e.Message}", ELogType.DEBUG);

            // Any DNS-SD answers?
            var sd = e.Message.Answers
                .OfType<PtrRecord>()
                .Where(ptr => ptr.Name.IsSubDomainOf(LocalDomain));
            foreach (var ptr in sd)
            {
                if (ptr.Name == ServiceName)
                {
                    ServiceDiscovered?.Invoke(this, ptr.DomainName);
                }
                else if (ptr.Ttl == TimeSpan.Zero)
                {
                    var args = new ServiceDiscoveryShutdownEventArgs
                    {
                        ServiceInstanceName = ptr.DomainName,
                        Message = e.Message
                    };
                    ServiceInstanceShutdown?.Invoke(this, args);
                }
                else
                {
                    var args = new ServiceDiscoveryEventArgs
                    {
                        ServiceInstanceName = ptr.DomainName,
                        Message = e.Message
                    };
                    ServiceInstanceDiscovered?.Invoke(this, args);
                }
            }
        }

        private async void OnQueryAsync(object sender, MessageEventArgs e)
        {
            var request = e.Message;
            LoggerMultiCast.Log($"Query from {e.RemoteEndPoint} {request}", ELogType.DEBUG);

            var isUnicastQueryResponse = false; // unicast query response?
            foreach (var r in request.Questions)
            {
                if (((ushort)r.Class & 0x8000) != 0)
                {
                    isUnicastQueryResponse = true;
                    r.Class = (RecordClass)((ushort)r.Class & 0x7fff);
                }
            }

            var response = await NameServer.QueryAsync(request).ConfigureAwait(false);

            if (response.Header.ResponseCode != ResponseCode.NoError)
            {
                return;
            }

            if (response.Answers.Any(a => a.Name == ServiceName))
            {
                response.AdditionalRecords.Clear();
            }

            if (AnswersContainsAdditionalRecords)
            {
                response.Answers.AddRange(response.AdditionalRecords);
                response.AdditionalRecords.Clear();
            }

            if (isUnicastQueryResponse)
            {
                await MultiCastService.SendAnswerAsync(response, e).ConfigureAwait(false);
            }
            else
            {
                await MultiCastService.SendAnswerAsync(response, e).ConfigureAwait(false);
            }

            LoggerMultiCast.Log($"Sending answer");
            LoggerMultiCast.Log(response.ToString());
            LoggerMultiCast.Log($"Response time {(DateTime.Now - request.TimeCreated).TotalMilliseconds}ms", ELogType.DEBUG);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (MultiCastService != null)
                {
                    MultiCastService.QueryReceived -= OnQueryAsync;
                    MultiCastService.AnswerReceived -= OnAnswer;
                    if (_ownsMultiCastDns)
                    {
                        MultiCastService.Dispose();
                    }
                    MultiCastService = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
