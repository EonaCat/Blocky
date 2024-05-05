﻿/*
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

public class MxRecord : ResourceRecord
{
    public MxRecord()
    {
        Type = RecordType.Mx;
    }

    public ushort Preference { get; set; }

    public DomainName Exchange { get; set; }

    public override void ReadData(DnsReader reader, int length)
    {
        Preference = reader.ReadUInt16();
        Exchange = reader.ReadDomainName();
    }

    public override void ReadData(MasterReader reader)
    {
        Preference = reader.ReadUInt16();
        Exchange = reader.ReadDomainName();
    }

    public override void WriteData(DnsWriter writer)
    {
        writer.WriteUInt16(Preference);
        writer.WriteDomainName(Exchange);
    }

    public override void WriteData(MasterWriter writer)
    {
        writer.WriteUInt16(Preference);
        writer.WriteDomainName(Exchange, false);
    }
}