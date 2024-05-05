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