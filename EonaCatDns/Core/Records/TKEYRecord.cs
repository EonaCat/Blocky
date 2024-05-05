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

public class TkeyRecord : ResourceRecord
{
    private static readonly byte[] NoData = Array.Empty<byte>();

    public TkeyRecord()
    {
        Type = RecordType.Tkey;
        Class = RecordClass.Any;
        Ttl = TimeSpan.Zero;
        OtherData = NoData;
    }

    public DomainName Algorithm { get; set; }

    public DateTime Inception { get; set; }

    public DateTime Expiration { get; set; }

    public KeyExchangeMode Mode { get; set; }

    public ResponseCode Error { get; set; }

    public byte[] Key { get; set; }

    public byte[] OtherData { get; set; }

    public override void ReadData(DnsReader reader, int length)
    {
        Algorithm = reader.ReadDomainName();
        Inception = reader.ReadDateTime32();
        Expiration = reader.ReadDateTime32();
        Mode = (KeyExchangeMode)reader.ReadUInt16();
        Error = (ResponseCode)reader.ReadUInt16();
        Key = reader.ReadUInt16LengthPrefixedBytes();
        OtherData = reader.ReadUInt16LengthPrefixedBytes();
    }

    public override void WriteData(DnsWriter writer)
    {
        writer.WriteDomainName(Algorithm);
        writer.WriteDateTime32(Inception);
        writer.WriteDateTime32(Expiration);
        writer.WriteUInt16((ushort)Mode);
        writer.WriteUInt16((ushort)Error);
        writer.WriteUint16LengthPrefixedBytes(Key);
        writer.WriteUint16LengthPrefixedBytes(OtherData);
    }

    public override void ReadData(MasterReader reader)
    {
        Algorithm = reader.ReadDomainName();
        Inception = reader.ReadDateTime();
        Expiration = reader.ReadDateTime();
        Mode = (KeyExchangeMode)reader.ReadUInt16();
        Error = (ResponseCode)reader.ReadUInt16();
        Key = Convert.FromBase64String(reader.ReadString());
        OtherData = Convert.FromBase64String(reader.ReadString());
    }

    public override void WriteData(MasterWriter writer)
    {
        writer.WriteDomainName(Algorithm);
        writer.WriteDateTime(Inception);
        writer.WriteDateTime(Expiration);
        writer.WriteUInt16((ushort)Mode);
        writer.WriteUInt16((ushort)Error);
        writer.WriteBase64String(Key);
        writer.WriteBase64String(OtherData ?? NoData, false);
    }
}