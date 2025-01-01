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
using System.Collections.Generic;
using System.Text;
using EonaCat.Dns.Core.Records.Registry;

namespace EonaCat.Dns.Core.Records;

public class OptRecord : ResourceRecord
{
    public OptRecord()
    {
        Type = RecordType.Opt;
        Name = DomainName.Root;
        RequestPayloadSize = 1280;
        Ttl = TimeSpan.Zero;
    }

    public ushort RequestPayloadSize
    {
        get => (ushort)Class;
        set => Class = (RecordClass)value;
    }

    public byte Opcode8
    {
        get => (byte)(((Ttl.Ticks / TimeSpan.TicksPerSecond) >> 24) & 255);
        set =>
            Ttl = TimeSpan.FromTicks(
                (((Ttl.Ticks / TimeSpan.TicksPerSecond) & ~4278190080L)
                 | ((long)value << 24))
                * TimeSpan.TicksPerSecond);
    }

    public byte Version
    {
        get => (byte)(((Ttl.Ticks / TimeSpan.TicksPerSecond) >> 16) & 255);
        set =>
            Ttl = TimeSpan.FromTicks(
                (((Ttl.Ticks / TimeSpan.TicksPerSecond) & ~16711680L)
                 | ((long)value << 16))
                * TimeSpan.TicksPerSecond);
    }

    public bool Do
    {
        get => Ttl.Ticks / TimeSpan.TicksPerSecond == 32768L;
        set =>
            Ttl = TimeSpan.FromTicks(
                (((Ttl.Ticks / TimeSpan.TicksPerSecond) & ~32768L)
                 | (Convert.ToInt64(value) << 15))
                * TimeSpan.TicksPerSecond);
    }

    public List<EdnsOptionBase> Options { get; set; } = new();

    public override void ReadData(DnsReader reader, int length)
    {
        var end = reader.CurrentPosition + length;
        while (reader.CurrentPosition < end)
        {
            var type = (EdnsOptionType)reader.ReadUInt16();
            int optionLength = reader.ReadUInt16();

            var option = EdnsOptionRegistry.Options.TryGetValue(type, out var maker)
                ? maker()
                : new UnknownEdnsOption { Type = type };
            Options.Add(option);
            option.ReadData(reader, optionLength);
        }
    }

    public override void WriteData(DnsWriter writer)
    {
        foreach (var option in Options)
        {
            writer.WriteUInt16((ushort)option.Type);

            writer.PushLengthPrefixedScope();
            option.WriteData(writer);
            writer.PopLengthPrefixedScope();
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"; EDNS: version: {Version}, udp {RequestPayloadSize}");

        foreach (var option in Options) sb.AppendLine(option.ToString());

        return sb.ToString();
    }
}