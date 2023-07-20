﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EonaCat.Dns.Core.Clients.HttpResolver;
using EonaCat.Helpers.Retry;
using EonaCat.Logger;

namespace EonaCat.Dns.Core.Clients
{
    internal class DohClient : DnsClientBase
    {
        private static readonly DnsHandler DnsHandler = new DnsHandler(new GoogleDnsResolver());
        private static readonly HttpClient HttpClient = new HttpClient(DnsHandler);

        public const string DnsWireFormat = "application/dns-message";
        public const string DnsJsonFormat = "application/dns-json";

        public List<string> Servers { get; set; } = new List<string>
        {
            "https://dns.google/dns-query",
            "https://cloudflare-dns.com/dns-query",
            "https://dns.quad9.net/dns-query",
        };

        public override async Task<Message> QueryAsync(Message request, CancellationToken cancel = default)
        {
            if (request.HasAnswers)
            {
                return request;
            }

            if (request.Questions.Count == 0)
            {
                throw new Exception("EonaCatDns: Request must have at least 1 question");
            }

            RetryHelper.Instance.DefaultMaxTryCount = 3;
            RetryHelper.Instance.DefaultMaxTryTime = TimeSpan.FromSeconds(3);
            RetryHelper.Instance.DefaultTryInterval = TimeSpan.FromMilliseconds(100);

            Message fastestResponse = null;

            // Post the request.
            using var memoryStream = new MemoryStream();
            request.Write(memoryStream);
            var content = new ByteArrayContent(memoryStream.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue(DnsWireFormat);

            foreach (var server in Servers)
            {
                var response = request.CreateResponse();

                // Run the request in a separate thread
                await Task.Run(async () =>
                {
                    var result = await GetDohResponseAsync(server, content, response).ConfigureAwait(false);

                    // Only update the fastest response if it is the first one
                    if (result != null && result.HasAnswerRecords && fastestResponse == null)
                    {
                        fastestResponse = result;
                    }
                }, cancel).ConfigureAwait(false);
                
                if (fastestResponse != null)
                {
                    break;
                }
            }

            // Return the fastest response
            return fastestResponse;
        }

        private static async Task<Message> GetDohResponseAsync(string server, ByteArrayContent content, Message response)
        {
            var question = response.Questions.FirstOrDefault();
            if (question == null)
            {
                // Invalid question
                return null;
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, server) { Content = content };
            try
            {
                var stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
                var httpResponse = await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                stopwatch.Stop();

                httpResponse.EnsureSuccessStatusCode();
                response.ResponseTime = stopwatch.Elapsed;
                response.ServerUrl = server;

                if (httpResponse.Content.Headers.ContentType?.MediaType != DnsWireFormat)
                {
                    throw new HttpRequestException("EonaCatDns: " + $"#{response.Header.Id} Expected content-type '{DnsWireFormat}' not '{httpResponse.Content.Headers.ContentType?.MediaType}'.");
                }

                Logger.Log($"Reading DoH response for '{question.Name}' ('{question.Type}') via '{server}'", ELogType.DEBUG, writeToConsole: false);

                // Read the response into a new MemoryStream
                using (var responseBodyStream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    var responseMemoryStream = new MemoryStream();
                    await responseBodyStream.CopyToAsync(responseMemoryStream).ConfigureAwait(false);
                    responseMemoryStream.Position = 0;

                    // Use the new MemoryStream to read the response
                    response.Read(responseMemoryStream);
                }
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException("EonaCatDns: " + $"#{response.Header.Id} Error occurred while sending the request.", ex);
            }

            return response;
        }
    }
}