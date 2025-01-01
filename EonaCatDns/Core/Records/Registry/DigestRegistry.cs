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
using System.Security.Cryptography;

namespace EonaCat.Dns.Core.Records.Registry;

public static class DigestRegistry
{
    public static Dictionary<DigestType, Func<HashAlgorithm>> Digests;

    static DigestRegistry()
    {
        Digests = new Dictionary<DigestType, Func<HashAlgorithm>>
        {
            { DigestType.Sha1, SHA1.Create },
            { DigestType.Sha256, SHA256.Create },
            { DigestType.Sha384, SHA384.Create },
            { DigestType.Sha512, SHA512.Create }
        };
    }

    public static HashAlgorithm Create(DigestType digestType)
    {
        if (Digests.TryGetValue(digestType, out var create))
        {
            return create();
        }

        throw new NotImplementedException("EonaCatDns: " + $"Digest type '{digestType}' is not implemented.");
    }

    public static HashAlgorithm Create(SecurityAlgorithm algorithm)
    {
        var metadata = SecurityAlgorithmRegistry.GetMetadata(algorithm);
        return Create(metadata.HashAlgorithm);
    }
}