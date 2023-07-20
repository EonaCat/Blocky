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

namespace EonaCat.Dns.Core.Records
{
    public class SoaRecord : ResourceRecord
    {
        public SoaRecord()
        {
            Type = RecordType.Soa;
            Ttl = TimeSpan.FromSeconds(0);
        }

        public DomainName PrimaryName { get; set; }

        public DomainName Mailbox { get; set; }

        public uint SerialNumber { get; set; }

        public TimeSpan Refresh { get; set; }

        public TimeSpan Retry { get; set; }

        public TimeSpan Expire { get; set; }

        public TimeSpan Minimum { get; set; }

        public override void ReadData(DnsReader reader, int length)
        {
            PrimaryName = reader.ReadDomainName();
            Mailbox = reader.ReadDomainName();
            SerialNumber = reader.ReadUInt32();
            Refresh = reader.ReadTimeSpan32();
            Retry = reader.ReadTimeSpan32();
            Expire = reader.ReadTimeSpan32();
            Minimum = reader.ReadTimeSpan32();
        }

        public override void ReadData(MasterReader reader)
        {
            PrimaryName = reader.ReadDomainName();
            Mailbox = reader.ReadDomainName();
            SerialNumber = reader.ReadUInt32();
            Refresh = reader.ReadTimeSpan32();
            Retry = reader.ReadTimeSpan32();
            Expire = reader.ReadTimeSpan32();
            Minimum = reader.ReadTimeSpan32();
        }

        public override void WriteData(DnsWriter writer)
        {
            writer.WriteDomainName(PrimaryName);
            writer.WriteDomainName(Mailbox);
            writer.WriteUInt32(SerialNumber);
            writer.WriteTimeSpan32(Refresh);
            writer.WriteTimeSpan32(Retry);
            writer.WriteTimeSpan32(Expire);
            writer.WriteTimeSpan32(Minimum);
        }

        public override void WriteData(MasterWriter writer)
        {
            writer.WriteDomainName(PrimaryName);
            writer.WriteDomainName(Mailbox);
            writer.WriteUInt32(SerialNumber);
            writer.WriteTimeSpan32(Refresh);
            writer.WriteTimeSpan32(Retry);
            writer.WriteTimeSpan32(Expire);
            writer.WriteTimeSpan32(Minimum, appendSpace: false);
        }
    }
}