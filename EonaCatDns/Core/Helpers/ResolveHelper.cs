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
using EonaCat.Dns.Core.Clients;
using EonaCat.Helpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace EonaCat.Dns.Core.Helpers
{
    internal static class ResolveHelper
    {
        internal static EonaCatDnsConfig Config { get; set; }
        public static bool HasInternet { get; set; } = true;

        internal static async Task<Message> ResolveOverDohAsync(Message message, bool isLookupTool = false)
        {
            IsConfigValid(isLookupTool);

            var resolveOverDoh = isLookupTool || Config.ResolveOverDoh;
            if (!resolveOverDoh || !(HasInternet = WebHelper.IsInternetAvailable()) || message.HasAnswers)
            {
                return message;
            }

            var continueWhenDohFails = isLookupTool || Config.ContinueWhenDohFails;
            if (!message.HasAnswers)
            {
                var dohClient = new DohClient
                {
                    Servers = isLookupTool ? new List<string>()
                    {
                        "https://dns.google/dns-query",
                        "https://dns.quad9.net/dns-query",
                        "https://cloudflare-dns.com/dns-query",
                    } : Config.ForwardersDoH
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
                    Logger.Log(exception);
                }
            }

            if (!continueWhenDohFails)
            {
                throw new Exception("EonaCatDns: DoH request failed");
            }

            return message;
        }

        private static void IsConfigValid(bool isLookupTool = false)
        {
            var isValid = isLookupTool || Config != null;
            if (!isValid)
            {
                throw new Exception("EonaCatDns: Please make sure that you use a valid config using the method 'WithConfig' on the server class");
            }
        }

        internal static async Task<Message> ResolveOverDnsAsync(Message originalMessage, bool isLookupTool = false)
        {
            IsConfigValid(isLookupTool);

            var message = originalMessage;
            if (message.HasAnswers || !(HasInternet = WebHelper.IsInternetAvailable()))
            {
                return message;
            }

            message.ResolveType = ResolveType.Dns;
            var dnsClient = new DnsClient
            {
                Servers = isLookupTool ? new[] { IPAddress.Parse("8.8.8.8") } : Config.ForwardersV4.Union(Config.ForwardersV6).Select(IPAddress.Parse)
            };

            message = await dnsClient.QueryAsync(message).ConfigureAwait(false);
            return message ?? originalMessage;
        }
    }
}
