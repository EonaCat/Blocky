using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EonaCat.Dns.Core.Clients;
using EonaCat.Helpers.Helpers;

namespace EonaCat.Dns.Core.Helpers;

internal static class ResolveHelper
{
    internal static EonaCatDnsConfig Config { get; set; }
    public static bool HasInternet { get; set; } = true;

    internal static async Task<Message> ResolveOverDohAsync(Message message, bool isLookupTool = false)
    {
        ValidateConfig(isLookupTool);

        var resolveOverDoh = isLookupTool || Config.ResolveOverDoh;
        if (!resolveOverDoh || !(HasInternet = WebHelper.IsInternetAvailable()) || message.HasAnswers)
        {
            return message;
        }

        if (!message.HasAnswers)
        {
            var dohClient = new DohClient
            {
                Servers = isLookupTool
                    ? new List<string>
                    {
                        "https://dns.google/dns-query",
                        "https://dns.quad9.net/dns-query",
                        "https://cloudflare-dns.com/dns-query"
                    }
                    : Config.ForwardersDoH
            };

            try
            {
                var response = await dohClient.QueryAsync(message).ConfigureAwait(false);
                if (response != null && response.HasAnswers)
                {
                    response.ResolveType = ResolveType.Doh;
                    return response;
                }
            }
            catch (Exception exception)
            {
                await Logger.LogAsync(exception);
            }
        }

        if (!isLookupTool && !Config.ContinueWhenDohFails)
        {
            throw new Exception("EonaCatDns: DoH request failed");
        }

        return message;
    }

    internal static async Task<Message> ResolveOverDnsAsync(Message originalMessage, bool isLookupTool = false)
    {
        ValidateConfig(isLookupTool);

        var message = originalMessage;
        if (message.HasAnswers || !(HasInternet = WebHelper.IsInternetAvailable()))
        {
            return message;
        }

        message.ResolveType = ResolveType.Dns;
        var dnsClient = new DnsClient
        {
            Servers = isLookupTool
                ? [IPAddress.Parse("8.8.8.8")]
                : Config.ForwardersV4.Union(Config.ForwardersV6).Select(IPAddress.Parse)
        };

        message = await dnsClient.QueryAsync(message).ConfigureAwait(false);
        return message ?? originalMessage;
    }

    private static void ValidateConfig(bool isLookupTool = false)
    {
        var isValid = isLookupTool || Config != null;
        if (!isValid)
        {
            throw new Exception(
                "EonaCatDns: Please make sure that you use a valid config using the method 'WithConfig' on the server class");
        }
    }
}