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

namespace EonaCat.Dns.Core.Records.Registry
{
    public static class SecurityAlgorithmRegistry
    {
        public class Metadata
        {
            public DigestType HashAlgorithm { get; set; }

            public string[] OtherNames { get; set; } = Array.Empty<string>();
        }

        public static Dictionary<SecurityAlgorithm, Metadata> Algorithms;

        static SecurityAlgorithmRegistry()
        {
            Algorithms = new Dictionary<SecurityAlgorithm, Metadata>
            {
                {
                    SecurityAlgorithm.Rsasha1, new Metadata
                    {
                        HashAlgorithm = DigestType.Sha1,
                    }
                },
                {
                    SecurityAlgorithm.Rsasha256, new Metadata
                    {
                        HashAlgorithm = DigestType.Sha256,
                    }
                },
                { SecurityAlgorithm.Rsasha512, new Metadata
                {
                    HashAlgorithm = DigestType.Sha512,
                } },
                { SecurityAlgorithm.Dsa, new Metadata
                {
                    HashAlgorithm = DigestType.Sha1,
                } },
                { SecurityAlgorithm.Ecdsap256Sha256, new Metadata
                {
                    HashAlgorithm = DigestType.Sha256,
                    OtherNames = new string[] { "nistP256", "ECDSA_P256" },
                } },
                { SecurityAlgorithm.Ecdsap384Sha384, new Metadata
                {
                    HashAlgorithm = DigestType.Sha384,
                    OtherNames = new string[] { "nistP384", "ECDSA_P384" },
                } }
            };

            Algorithms.Add(SecurityAlgorithm.Rsasha1Nsec3Sha1, Algorithms[SecurityAlgorithm.Rsasha1]);
            Algorithms.Add(SecurityAlgorithm.Dsansec3Sha1, Algorithms[SecurityAlgorithm.Dsa]);
        }

        public static Metadata GetMetadata(SecurityAlgorithm algorithm)
        {
            if (Algorithms.TryGetValue(algorithm, out var metadata))
            {
                return metadata;
            }
            throw new NotImplementedException("EonaCatDns: " + $"The security algorithm '{algorithm}' is not defined.");
        }
    }
}