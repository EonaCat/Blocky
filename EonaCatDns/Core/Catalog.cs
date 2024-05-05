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
using System.Reflection;
using System.Threading.Tasks;
using EonaCat.Dns.Core.Extensions;
using EonaCat.Dns.Core.Helpers;
using EonaCat.Dns.Core.Records;
using EonaCat.Dns.Database;
using EonaCat.Helpers.Controls;

namespace EonaCat.Dns.Core;

public class Catalog : Dictionary<DomainName, DomainNode>
{
    private const int CheckIntervalForClientsInMinutes = 10;
    private readonly object _lock = new();
    private readonly HashSet<DomainName> _zoneKeys = new();

    public static EonaCatTimer ClientCheckTimer { get; set; }

    public bool IncludeClientsFromDatabase { get; set; }

    private async Task AddZone(MasterReader reader)
    {
        var resources = new HashSet<ResourceRecord>();
        ResourceRecord resourceRecord;
        while ((resourceRecord = await reader.ReadResourceRecord().ConfigureAwait(false)) != null)
            resources.Add(resourceRecord);

        if (resources.Count == 0)
        {
            throw new InvalidDataException("EonaCatDns: No resources found in masterFile.");
        }

        if (resources.First().Type != RecordType.Soa)
        {
            throw new InvalidDataException("EonaCatDns: First resource record must be a SOA.");
        }

        var soa = (SoaRecord)resources.First();
        if (resources.Any(resourceRecord => !resourceRecord.Name.BelongsTo(soa.Name)))
        {
            throw new InvalidDataException("EonaCatDns: All resource records must belong to the zone.");
        }

        var nodes = resources
            .GroupBy(
                resourceRecord => resourceRecord.Name,
                (key, results) => new DomainNode
                {
                    Name = key,
                    IsAuthoritative = true,
                    Resources = new HashSet<ResourceRecord>(results)
                });

        foreach (var node in nodes)
            if (!TryAdd(node.Name, node))
            {
                throw new InvalidDataException($"EonaCatDns: '{node.Name}' already exists.");
            }
    }

    public void RemoveZone(DomainName name)
    {
        lock (_lock)
        {
            var keysToRemove = _zoneKeys.Where(domainName => domainName.BelongsTo(name));
            foreach (var key in keysToRemove) Remove(key);
        }
    }

    public DomainNode AddOrUpdate(ResourceRecord resource, bool authoritative = false)
    {
        lock (_lock)
        {
            if (!TryGetValue(resource.Name, out var node))
            {
                node = new DomainNode
                {
                    Name = resource.Name
                };
                Add(resource.Name, node);
            }

            node.IsAuthoritative = authoritative;

            if (node.Resources.Add(resource))
            {
                return node;
            }

            // If the resource already exists, then update
            node.Resources.Remove(resource);
            node.Resources.Add(resource);
            return node;
        }
    }

    private async Task WithRootNs()
    {
        var assembly = typeof(Catalog).GetTypeInfo().Assembly;
        using (var hints = assembly.GetManifestResourceStream("EonaCat.Dns.Core.RootNS"))
        {
            var rootHints = RootHintHelper.Parse(hints);
            var masterFileReader = new MasterReader(null);
            foreach (var rootHintEntry in rootHints)
                AddOrUpdate(await masterFileReader.ReadResourceRecord(rootHintEntry).ConfigureAwait(false));
        }

        // Add the root resourceRecord for the nameServer
        var resource = new ResourceRecord { Name = new DomainName(".") };
        var node = GetOrAdd(resource);
        node.IsAuthoritative = true;
    }

    public DomainNode GetOrAdd(ResourceRecord resource, bool authoritative = false)
    {
        lock (_lock)
        {
            if (TryGetValue(resource.Name, out var node))
            {
                return node;
            }

            node = new DomainNode
            {
                Name = resource.Name,
                IsAuthoritative = authoritative
            };
            if (TryAdd(resource.Name, node))
            {
                return node;
            }

            // Failed to add, possibly due to concurrent modification
            if (TryGetValue(resource.Name, out node))
            {
                return node;
            }

            throw new InvalidOperationException("Failed to add or get the DomainNode.");
        }
    }

    public async Task WithResourceRecord(MasterReader reader, bool authoritative = false)
    {
        ResourceRecord resourceRecord;
        while ((resourceRecord = await reader.ReadResourceRecord().ConfigureAwait(false)) != null)
            AddOrUpdate(resourceRecord, authoritative);
    }

    public void WithReverseLookupRecords()
    {
        var addressRecords = Values
            .Where(node => node.IsAuthoritative)
            .SelectMany(node => node.Resources.OfType<AddressRecordBase>())
            .ToList(); // Create a copy of the collection

        Parallel.ForEach(addressRecords, AddReverseLookupRecord);
    }


    internal void AddReverseLookupRecord(AddressRecordBase addressRecord)
    {
        var ptrRecord = new PtrRecord
        {
            Class = addressRecord.Class,
            Name = new DomainName(addressRecord.Address.GetArpaName()),
            DomainName = addressRecord.Name,
            Ttl = addressRecord.Ttl
        };
        AddOrUpdate(ptrRecord, true);
    }

    public static async Task<Catalog> GenerateCatalog(string masterFileContents, bool withReverseLookup = true,
        bool withRootNs = true, bool withClientsFromDatabase = true)
    {
        var catalog = new Catalog();
        if (!string.IsNullOrWhiteSpace(masterFileContents))
        {
            await catalog.AddZone(new MasterReader(new StringReader(masterFileContents))).ConfigureAwait(false);
        }

        if (withReverseLookup)
        {
            catalog.WithReverseLookupRecords();
        }

        if (withRootNs)
        {
            await catalog.WithRootNs().ConfigureAwait(false);
        }

        if (withClientsFromDatabase)
        {
            catalog.IncludeClientsFromDatabase = true;
        }

        catalog.CreateClientDatabaseTimerCheck();

        return catalog;
    }

    private void CreateClientDatabaseTimerCheck()
    {
        ClientCheckTimer = new EonaCatTimer(TimeSpan.FromMinutes(CheckIntervalForClientsInMinutes), RetrieveClients);
        ClientCheckTimer.Start();
    }

    private async void RetrieveClients()
    {
        if (!IncludeClientsFromDatabase)
        {
            return;
        }

        var clients = await DatabaseManager.Clients.GetAll().ToListAsync().ConfigureAwait(false);
        foreach (var client in clients)
        {
            if (!IPAddress.TryParse(client.Ip, out var ip))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(client.Name))
            {
                continue;
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var record = new AaaaRecord
                {
                    Name = client.Name,
                    Type = RecordType.Aaaa,
                    Address = ip,
                    Class = RecordClass.Internet,
                    TimeCreated = DateTime.Now,
                    Ttl = TimeSpan.FromMinutes(CheckIntervalForClientsInMinutes)
                };
                AddReverseLookupRecord(record);
            }
            else
            {
                var record = new ARecord
                {
                    Name = client.Name,
                    Type = RecordType.A,
                    Address = ip,
                    Class = RecordClass.Internet,
                    TimeCreated = DateTime.Now,
                    Ttl = TimeSpan.FromMinutes(CheckIntervalForClientsInMinutes)
                };
                AddReverseLookupRecord(record);
            }
        }
    }
}