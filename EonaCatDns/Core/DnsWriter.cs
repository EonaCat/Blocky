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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace EonaCat.Dns.Core;

public class DnsWriter : DnsStreamBase
{
    private readonly Dictionary<string, int> _pointers = new();
    private readonly Stack<Stream> _scopes = new();

    public DnsWriter(Stream stream)
    {
        Stream = stream;
    }

    public bool IsCanonical { get; set; }

    public void PushLengthPrefixedScope()
    {
        _scopes.Push(Stream);
        Stream = new MemoryStream();
        Position += 2; // count the length prefix
    }

    public ushort PopLengthPrefixedScope()
    {
        var lengthPrefix = Stream;
        var length = (ushort)lengthPrefix.Position;
        Stream = _scopes.Pop();
        WriteUInt16(length);
        Position -= 2;
        lengthPrefix.Position = 0;
        lengthPrefix.CopyTo(Stream);

        return length;
    }

    public void WriteByte(byte value)
    {
        Stream.WriteByte(value);
        Position++;
    }

    public void WriteBytes(byte[] bytes)
    {
        if (bytes == null)
        {
            return;
        }

        Stream.Write(bytes, 0, bytes.Length);
        Position += bytes.Length;
    }

    public void WriteByteLengthPrefixedBytes(byte[] bytes)
    {
        var length = bytes?.Length ?? 0;
        if (length > byte.MaxValue)
        {
            throw new ArgumentException($"Length cannot exceed {byte.MaxValue}.", nameof(bytes));
        }

        WriteByte((byte)length);
        WriteBytes(bytes);
    }

    public void WriteUInt16(ushort value)
    {
        Stream.WriteByte((byte)(value >> 8));
        Stream.WriteByte((byte)value);
        Position += 2;
    }

    public void WriteUInt32(uint value)
    {
        Stream.WriteByte((byte)(value >> 24));
        Stream.WriteByte((byte)(value >> 16));
        Stream.WriteByte((byte)(value >> 8));
        Stream.WriteByte((byte)value);
        Position += 4;
    }

    public void WriteUInt48(ulong value)
    {
        Stream.WriteByte((byte)(value >> 40));
        Stream.WriteByte((byte)(value >> 32));
        Stream.WriteByte((byte)(value >> 24));
        Stream.WriteByte((byte)(value >> 16));
        Stream.WriteByte((byte)(value >> 8));
        Stream.WriteByte((byte)value);
        Position += 6;
    }

    public void WriteUint16LengthPrefixedBytes(byte[] bytes)
    {
        var length = bytes?.Length ?? 0;
        if (length > ushort.MaxValue)
        {
            throw new ArgumentException($"Bytes length cannot exceed {ushort.MaxValue}.");
        }

        WriteUInt16((ushort)length);
        WriteBytes(bytes);
    }

    public void WriteDomainName(string name, bool uncompressed = false)
    {
        if (string.IsNullOrEmpty(name))
        {
            WriteDomainName(null, uncompressed);
            return;
        }

        WriteDomainName(new DomainName(name), uncompressed);
    }

    private void WriteByteTermination()
    {
        Stream.WriteByte(0); // Terminating byte
    }

    public void WriteDomainName(DomainName name, bool uncompressed = false)
    {
        if (name == null)
        {
            WriteByteTermination();
            Position++;
            return;
        }

        if (IsCanonical)
        {
            uncompressed = true;
            name = name.ToCanonical();
        }

        var labels = name.Labels.ToArray();
        var labelsLength = labels.Length;
        for (var i = 0; i < labelsLength; i++)
        {
            var label = labels[i];
            if (label.Length > 63)
            {
                throw new ArgumentException($"Label '{label}' cannot exceed 63 octets.");
            }

            var qualifiedName = string.Join(".", labels, i, labels.Length - i);
            if (!uncompressed && _pointers.TryGetValue(qualifiedName, out var pointer))
            {
                WriteUInt16((ushort)(49152 | pointer));
                return;
            }

            if (Position <= MaxPointerLength)
            {
                _pointers[qualifiedName] = Position;
            }

            var bytes = Encoding.UTF8.GetBytes(label);
            WriteByteLengthPrefixedBytes(bytes);
        }

        WriteByteTermination();
        Position++;
    }

    public void WriteString(string value)
    {
        if (value.Any(character => character > 127))
        {
            throw new ArgumentException("Only ASCII characters are allowed.");
        }

        var bytes = Encoding.ASCII.GetBytes(value);
        WriteByteLengthPrefixedBytes(bytes);
    }

    public void WriteTimeSpan16(TimeSpan value)
    {
        WriteUInt16((ushort)value.TotalSeconds);
    }

    public void WriteTimeSpan32(TimeSpan value)
    {
        WriteUInt32((uint)value.TotalSeconds);
    }

    public void WriteDateTime32(DateTime value)
    {
        var seconds = (value.ToUniversalTime() - UnixEpoch).TotalSeconds;
        WriteUInt32(Convert.ToUInt32(seconds));
    }

    public void WriteDateTime48(DateTime value)
    {
        var seconds = (value.ToUniversalTime() - UnixEpoch).TotalSeconds;
        WriteUInt48(Convert.ToUInt64(seconds));
    }

    public void WriteIpAddress(IPAddress value)
    {
        WriteBytes(value.GetAddressBytes());
    }

    public void WriteBitmap(IEnumerable<ushort> values)
    {
        var windows = values
            .Select(v => new { Window = v / 256, Mask = new BitArray(256, false) { [v & 255] = true } })
            .GroupBy(window => window.Window)
            .Select(grouping => new
            {
                Window = grouping.Key,
                Mask = grouping.Select(w => w.Mask).Aggregate((a, b) =>
                {
                    a.Or(b);
                    return a;
                })
            })
            .OrderBy(window => window.Window)
            .ToArray();

        foreach (var window in windows)
        {
            var mask = ToBytes(window.Mask, true).ToList();
            for (var i = mask.Count - 1; i > 0; --i)
            {
                if (mask[i] != 0)
                {
                    break;
                }

                mask.RemoveAt(i);
            }

            Stream.WriteByte((byte)window.Window);
            Stream.WriteByte((byte)mask.Count);
            Position += 2;
            WriteBytes(mask.ToArray());
        }
    }

    private static IEnumerable<byte> ToBytes(BitArray bits, bool msb = false)
    {
        var bitCount = 7;
        var outByte = 0;

        foreach (bool bitValue in bits)
        {
            if (bitValue)
            {
                outByte |= msb ? 1 << bitCount : 1 << (7 - bitCount);
            }

            if (bitCount == 0)
            {
                yield return (byte)outByte;
                bitCount = 8;
                outByte = 0;
            }

            bitCount--;
        }

        if (bitCount < 7)
        {
            yield return (byte)outByte;
        }
    }
}