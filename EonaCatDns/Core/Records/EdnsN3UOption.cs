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

using System.Collections.Generic;
using EonaCat.Dns.Core.Records.Registry;

namespace EonaCat.Dns.Core.Records;

public class EdnsN3UOption : EdnsOptionBase
{
    public EdnsN3UOption()
    {
        Type = EdnsOptionType.N3U;
        Algorithms = new List<DigestType>();
    }

    public List<DigestType> Algorithms { get; set; }

    public static EdnsN3UOption Create()
    {
        var option = new EdnsN3UOption();
        option.Algorithms.AddRange(DigestRegistry.Digests.Keys);
        return option;
    }

    public override void ReadData(DnsReader reader, int length)
    {
        Algorithms.Clear();
        for (; length > 0; length--) Algorithms.Add((DigestType)reader.ReadByte());
    }

    public override void WriteData(DnsWriter writer)
    {
        foreach (var algorithm in Algorithms) writer.WriteByte((byte)algorithm);
    }

    public override string ToString()
    {
        return $";   N3U = {string.Join(", ", Algorithms)}";
    }
}