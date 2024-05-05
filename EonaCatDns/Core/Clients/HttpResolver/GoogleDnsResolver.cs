using System.Linq;
using System.Threading.Tasks;

namespace EonaCat.Dns.Core.Clients.HttpResolver;

internal class GoogleDnsResolver
{
    private static bool UseGoogleDns => true;

    public static async Task<string> ResolveAsync(string host)
    {
        if (UseGoogleDns)
        {
            return "8.8.8.8";
        }

        var ipAddresses = await System.Net.Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
        return ipAddresses.FirstOrDefault()?.ToString() ?? string.Empty;
    }
}