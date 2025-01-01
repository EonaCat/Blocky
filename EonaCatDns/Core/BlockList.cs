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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EonaCat.Dns.Database;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Logger;

namespace EonaCat.Dns.Core
{
    internal class BlockList
    {
        private const int CacheTime = 1;
        private static readonly Cache.Memory.Cache DomainsBlockedCache = new();

        public static void RemoveFromCache(string host)
        {
            DomainsBlockedCache.Remove(host);
        }

        public static void AddToCache(string host)
        {
            // Only add to cache if not present
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

                // Loop through domain parts in reverse order
                for (var i = parts.Length; i >= 1; i--)
                {
                    var check = string.Join(".", partsSpan.Slice(0, i).ToArray());

                    // Check if the domain is already cached
                    if (DomainsBlockedCache.HasKey(check))
                    {
                        // Fetch the domain from the cache
                        var cachedDomain = await CheckIfDomainIsAllowed(check);

                        // If the domain is allowed, return false (allow the domain)
                        if (cachedDomain != null)
                        {
                            return false;
                        }
                        return true;
                    }

                    // Fetch the first matching domain from the database
                    var domain = await DatabaseManager.Domains
                        .Where(x => IsMatch(x.Url, check))
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);

                    // No matching domains found, skip
                    if (domain == null)
                    {
                        continue;
                    }

                    // Clean up duplicate domains if necessary
                    await CleanUpDuplicateDomainsAsync(check, domain);

                    // Check if the domain is blocked
                    if (domain.ListType != ListType.Blocked)
                    {
                        continue;
                    }

                    // Add to cache if not already present
                    if (!DomainsBlockedCache.HasKey(check))
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

        private static async Task<Domain> CheckIfDomainIsAllowed(string check)
        {
            if (DomainsBlockedCache.HasKey(check))
            {
                return await DatabaseManager.Domains
                    .Where(x => IsMatch(x.Url, check) && x.ListType == ListType.Allowed)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
            }

            return null;
        }

        private static async Task CleanUpDuplicateDomainsAsync(string check, Domain domainToKeep)
        {
            // Fetch all domains matching the URL pattern
            var matchingDomains = await DatabaseManager.Domains
                .Where(x => x.Url == check)
                .ToListAsync()
                .ConfigureAwait(false);

            if (matchingDomains.Count <= 1)
            {
                return;
            }

            // Delete duplicate domains
            var domainsToDelete = matchingDomains.Where(d => d.Id != domainToKeep.Id).ToList();
            foreach (var delete in domainsToDelete)
            {
                await DatabaseManager.Domains.DeleteAsync(delete).ConfigureAwait(false);
            }

            // Log deletion of duplicate domains
            await Logger.LogAsync(
                $"Deleted {domainsToDelete.Count} duplicated domains for URL '{check}', keeping ID {domainToKeep.Id}.",
                ELogType.INFO,
                false
            ).ConfigureAwait(false);
        }

        private static bool IsMatch(string pattern, string check)
        {
            try
            {
                // Perform regex matching only if pattern is valid
                return IsRegex(pattern) ? Regex.IsMatch(check, pattern) : string.Equals(pattern, check, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Fall back to exact match if regex fails
                return string.Equals(pattern, check, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool IsRegex(string pattern)
        {
            try
            {
                new Regex(pattern);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
