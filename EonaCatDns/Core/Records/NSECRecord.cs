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

using System.Collections.Generic;
using System.Linq;

namespace EonaCat.Dns.Core.Records;

public class NsecRecord : ResourceRecord
{
    public NsecRecord()
    {
        Type = RecordType.Nsec;
    }

    public DomainName NextOwnerName { get; set; } = DomainName.Root;

    public List<RecordType> Types { get; set; } = new();

    public override void ReadData(DnsReader reader, int length)
    {
        var end = reader.CurrentPosition + length;
        NextOwnerName = reader.ReadDomainName();
        while (reader.CurrentPosition < end) Types.AddRange(reader.ReadBitmap().Select(t => (RecordType)t));
    }

    public override void WriteData(DnsWriter writer)
    {
        writer.WriteDomainName(NextOwnerName, true);
        writer.WriteBitmap(Types.Select(t => (ushort)t));
    }

    public override void ReadData(MasterReader reader)
    {
        NextOwnerName = reader.ReadDomainName();
        while (!reader.IsEndOfLine()) Types.Add(reader.ReadDnsType());
    }

    public override void WriteData(MasterWriter writer)
    {
        writer.WriteDomainName(NextOwnerName);

        var next = false;
        foreach (var type in Types)
        {
            if (next)
            {
                writer.WriteSpace();
            }

            writer.WriteDnsType(type, false);
            next = true;
        }
    }
}