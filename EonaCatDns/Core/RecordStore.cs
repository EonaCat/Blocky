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
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EonaCat.Logger;

namespace EonaCat.Dns.Core
{
    public static class RecordStore
    {
        private static readonly Dictionary<RecordType, Func<ResourceRecord>> Records;

        static RecordStore()
        {
            Records = new Dictionary<RecordType, Func<ResourceRecord>>();
            AddRecordsFromAssembly(Assembly.GetExecutingAssembly());
        }

        private static void AddRecordsFromAssembly(Assembly assembly)
        {
            var recordTypes = assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ResourceRecord)) && !type.IsAbstract);
            foreach (var recordType in recordTypes)
            {
                try
                {
                    var constructor = recordType.GetConstructor(Type.EmptyTypes);
                    if (constructor == null)
                    {
                        throw new ArgumentException(
                            $"EonaCatDns: The {recordType.Name} class does not have a parameterless constructor.");
                    }

                    var createInstance = Expression.Lambda<Func<ResourceRecord>>(Expression.New(constructor)).Compile();
                    var resourceRecord = createInstance();
                    if (resourceRecord.Type == 0)
                    {
                        throw new ArgumentException(
                            $"EonaCatDns: The {recordType.Name} class does not define the resource record type.");
                    }

                    Records.Add(resourceRecord.Type, createInstance);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message, ELogType.CRITICAL);
                }
            }
        }

        public static ResourceRecord Create(RecordType type)
        {
            return Records.TryGetValue(type, out var create) ? create() : new UnknownRecord();
        }
    }
}
