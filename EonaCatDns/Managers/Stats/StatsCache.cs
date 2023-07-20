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

namespace EonaCat.Dns.Managers.Stats
{
    public static class StatsCache
    {
        private static Cache.Memory.Cache _cache;

        static StatsCache()
        {
            Init();
        }

        public static void Init()
        {
            _cache = new Cache.Memory.Cache();
        }

        public static Dictionary<string, List<StatsLog>> GetCacheAsync(string statsKey)
        {
            var cacheKey = GetCacheKey(statsKey);
            if (cacheKey == null)
            {
                return null;
            }

            var cached = _cache.HasKey(cacheKey) ? _cache.Get<Dictionary<string, List<StatsLog>>>($"{cacheKey}") : null;
            return cached;
        }

        private static string GetCacheKey(string statsKey)
        {
            if (string.IsNullOrWhiteSpace(statsKey))
            {
                return null;
            }

            var cacheKey = $"{statsKey}";
            return cacheKey;
        }

        public static void SetCache(string statsKey,
            Dictionary<string, List<StatsLog>> cachedDictionary, TimeSpan? ttl = null)
        {
            var cacheKey = GetCacheKey(statsKey);
            if (cacheKey == null)
            {
                return;
            }

            var cacheTtl = TimeSpan.FromHours(1);
            if (ttl.HasValue)
            {
                cacheTtl = ttl.Value;
            }

            _cache.Set(cacheKey, cachedDictionary, cacheTtl);
        }
    }
}