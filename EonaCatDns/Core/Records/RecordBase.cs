﻿/*
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
using System.IO;
using EonaCat.Dns.Core;
using EonaCat.Dns.Core.Records;

public abstract class RecordBase : IDns, ICloneable
{
    internal bool HasPacketError { get; set; }
    internal static TimeSpan TTLDefaultRecords { get; set; } = TimeSpan.FromDays(1);
    internal static TimeSpan TTLDefaultHosts { get; set; } = TimeSpan.FromDays(1);
    internal DateTime TimeCreated { get; set; } = DateTime.Now;
    internal TimeSpan Ttl { get; set; } = TTLDefaultRecords;

    private DateTime TimeExpired => TimeCreated + Ttl;

    internal bool IsExpired => HasPacketError || DateTime.Now > TimeExpired;

    public int Length()
    {
        using var ms = new MemoryStream();
        Write(new DnsWriter(ms));

        return (int)ms.Length;
    }

    public virtual object Clone()
    {
        using var ms = new MemoryStream();
        Write(ms);
        ms.Position = 0;
        var clone = (ResourceRecord)Read(ms);
        clone.TimeCreated = TimeCreated;
        return clone;
    }

    public T Clone<T>() where T : ResourceRecord
    {
        return (T)Clone();
    }

    public IDns Read(byte[] buffer)
    {
        return Read(buffer, 0, buffer.Length);
    }

    public IDns Read(byte[] buffer, int offset, int count)
    {
        using var ms = new MemoryStream(buffer, offset, count, false);
        return Read(new DnsReader(ms, buffer));
    }

    public IDns Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var buffer = ms.ToArray();
        return Read(new DnsReader(new MemoryStream(buffer), buffer));
    }

    public byte[] ToByteArray()
    {
        using var ms = new MemoryStream();
        Write(ms);
        return ms.ToArray();
    }

    public void Write(Stream stream)
    {
        Write(new DnsWriter(stream));
    }

    public abstract IDns Read(DnsReader reader);
    public abstract void Write(DnsWriter writer);
}