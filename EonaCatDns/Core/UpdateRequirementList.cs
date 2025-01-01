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
using EonaCat.Dns.Core.Records;

namespace EonaCat.Dns.Core;

public class UpdateRequirementList : List<ResourceRecord>
{
    public UpdateRequirementList Exist(DomainName name, RecordType type)
    {
        var resourceRecord = new ResourceRecord
        {
            Name = name,
            Type = type,
            Class = RecordClass.Any,
            Ttl = TimeSpan.Zero
        };
        Add(resourceRecord);
        return this;
    }

    public UpdateRequirementList Exist(DomainName name)
    {
        return Exist(name, RecordType.Any);
    }

    public UpdateRequirementList Exist<T>(DomainName name)
        where T : ResourceRecord, new()
    {
        return Exist(name, new T().Type);
    }

    public UpdateRequirementList Exist(ResourceRecord resource)
    {
        resource.Ttl = TimeSpan.Zero;
        Add(resource);
        return this;
    }

    public UpdateRequirementList NotExist(DomainName name, RecordType type)
    {
        var resourceRecord = new ResourceRecord
        {
            Name = name,
            Type = type,
            Class = RecordClass.None,
            Ttl = TimeSpan.Zero
        };
        Add(resourceRecord);
        return this;
    }

    public UpdateRequirementList NotExist(DomainName name)
    {
        return NotExist(name, RecordType.Any);
    }

    public UpdateRequirementList NotExist<T>(DomainName name)
        where T : ResourceRecord, new()
    {
        return NotExist(name, new T().Type);
    }
}