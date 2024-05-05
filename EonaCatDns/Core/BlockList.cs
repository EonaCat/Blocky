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
using System.Threading.Tasks;
using EonaCat.Dns.Database;

namespace EonaCat.Dns.Core;

internal class BlockList
{
    private const int CacheTime = 15;
    private static readonly Cache.Memory.Cache DomainsBlockedCache = new();

    public static void RemoveFromCache(string host)
    {
        if (DomainsBlockedCache.HasKey(host))
        {
            DomainsBlockedCache.Remove(host);
        }
    }

    public static void AddToCache(string host)
    {
        if (!DomainsBlockedCache.HasKey(host))
        {
            DomainsBlockedCache.Set(host, true, TimeSpan.FromMinutes(CacheTime));
        }
    }

    public static async Task<bool> MatchAsync(string host)
    {
        try
        {
            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var partsSpan = new ReadOnlyMemory<string>(parts);

            for (var i = parts.Length; i >= 1; i--)
            {
                var check = string.Join(".", partsSpan.Slice(0, i).ToArray());

                if (DomainsBlockedCache.HasKey(check))
                {
                    return true;
                }

                var domain = await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Url == check)
                    .ConfigureAwait(false);

                if (domain == null || domain.ListType != ListType.Blocked)
                {
                    continue;
                }

                if (!DomainsBlockedCache.HasKey(host))
                {
                    DomainsBlockedCache.Set(check, true, TimeSpan.FromMinutes(CacheTime));
                }

                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            await Logger.LogAsync(e, $"BlockList match {host}").ConfigureAwait(false);
            return false;
        }
    }
}