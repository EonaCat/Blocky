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

namespace EonaCat.Dns.Core.Records;

public class RrsigRecord : ResourceRecord
{
    public RrsigRecord()
    {
        Type = RecordType.Rrsig;
    }

    public RecordType TypeCovered { get; set; }

    public SecurityAlgorithm Algorithm { get; set; }

    public byte Labels { get; set; }

    public TimeSpan OriginalTtl { get; set; }

    public DateTime SignatureExpiration { get; set; }

    public DateTime SignatureInception { get; set; }

    public ushort KeyTag { get; set; }

    public DomainName SignerName { get; set; }

    public byte[] Signature { get; set; }

    public override void ReadData(DnsReader reader, int length)
    {
        var end = reader.CurrentPosition + length;

        TypeCovered = (RecordType)reader.ReadUInt16();
        Algorithm = (SecurityAlgorithm)reader.ReadByte();
        Labels = reader.ReadByte();
        OriginalTtl = reader.ReadTimeSpan32();
        SignatureExpiration = reader.ReadDateTime32();
        SignatureInception = reader.ReadDateTime32();
        KeyTag = reader.ReadUInt16();
        SignerName = reader.ReadDomainName();
        Signature = reader.ReadBytes(end - reader.CurrentPosition);
    }

    public override void WriteData(DnsWriter writer)
    {
        writer.WriteUInt16((ushort)TypeCovered);
        writer.WriteByte((byte)Algorithm);
        writer.WriteByte(Labels);
        writer.WriteTimeSpan32(OriginalTtl);
        writer.WriteDateTime32(SignatureExpiration);
        writer.WriteDateTime32(SignatureInception);
        writer.WriteUInt16(KeyTag);
        writer.WriteDomainName(SignerName, true);
        writer.WriteBytes(Signature);
    }

    public override void ReadData(MasterReader reader)
    {
        TypeCovered = reader.ReadDnsType();
        Algorithm = (SecurityAlgorithm)reader.ReadByte();
        Labels = reader.ReadByte();
        OriginalTtl = reader.ReadTimeSpan32();
        SignatureExpiration = reader.ReadDateTime();
        SignatureInception = reader.ReadDateTime();
        KeyTag = reader.ReadUInt16();
        SignerName = reader.ReadDomainName();
        Signature = reader.ReadBase64String();
    }

    public override void WriteData(MasterWriter writer)
    {
        writer.WriteDnsType(TypeCovered);
        writer.WriteByte((byte)Algorithm);
        writer.WriteByte(Labels);
        writer.WriteTimeSpan32(OriginalTtl);
        writer.WriteDateTime(SignatureExpiration);
        writer.WriteDateTime(SignatureInception);
        writer.WriteUInt16(KeyTag);
        writer.WriteDomainName(SignerName);
        writer.WriteBase64String(Signature, false);
    }
}