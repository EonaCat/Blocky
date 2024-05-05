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

using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EonaCat.Dns.Core.Records;

public class ResourceRecord : RecordBase
{
    public DomainName Name { get; set; }

    public string CanonicalName => Name.ToCanonical().ToString();

    public RecordType Type { get; set; }

    public RecordClass Class { get; set; } = RecordClass.Internet;

    public int GetDataLength()
    {
        using var ms = new MemoryStream();
        var writer = new DnsWriter(ms);
        WriteData(writer);
        return (int)ms.Length;
    }

    public byte[] GetData()
    {
        using var ms = new MemoryStream();
        var writer = new DnsWriter(ms);
        WriteData(writer);
        return ms.ToArray();
    }

    public override async Task<IDns> Read(DnsReader reader)
    {
        Name = reader.ReadDomainName();
        Type = (RecordType)reader.ReadUInt16();
        Class = (RecordClass)reader.ReadUInt16();
        Ttl = reader.ReadTimeSpan32();
        int length = reader.ReadUInt16();

        var resourceRecord = RecordStore.Create(Type);
        resourceRecord.Name = Name;
        resourceRecord.Type = Type;
        resourceRecord.Class = Class;
        resourceRecord.Ttl = Ttl;

        // Read data of a record
        var end = reader.CurrentPosition + length;
        resourceRecord.ReadData(reader, length);
        if (reader.CurrentPosition != end)
        {
            throw new InvalidDataException("EonaCatDns: " + "Found extra data while decoding RDATA.");
        }

        return await Task.FromResult(resourceRecord).ConfigureAwait(false);
    }

    public virtual void ReadData(DnsReader reader, int length)
    {
        // Override this method
    }

    public override void Write(DnsWriter writer)
    {
        writer.WriteDomainName(Name);
        writer.WriteUInt16((ushort)Type);
        writer.WriteUInt16((ushort)Class);
        writer.WriteTimeSpan32(Ttl);

        writer.PushLengthPrefixedScope();
        WriteData(writer);
        writer.PopLengthPrefixedScope();
    }

    public virtual void WriteData(DnsWriter writer)
    {
        // Override this method
    }

    public override bool Equals(object obj)
    {
        var that = obj as ResourceRecord;
        if (that == null)
        {
            return false;
        }

        if (Name != that.Name)
        {
            return false;
        }

        if (Class != that.Class)
        {
            return false;
        }

        if (Type != that.Type)
        {
            return false;
        }

        return GetData().SequenceEqual(that.GetData());
    }

    public static bool operator ==(ResourceRecord a, ResourceRecord b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (ReferenceEquals(a, null))
        {
            return false;
        }

        if (ReferenceEquals(b, null))
        {
            return false;
        }

        return a.Equals(b);
    }

    public static bool operator !=(ResourceRecord a, ResourceRecord b)
    {
        if (ReferenceEquals(a, b))
        {
            return false;
        }

        if (ReferenceEquals(a, null))
        {
            return true;
        }

        if (ReferenceEquals(b, null))
        {
            return true;
        }

        return !a.Equals(b);
    }

    public override int GetHashCode()
    {
        return
            Name?.GetHashCode() ?? 0
            ^ Class.GetHashCode()
            ^ Type.GetHashCode()
            ^ GetData().Aggregate(0, (r, b) => r ^ b.GetHashCode());
    }

    public override string ToString()
    {
        using var s = new StringWriter();
        Write(new MasterWriter(s));

        // Trim trailing whitespaces
        var sb = s.GetStringBuilder();
        while (sb.Length > 0 && char.IsWhiteSpace(sb[^1])) sb.Length--;

        return sb.ToString();
    }

    public void Write(MasterWriter writer)
    {
        writer.WriteDomainName(Name);
        if (Ttl != TTLDefaultRecords)
        {
            writer.WriteTimeSpan32(Ttl);
        }

        writer.WriteDnsClass(Class);
        writer.WriteDnsType(Type);

        WriteData(writer);
        writer.WriteEndOfLine();
    }

    public virtual void WriteData(MasterWriter writer)
    {
        var rData = GetData();
        var hasData = rData.Length > 0;
        writer.WriteStringUnencoded("\\#");
        writer.WriteUInt32((uint)rData.Length, hasData);
        if (hasData)
        {
            writer.WriteBase16String(rData, false);
        }
    }

    public async Task<ResourceRecord> Read(string text)
    {
        return await Read(new MasterReader(new StringReader(text))).ConfigureAwait(false);
    }

    public async Task<ResourceRecord> Read(MasterReader reader)
    {
        return await reader.ReadResourceRecord().ConfigureAwait(false);
    }

    public virtual void ReadData(MasterReader reader)
    {
        // Override this method
    }
}