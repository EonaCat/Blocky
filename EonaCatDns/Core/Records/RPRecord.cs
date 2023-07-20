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

namespace EonaCat.Dns.Core.Records
{
    public class RpRecord : ResourceRecord
    {
        public RpRecord()
        {
            Type = RecordType.Rp;
        }

        public DomainName Mailbox { get; set; } = DomainName.Root;

        public DomainName TextName { get; set; } = DomainName.Root;

        public override void ReadData(DnsReader reader, int length)
        {
            Mailbox = reader.ReadDomainName();
            TextName = reader.ReadDomainName();
        }

        public override void ReadData(MasterReader reader)
        {
            Mailbox = reader.ReadDomainName();
            TextName = reader.ReadDomainName();
        }

        public override void WriteData(DnsWriter writer)
        {
            writer.WriteDomainName(Mailbox);
            writer.WriteDomainName(TextName);
        }

        public override void WriteData(MasterWriter writer)
        {
            writer.WriteDomainName(Mailbox);
            writer.WriteDomainName(TextName, appendSpace: false);
        }
    }
}