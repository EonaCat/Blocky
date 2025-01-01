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

using System.Collections.Generic;
using System.Linq;
using System.Net;
using EonaCat.Dns.Core.Records;

namespace EonaCat.Dns.Core.MultiCast;

public class ServiceProfile
{
    public ServiceProfile()
    {
    }

    public ServiceProfile(DomainName instanceName, DomainName serviceName, ushort port,
        IEnumerable<IPAddress> addresses = null)
    {
        InstanceName = instanceName;
        ServiceName = serviceName;
        var fqn = FullyQualifiedName;

        var simpleServiceName = new DomainName(ServiceName.ToString()
            .Replace("._tcp", "")
            .Replace("._udp", "")
            .Trim('_')
            .Replace("_", "-"));
        HostName = DomainName.Join(InstanceName, simpleServiceName, Domain);
        Resources.Add(new SrvRecord
        {
            Name = fqn,
            Port = port,
            Target = HostName
        });
        Resources.Add(new TxtRecord
        {
            Name = fqn,
            Strings = { "txtvers=1" }
        });

        foreach (var address in addresses ?? MultiCastService.GetLinkLocalAddresses())
            Resources.Add(AddressRecordBase.Create(HostName, address));
    }

    public DomainName Domain { get; } = "local";

    public DomainName ServiceName { get; set; }

    public DomainName InstanceName { get; set; }

    public DomainName QualifiedServiceName => DomainName.Join(ServiceName, Domain);

    public DomainName HostName { get; set; }

    public DomainName FullyQualifiedName =>
        DomainName.Join(InstanceName, ServiceName, Domain);

    public List<ResourceRecord> Resources { get; set; } = new();

    public List<string> Subtypes { get; set; } = new();

    public void AddProperty(string key, string value)
    {
        var txt = Resources.OfType<TxtRecord>().FirstOrDefault();
        if (txt == null)
        {
            txt = new TxtRecord { Name = FullyQualifiedName };
            Resources.Add(txt);
        }

        txt.Strings.Add(key + "=" + value);
    }
}