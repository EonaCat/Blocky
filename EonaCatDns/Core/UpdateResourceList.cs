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

using EonaCat.Dns.Core.Records;
using System;
using System.Collections.Generic;

namespace EonaCat.Dns.Core
{
    public class UpdateResourceList : List<ResourceRecord>
    {
        public UpdateResourceList AddResource(ResourceRecord resource)
        {
            Add(resource);
            return this;
        }

        public UpdateResourceList DeleteResource(ResourceRecord resource)
        {
            resource.Class = RecordClass.None;
            resource.Ttl = TimeSpan.Zero;
            Add(resource);
            return this;
        }

        public UpdateResourceList DeleteResource(DomainName name)
        {
            var resource = new ResourceRecord
            {
                Name = name,
                Class = RecordClass.Any,
                Type = RecordType.Any,
                Ttl = TimeSpan.Zero
            };
            Add(resource);
            return this;
        }

        public UpdateResourceList DeleteResource(DomainName name, RecordType type)
        {
            var resource = new ResourceRecord
            {
                Name = name,
                Class = RecordClass.Any,
                Type = type,
                Ttl = TimeSpan.Zero
            };
            Add(resource);
            return this;
        }

        public UpdateResourceList DeleteResource<T>(DomainName name)
             where T : ResourceRecord, new()
        {
            return DeleteResource(name, new T().Type);
        }
    }
}