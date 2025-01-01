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

using System;
using System.IO;
using System.Text;
using EonaCat.Dns.Core.Base;
using EonaCat.Dns.Core.Records.Registry;

namespace EonaCat.Dns.Core.Records;

public class DsRecord : ResourceRecord
{
    public DsRecord()
    {
        Type = RecordType.Ds;
    }

    public DsRecord(DnskeyRecord key, bool force = false)
        : this()
    {
        if (!force)
        {
            if ((key.Flags & DnsKeyFlags.ZoneKey) == DnsKeyFlags.None)
            {
                throw new ArgumentException("EonaCatDns: " + "ZoneKey must be set.", nameof(key));
            }

            if ((key.Flags & DnsKeyFlags.SecureEntryPoint) == DnsKeyFlags.None)
            {
                throw new ArgumentException("EonaCatDns: " + "SecureEntryPoint must be set.", nameof(key));
            }
        }

        byte[] digest;
        using (var ms = new MemoryStream())
        using (var hasher = DigestRegistry.Create(key.Algorithm))
        {
            var writer = new DnsWriter(ms) { IsCanonical = true };
            writer.WriteDomainName(key.Name);
            key.WriteData(writer);
            ms.Position = 0;
            digest = hasher.ComputeHash(ms);
        }

        Algorithm = key.Algorithm;
        Class = key.Class;
        KeyTag = key.KeyTag();
        Name = key.Name;
        Ttl = key.Ttl;
        Digest = digest;
        HashAlgorithm = DigestType.Sha1;
    }

    public ushort KeyTag { get; set; }

    public SecurityAlgorithm Algorithm { get; set; }

    public DigestType HashAlgorithm { get; set; }

    public byte[] Digest { get; set; }

    public override void ReadData(DnsReader reader, int length)
    {
        var end = reader.CurrentPosition + length;

        KeyTag = reader.ReadUInt16();
        Algorithm = (SecurityAlgorithm)reader.ReadByte();
        HashAlgorithm = (DigestType)reader.ReadByte();
        Digest = reader.ReadBytes(end - reader.CurrentPosition);
    }

    public override void WriteData(DnsWriter writer)
    {
        writer.WriteUInt16(KeyTag);
        writer.WriteByte((byte)Algorithm);
        writer.WriteByte((byte)HashAlgorithm);
        writer.WriteBytes(Digest);
    }

    public override void ReadData(MasterReader reader)
    {
        KeyTag = reader.ReadUInt16();
        Algorithm = (SecurityAlgorithm)reader.ReadByte();
        HashAlgorithm = (DigestType)reader.ReadByte();

        var sb = new StringBuilder();
        while (!reader.IsEndOfLine()) sb.Append(reader.ReadString());

        Digest = Base16Converter.ToBytes(sb.ToString());
    }

    public override void WriteData(MasterWriter writer)
    {
        writer.WriteUInt16(KeyTag);
        writer.WriteByte((byte)Algorithm);
        writer.WriteByte((byte)HashAlgorithm);
        writer.WriteBase16String(Digest, false);
    }
}