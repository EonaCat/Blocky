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

namespace EonaCat.Dns.Core.Records
{
    public class TsigRecord : ResourceRecord
    {
        private static readonly byte[] NoData = Array.Empty<byte>();

        public const string Hmacmd5 = "HMAC-MD5.SIG-ALG.REG.INT";

        public const string Gsstsig = "gss-tsig";

        public const string Hmacsha1 = "hmac-sha1";

        public const string Hmacsha224 = "hmac-sha224";

        public const string Hmacsha256 = "hmac-sha256";

        public const string Hmacsha384 = "hmac-sha384";

        public const string Hmacsha512 = "hmac-sha512";

        public TsigRecord()
        {
            Type = RecordType.Tsig;
            Class = RecordClass.Any;
            Ttl = TimeSpan.Zero;
            var now = DateTime.UtcNow;
            TimeSigned = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, now.Kind);
            Fudge = TimeSpan.FromSeconds(300);
            OtherData = NoData;
        }

        public DomainName Algorithm { get; set; }

        public DateTime TimeSigned { get; set; }

        public byte[] Mac { get; set; }

        public TimeSpan Fudge { get; set; }

        public ushort OriginalMessageId { get; set; }

        public ResponseCode Error { get; set; }

        public byte[] OtherData { get; set; }

        public override void ReadData(DnsReader reader, int length)
        {
            Algorithm = reader.ReadDomainName();
            TimeSigned = reader.ReadDateTime48();
            Fudge = reader.ReadTimeSpan16();
            Mac = reader.ReadUInt16LengthPrefixedBytes();
            OriginalMessageId = reader.ReadUInt16();
            Error = (ResponseCode)reader.ReadUInt16();
            OtherData = reader.ReadUInt16LengthPrefixedBytes();
        }

        public override void WriteData(DnsWriter writer)
        {
            writer.WriteDomainName(Algorithm);
            writer.WriteDateTime48(TimeSigned);
            writer.WriteTimeSpan16(Fudge);
            writer.WriteUint16LengthPrefixedBytes(Mac);
            writer.WriteUInt16(OriginalMessageId);
            writer.WriteUInt16((ushort)Error);
            writer.WriteUint16LengthPrefixedBytes(OtherData);
        }

        public override void ReadData(MasterReader reader)
        {
            Algorithm = reader.ReadDomainName();
            TimeSigned = reader.ReadDateTime();
            Fudge = reader.ReadTimeSpan16();
            Mac = Convert.FromBase64String(reader.ReadString());
            OriginalMessageId = reader.ReadUInt16();
            Error = (ResponseCode)reader.ReadUInt16();
            OtherData = Convert.FromBase64String(reader.ReadString());
        }

        public override void WriteData(MasterWriter writer)
        {
            writer.WriteDomainName(Algorithm);
            writer.WriteDateTime(TimeSigned);
            writer.WriteTimeSpan16(Fudge);
            writer.WriteBase64String(Mac);
            writer.WriteUInt16(OriginalMessageId);
            writer.WriteUInt16((ushort)Error);
            writer.WriteBase64String(OtherData ?? NoData, appendSpace: false);
        }
    }
}