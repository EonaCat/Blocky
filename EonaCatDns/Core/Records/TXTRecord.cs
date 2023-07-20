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
using System.Text;

namespace EonaCat.Dns.Core.Records
{
    public class TxtRecord : ResourceRecord
    {
        public TxtRecord()
        {
            Type = RecordType.Txt;
        }

        public List<string> Strings { get; set; } = new List<string>();

        public override void ReadData(DnsReader reader, int length)
        {
            while (length > 0)
            {
                var s = reader.ReadString();
                Strings.Add(s);
                length -= Encoding.UTF8.GetByteCount(s) + 1;
            }
        }

        public override void ReadData(MasterReader reader)
        {
            while (!reader.IsEndOfLine())
            {
                Strings.Add(reader.ReadString());
            }
        }

        public override void WriteData(DnsWriter writer)
        {
            foreach (var s in Strings)
            {
                writer.WriteString(s);
            }
        }

        public override void WriteData(MasterWriter writer)
        {
            var next = false;
            foreach (var currentString in Strings)
            {
                if (next)
                {
                    writer.WriteSpace();
                }
                writer.WriteString(currentString, appendSpace: false);
                next = true;
            }
        }
    }
}