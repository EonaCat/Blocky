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

using EonaCat.Dns.Core.Base;

namespace EonaCat.Dns.Core.Records;

public class Nsec3ParamRecord : ResourceRecord
{
    public Nsec3ParamRecord()
    {
        Type = RecordType.Nsec3Param;
    }

    public DigestType HashAlgorithm { get; set; }

    public byte Flags { get; set; }

    public ushort Iterations { get; set; }

    public byte[] Salt { get; set; }

    public override void ReadData(DnsReader reader, int length)
    {
        HashAlgorithm = (DigestType)reader.ReadByte();
        Flags = reader.ReadByte();
        Iterations = reader.ReadUInt16();
        Salt = reader.ReadByteLengthPrefixedBytes();
    }

    public override void WriteData(DnsWriter writer)
    {
        writer.WriteByte((byte)HashAlgorithm);
        writer.WriteByte(Flags);
        writer.WriteUInt16(Iterations);
        writer.WriteByteLengthPrefixedBytes(Salt);
    }

    public override void ReadData(MasterReader reader)
    {
        HashAlgorithm = (DigestType)reader.ReadByte();
        Flags = reader.ReadByte();
        Iterations = reader.ReadUInt16();

        var salt = reader.ReadString();
        if (salt != "-")
        {
            Salt = Base16Converter.ToBytes(salt);
        }
    }

    public override void WriteData(MasterWriter writer)
    {
        writer.WriteByte((byte)HashAlgorithm);
        writer.WriteByte(Flags);
        writer.WriteUInt16(Iterations);

        if (Salt == null || Salt.Length == 0)
        {
            writer.WriteString("-");
        }
        else
        {
            writer.WriteBase16String(Salt, false);
        }
    }
}