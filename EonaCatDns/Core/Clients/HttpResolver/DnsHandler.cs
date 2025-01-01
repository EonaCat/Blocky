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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EonaCat.Dns.Core.Clients.HttpResolver;

internal class DnsHandler : HttpClientHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.RequestUri == null)
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            var host = request.RequestUri.Host;
            var ip = await GoogleDnsResolver.ResolveAsync(host).ConfigureAwait(false);

            var builder = new UriBuilder(request.RequestUri)
            {
                Host = ip
            };

            request.RequestUri = builder.Uri;

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Logger.LogAsync(ex);
            return null;
        }
    }
}