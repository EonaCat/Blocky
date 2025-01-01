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

namespace EonaCat.Dns.Core.Records;

public class EdnsKeepaliveOption : EdnsOptionBase
{
    public EdnsKeepaliveOption()
    {
        Type = EdnsOptionType.Keepalive;
    }

    public TimeSpan? Timeout { get; set; }

    public override void ReadData(DnsReader reader, int length)
    {
        switch (length)
        {
            case 0:
                Timeout = null;
                break;

            case 2:
                Timeout = TimeSpan.FromMilliseconds(reader.ReadUInt16() * 100);
                break;

            default:
                throw new InvalidDataException("EonaCatDns: " + $"Invalid EdnsKeepAlive length of '{length}'.");
        }
    }

    public override void WriteData(DnsWriter writer)
    {
        if (Timeout.HasValue)
        {
            writer.WriteUInt16((ushort)(Timeout.Value.TotalMilliseconds / 100));
        }
    }

    public override string ToString()
    {
        return $";   Keepalive = {Timeout}";
    }
}