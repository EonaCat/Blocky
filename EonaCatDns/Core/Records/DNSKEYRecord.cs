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

using EonaCat.Dns.Core.Records.Registry;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace EonaCat.Dns.Core.Records
{
    public class DnskeyRecord : ResourceRecord
    {
        public DnskeyRecord()
        {
            Type = RecordType.Dnskey;
        }

        public DnskeyRecord(RSA key, SecurityAlgorithm algorithm)
            : this()
        {
            switch (algorithm)
            {
                case SecurityAlgorithm.Rsamd5:
                case SecurityAlgorithm.Rsasha1:
                case SecurityAlgorithm.Rsasha1Nsec3Sha1:
                case SecurityAlgorithm.Rsasha256:
                case SecurityAlgorithm.Rsasha512:
                    break;

                default:
                    throw new ArgumentException("EonaCatDns: " + $"Security algorithm '{algorithm}' is not allowed for a RSA key.");
            }
            Algorithm = algorithm;

            using var ms = new MemoryStream();
            var parameters = key.ExportParameters(includePrivateParameters: false);
            ms.WriteByte((byte)parameters.Exponent.Length);
            ms.Write(parameters.Exponent, 0, parameters.Exponent.Length);
            ms.Write(parameters.Modulus, 0, parameters.Modulus.Length);
            PublicKey = ms.ToArray();
        }

        public DnskeyRecord(ECDsa key)
            : this()
        {
            var parameters = key.ExportParameters(includePrivateParameters: false);
            parameters.Validate();

            if (!parameters.Curve.IsNamed)
            {
                throw new ArgumentException("EonaCatDns: " + "Only named ECDSA curves are allowed.");
            }

            Algorithm = SecurityAlgorithmRegistry.Algorithms
                .Where(alg => alg.Value.OtherNames.Contains(parameters.Curve.Oid.FriendlyName))
                .Select(alg => alg.Key)
                .FirstOrDefault();
            if (Algorithm == 0)
            {
                throw new ArgumentException("EonaCatDns: " + $"ECDSA curve '{parameters.Curve.Oid.FriendlyName} is not known'.");
            }

            // ECDSA public keys consist of a single value, called "Q" in FIPS 186-3.
            // In DNSSEC keys, Q is a bit string that represents the uncompressed form of X and Y.
            using var ms = new MemoryStream();
            ms.Write(parameters.Q.X, 0, parameters.Q.X.Length);
            ms.Write(parameters.Q.Y, 0, parameters.Q.Y.Length);
            PublicKey = ms.ToArray();
        }

        public DnsKeyFlags Flags { get; set; }

        public byte Protocol { get; set; } = 3;

        public SecurityAlgorithm Algorithm { get; set; }

        public byte[] PublicKey { get; set; }

        public ushort KeyTag()
        {
            var key = GetData();
            var length = key.Length;
            var ac = 0;

            for (var i = 0; i < length; i++)
            {
                ac += (i & 1) == 1 ? key[i] : key[i] << 8;
            }
            ac += ac >> 16 & 65535;
            return (ushort)(ac & 65535);
        }

        public override void ReadData(DnsReader reader, int length)
        {
            var end = reader.CurrentPosition + length;

            Flags = (DnsKeyFlags)reader.ReadUInt16();
            Protocol = reader.ReadByte();
            Algorithm = (SecurityAlgorithm)reader.ReadByte();
            PublicKey = reader.ReadBytes(end - reader.CurrentPosition);
        }

        public override void WriteData(DnsWriter writer)
        {
            writer.WriteUInt16((ushort)Flags);
            writer.WriteByte(Protocol);
            writer.WriteByte((byte)Algorithm);
            writer.WriteBytes(PublicKey);
        }

        public override void ReadData(MasterReader reader)
        {
            Flags = (DnsKeyFlags)reader.ReadUInt16();
            Protocol = reader.ReadByte();
            Algorithm = (SecurityAlgorithm)reader.ReadByte();
            PublicKey = reader.ReadBase64String();
        }

        public override void WriteData(MasterWriter writer)
        {
            writer.WriteUInt16((ushort)Flags);
            writer.WriteByte(Protocol);
            writer.WriteByte((byte)Algorithm);
            writer.WriteBase64String(PublicKey, appendSpace: false);
        }
    }
}