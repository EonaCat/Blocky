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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace EonaCat.Dns.Core;

public class DnsReader : DnsStreamBase
{
    private readonly Dictionary<int, List<string>> _names = new();

    public DnsReader(Stream stream, byte[] originalBytes)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        OriginalBytes = originalBytes ?? throw new ArgumentNullException(nameof(originalBytes));
    }

    public bool HasOriginalBytes => OriginalBytes?.Length > 0;
    public byte[] OriginalBytes { get; set; }

    private int ReadByteInternal()
    {
        var value = Stream.ReadByte();
        if (value < 0)
        {
            throw new EndOfStreamException("Attempted to read past the end of the stream");
        }

        Position++;
        return value;
    }

    public byte ReadByte()
    {
        return (byte)ReadByteInternal();
    }

    public byte[] ReadBytes(int length)
    {
        var buffer = new byte[length];
        var bytesRead = 0;

        while (bytesRead < length)
        {
            var currentByte = Stream.Read(buffer, bytesRead, length - bytesRead);
            if (currentByte == 0)
            {
                throw new EndOfStreamException("Attempted to read past the end of the stream");
            }

            bytesRead += currentByte;
            Position += currentByte;
        }

        return buffer;
    }

    public byte[] ReadByteLengthPrefixedBytes()
    {
        int length = ReadByte();
        return ReadBytes(length);
    }

    public byte[] ReadUInt16LengthPrefixedBytes()
    {
        int length = ReadUInt16();
        return ReadBytes(length);
    }

    public ushort ReadUInt16()
    {
        return (ushort)((ReadByte() << 8) | ReadByte());
    }

    public uint ReadUInt32()
    {
        return (uint)((ReadByte() << 24) | (ReadByte() << 16) | (ReadByte() << 8) | ReadByte());
    }

    public ulong ReadUInt48()
    {
        return ((ulong)ReadByte() << 40) | ((ulong)ReadByte() << 32) | ((ulong)ReadByte() << 24) |
               ((ulong)ReadByte() << 16) | ((ulong)ReadByte() << 8) | ReadByte();
    }

    public DomainName ReadDomainName()
    {
        var labels = ReadLabels().ToList();
        return new DomainName(labels.ToArray());
    }

    private List<string> ReadLabels()
    {
        var pointer = Position;
        var length = ReadByte();

        if ((length & 192) == 192) // Compressed pointer
        {
            var compressedPointer = ((length ^ 192) << 8) | ReadByte();
            var cname = _names[compressedPointer];
            _names[pointer] = cname;
            return cname;
        }

        var labels = new List<string>();
        if (length == 0)
        {
            return labels;
        }

        var buffer = ReadBytes(length);
        labels.Add(Encoding.UTF8.GetString(buffer, 0, length));
        labels.AddRange(ReadLabels());

        _names[pointer] = labels;
        return labels;
    }

    public string ReadString()
    {
        var bytes = ReadByteLengthPrefixedBytes();
        if (bytes.Any(c => c > 127))
        {
            throw new InvalidDataException("EonaCatDns: Only ASCII characters are allowed.");
        }

        return Encoding.ASCII.GetString(bytes);
    }

    public TimeSpan ReadTimeSpan16()
    {
        return TimeSpan.FromSeconds(ReadUInt16());
    }

    public TimeSpan ReadTimeSpan32()
    {
        return TimeSpan.FromSeconds(ReadUInt32());
    }

    public IPAddress ReadIpAddress(int length = 4)
    {
        var address = ReadBytes(length);
        return new IPAddress(address);
    }

    public List<ushort> ReadBitmap()
    {
        var values = new List<ushort>();
        var block = ReadByte();
        var length = ReadByte();
        var offset = block * 256;

        for (var i = 0; i < length; i++, offset += 8)
        {
            var bits = ReadByte();

            for (var bit = 0; bit < 8; bit++)
                if ((bits & (1 << Math.Abs(bit - 7))) != 0)
                {
                    values.Add((ushort)(offset + bit));
                }
        }

        return values;
    }

    public DateTime ReadDateTime32()
    {
        var seconds = ReadUInt32();
        return UnixEpoch.AddSeconds(seconds);
    }

    public DateTime ReadDateTime48()
    {
        var seconds = ReadUInt48();
        return UnixEpoch.AddSeconds(seconds);
    }
}