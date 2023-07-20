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
using EonaCat.Dns.Core.Helpers;
using System.IO;

namespace EonaCat.Dns.Core
{
    public class Question : RecordBase
    {
        public DomainName Name { get; set; }

        public RecordType Type { get; set; }

        public RecordClass Class { get; set; } = RecordClass.Internet;
        public bool IsArpa => Name.ToString().EndsWith(ResolveHelper.Config.ArpaPostFix);
        public bool IsRouterDomain => Name.ToString().Contains(ResolveHelper.Config.RouterDomain);

        public override IDns Read(DnsReader reader)
        {
            Name = reader.ReadDomainName();
            Type = (RecordType)reader.ReadUInt16();
            Class = (RecordClass)reader.ReadUInt16();
            return this;
        }

        public override void Write(DnsWriter writer)
        {
            writer.WriteDomainName(Name);
            writer.WriteUInt16((ushort)Type);
            writer.WriteUInt16((ushort)Class);
        }

        public override string ToString()
        {
            using var stringWriter = new StringWriter();
            var writer = new MasterWriter(stringWriter);
            writer.WriteDomainName(Name);
            writer.WriteDnsClass(Class);
            writer.WriteDnsType(Type, appendSpace: false);
            return stringWriter.ToString();
        }
    }
}