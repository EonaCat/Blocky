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
using EonaCat.Dns.Core.Base;

namespace EonaCat.Dns.Core.Records;

public class Nsec3Record : ResourceRecord
{
    public Nsec3Record()
    {
        Type = RecordType.Nsec3;
    }

    public DigestType HashAlgorithm { get; set; }

    public Nsec3Flags Flags { get; set; }

    public ushort Iterations { get; set; }

    public byte[] Salt { get; set; }

    public byte[] NextHashedOwnerName { get; set; }

    public List<RecordType> Types { get; set; } = new();

    public override void ReadData(DnsReader reader, int length)
    {
        var end = reader.CurrentPosition + length;

        HashAlgorithm = (DigestType)reader.ReadByte();
        Flags = (Nsec3Flags)reader.ReadByte();
        Iterations = reader.ReadUInt16();
        Salt = reader.ReadByteLengthPrefixedBytes();
        NextHashedOwnerName = reader.ReadByteLengthPrefixedBytes();

        while (reader.CurrentPosition < end) Types.AddRange(reader.ReadBitmap().Select(t => (RecordType)t));
    }

    public override void WriteData(DnsWriter writer)
    {
        writer.WriteByte((byte)HashAlgorithm);
        writer.WriteByte((byte)Flags);
        writer.WriteUInt16(Iterations);
        writer.WriteByteLengthPrefixedBytes(Salt);
        writer.WriteByteLengthPrefixedBytes(NextHashedOwnerName);
        writer.WriteBitmap(Types.Select(t => (ushort)t));
    }

    public override void ReadData(MasterReader reader)
    {
        HashAlgorithm = (DigestType)reader.ReadByte();
        Flags = (Nsec3Flags)reader.ReadByte();
        Iterations = reader.ReadUInt16();

        var salt = reader.ReadString();
        if (salt != "-")
        {
            Salt = Base16Converter.ToBytes(salt);
        }

        NextHashedOwnerName = Base32Converter.ToBytes(reader.ReadString());

        while (!reader.IsEndOfLine()) Types.Add(reader.ReadDnsType());
    }

    public override void WriteData(MasterWriter writer)
    {
        writer.WriteByte((byte)HashAlgorithm);
        writer.WriteByte((byte)Flags);
        writer.WriteUInt16(Iterations);

        if (Salt == null || Salt.Length == 0)
        {
            writer.WriteString("-");
        }
        else
        {
            writer.WriteBase16String(Salt);
        }

        writer.WriteString(Base32Converter.ToString(NextHashedOwnerName).ToLowerInvariant());

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