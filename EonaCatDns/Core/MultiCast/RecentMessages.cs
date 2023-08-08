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
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;

namespace EonaCat.Dns.Core.MultiCast
{
    public class RecentMessages
    {
        public ConcurrentDictionary<string, DateTime> Messages = new();

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

        public bool TryAdd(byte[] message)
        {
            Cleanup();
            return Messages.TryAdd(GetId(message), DateTime.Now);
        }

        public int Cleanup()
        {
            var dead = DateTime.Now - Interval;
            return Messages.Where(x => x.Value < dead).Count(stale => Messages.TryRemove(stale.Key, out _));
        }

        public string GetId(byte[] message)
        {
            using HashAlgorithm hasher = SHA256.Create();
            return Convert.ToBase64String(hasher.ComputeHash(message));
        }
    }
}