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
using System.Net;

namespace EonaCat.Dns.Core.Records;

public class WksRecord : ResourceRecord
{
    public WksRecord()
    {
        Type = RecordType.Wks;
    }

    public string Hostname { get; set; }
    public IPAddress IpAddress { get; set; }
    public byte ProtocolNumber { get; set; }
    public List<ushort> ServicePortNumbers { get; set; }

    public override void ReadData(DnsReader reader, int length)
    {
        Hostname = reader.ReadString();
        IpAddress = reader.ReadIpAddress();
        ProtocolNumber = reader.ReadByte();
        ServicePortNumbers = new List<ushort>(reader.ReadUInt16());
    }

    public override void WriteData(DnsWriter writer)
    {
        writer.WriteString(Hostname);
        writer.WriteIpAddress(IpAddress);
        writer.WriteByte(ProtocolNumber);
        foreach (var servicePortNumber in ServicePortNumbers) writer.WriteUInt16(servicePortNumber);
    }
}