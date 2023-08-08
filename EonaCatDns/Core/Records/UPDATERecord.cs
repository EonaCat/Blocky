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

namespace EonaCat.Dns.Core.Records
{
    public class UpdateRecord : RecordBase
    {
        public ushort Id { get; set; }

        public bool Qr { get; set; }

        public bool IsUpdate
        { get { return !Qr; } }

        public bool IsResponse
        { get { return Qr; } }

        public OperationCode Opcode { get; set; } = OperationCode.Update;

        public int Z { get; set; }

        public ResponseCode Status { get; set; }

        public Question Zone { get; set; } = new()
        {
            Class = RecordClass.Internet,
            Type = RecordType.Soa
        };

        public UpdateRequirementList Requirements { get; } = new();

        public UpdateResourceList Updates { get; } = new();

        public List<ResourceRecord> AdditionalResources { get; } = new();

        public UpdateRecord CreateResponse()
        {
            return new UpdateRecord
            {
                Id = Id,
                Opcode = Opcode,
                Qr = true
            };
        }

        public override IDns Read(DnsReader reader)
        {
            Id = reader.ReadUInt16();
            var flags = reader.ReadUInt16();
            Qr = (flags & 32768) == 32768;
            Opcode = (OperationCode)((flags & 30720) >> 11);
            Z = (flags & 2032) >> 4;
            Status = (ResponseCode)(flags & 15);
            var zoneCount = reader.ReadUInt16();
            var requirementCount = reader.ReadUInt16();
            var updateCount = reader.ReadUInt16();
            var additionalResourcesCount = reader.ReadUInt16();
            for (var i = 0; i < zoneCount; i++)
            {
                Zone = (Question)new Question().Read(reader);
            }
            for (var i = 0; i < requirementCount; i++)
            {
                var resourceRecord = (ResourceRecord)new ResourceRecord().Read(reader);
                Requirements.Add(resourceRecord);
            }
            for (var i = 0; i < updateCount; i++)
            {
                var resourceRecord = (ResourceRecord)new ResourceRecord().Read(reader);
                Updates.Add(resourceRecord);
            }
            for (var i = 0; i < additionalResourcesCount; i++)
            {
                var resourceRecord = (ResourceRecord)new ResourceRecord().Read(reader);
                AdditionalResources.Add(resourceRecord);
            }

            return this;
        }

        public override void Write(DnsWriter writer)
        {
            writer.WriteUInt16(Id);

            var flags =
                Convert.ToInt32(Qr) << 15 |
                ((ushort)Opcode & 15) << 11 |
                (Z & 127) << 4 |
                (ushort)Status & 15;

            writer.WriteUInt16((ushort)flags);
            writer.WriteUInt16(1);
            writer.WriteUInt16((ushort)Requirements.Count);
            writer.WriteUInt16((ushort)Updates.Count);
            writer.WriteUInt16((ushort)AdditionalResources.Count);
            Zone.Write(writer);
            foreach (var requirement in Requirements) requirement.Write(writer);
            foreach (var update in Updates) update.Write(writer);
            foreach (var additionalResource in AdditionalResources) additionalResource.Write(writer);
        }
    }
}