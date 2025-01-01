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
using System.IO;

namespace EonaCat.Dns.Core.Helpers;

internal class RootHintHelper
{
    public static List<RootHintEntry> Parse(Stream streamReader)
    {
        using var reader = new StreamReader(streamReader);
        var entries = new List<RootHintEntry>();
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith(";") || line.Trim().Length == 0)
            {
                continue;
            }

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 4)
            {
                throw new ArgumentException("EonaCatDns: " + "Invalid line: " + line);
            }

            var entry = new RootHintEntry
            {
                Name = parts[0],
                Ttl = int.Parse(parts[1]),
                Type = parts[2],
                Value = parts[3]
            };

            entries.Add(entry);
        }

        return entries;
    }
}