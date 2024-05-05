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

namespace EonaCat.Dns.Core.Records;

public class SrvRecord : ResourceRecord
{
    public SrvRecord()
    {
        Type = RecordType.Srv;
    }

    public ushort Priority { get; set; }

    public ushort Weight { get; set; }

    public ushort Port { get; set; }

    public DomainName Target { get; set; }

    public override void ReadData(DnsReader reader, int length)
    {
        Priority = reader.ReadUInt16();
        Weight = reader.ReadUInt16();
        Port = reader.ReadUInt16();
        Target = reader.ReadDomainName();
    }

    public override void ReadData(MasterReader reader)
    {
        Priority = reader.ReadUInt16();
        Weight = reader.ReadUInt16();
        Port = reader.ReadUInt16();
        Target = reader.ReadDomainName();
    }

    public override void WriteData(DnsWriter writer)
    {
        writer.WriteUInt16(Priority);
        writer.WriteUInt16(Weight);
        writer.WriteUInt16(Port);
        writer.WriteDomainName(Target);
    }

    public override void WriteData(MasterWriter writer)
    {
        writer.WriteUInt16(Priority);
        writer.WriteUInt16(Weight);
        writer.WriteUInt16(Port);
        writer.WriteDomainName(Target, false);
    }
}