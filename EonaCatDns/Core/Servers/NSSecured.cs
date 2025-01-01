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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EonaCat.Dns.Core.Records;

namespace EonaCat.Dns.Core.Servers;

// DNSSEC name server.
internal partial class Ns
{
    protected async Task<Message> AddSecurityExtensionsAsync(Message request, Message response)
    {
        // If the requests is not for DNSSEC just return.
        if (!request.IsDnsSecSupported)
        {
            return response;
        }

        response.IsDnsSecSupported = true;

        await AddSecurityResourcesAsync(response.Answers).ConfigureAwait(false);
        await AddSecurityResourcesAsync(response.AuthorityRecords).ConfigureAwait(false);
        await AddSecurityResourcesAsync(response.AdditionalRecords).ConfigureAwait(false);

        return response;
    }

    private async Task AddSecurityResourcesAsync(List<ResourceRecord> resourceRecords)
    {
        var neededSignatures = resourceRecords
            .Where(r => r.CanonicalName != string.Empty) // ignore pseudo records
            .GroupBy(r => new { r.CanonicalName, r.Type, r.Class })
            .Select(g => g.First());

        foreach (var need in neededSignatures)
        {
            var signatures = new Message();
            var question = new Question { Name = need.Name, Class = need.Class, Type = RecordType.Rrsig };

            if (!await GetAnswerFromCatalogAsync(question, signatures, CancellationToken.None)
                    .ConfigureAwait(false))
            {
                continue;
            }

            resourceRecords.AddRange(signatures.Answers
                .OfType<RrsigRecord>()
                .Where(r => r.TypeCovered == need.Type));
        }
    }
}