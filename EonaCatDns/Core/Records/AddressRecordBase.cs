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

using System.Net;
using System.Net.Sockets;

namespace EonaCat.Dns.Core.Records
{
    public abstract class AddressRecordBase : ResourceRecord
    {
        protected AddressRecordBase()
        {
            Ttl = TTLDefaultHosts;
        }

        public IPAddress Address { get; set; }

        public static AddressRecordBase Create(DomainName name, IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return new ARecord { Name = name, Address = address };
            }

            return new AaaaRecord { Name = name, Address = address };
        }

        public override void ReadData(DnsReader reader, int length)
        {
            Address = reader.ReadIpAddress(length);
        }

        public override void ReadData(MasterReader reader)
        {
            Address = reader.ReadIpAddress();
        }

        public override void WriteData(DnsWriter writer)
        {
            if (Address == null) return;
            writer.WriteIpAddress(Address);
        }

        public override void WriteData(MasterWriter writer)
        {
            if (Address == null) return;
            writer.WriteIpAddress(Address, appendSpace: false);
        }
    }
}