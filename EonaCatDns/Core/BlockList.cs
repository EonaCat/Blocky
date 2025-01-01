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
using System.Linq;
using System.Threading.Tasks;
using EonaCat.Dns.Database;
using EonaCat.Logger;

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

                // Check in cache first
                if (DomainsBlockedCache.HasKey(check))
                {
                    return true;
                }

                // Fetch all matching domains from the database
                var matchingDomains = await DatabaseManager.Domains
                    .Where(x => x.Url == check)
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (!matchingDomains.Any())
                {
                    continue;
                }

                // Keep only one domain in the database
                if (matchingDomains.Count > 1)
                {
                    var domainToKeep = matchingDomains.First();
                    var domainsToDelete = matchingDomains.Skip(1).ToList();

                    // Remove extra domains from the database
                    foreach (var delete in domainsToDelete)
                    {
                        await DatabaseManager.Domains.DeleteAsync(delete).ConfigureAwait(false);
                    }

                    await Logger.LogAsync(
                        $"Deleted duplicate domains for URL '{check}', keeping ID {domainToKeep.Id}.",
                        ELogType.INFO,
                        false
                    ).ConfigureAwait(false);
                }

                var domain = matchingDomains.First();

                // Check if the domain is blocked
                if (domain.ListType != ListType.Blocked)
                {
                    continue;
                }

                // Add to cache if not already present
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