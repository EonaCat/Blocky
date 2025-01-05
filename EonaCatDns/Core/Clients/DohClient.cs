using EonaCat.Dns.Core.Clients.HttpResolver;
using EonaCat.Dns.Core.Clients;
using EonaCat.Logger;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;
using System.Net.Http.Headers;
using EonaCat.Dns;
using EonaCat.Dns.Core;

internal class DohClient : DnsClientBase
{
    private const string DnsWireFormat = "application/dns-message";
    private static readonly DnsHandler DnsHandler = new();
    private static readonly HttpClient HttpClient = new(DnsHandler) { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly MediaTypeHeaderValue DnsWireFormatHeaderValue = new MediaTypeHeaderValue(DnsWireFormat);

    private static readonly Random Random = new();

    public List<string> Servers { get; set; } = new()
    {
        "https://dns.google/dns-query",
        "https://cloudflare-dns.com/dns-query",
        "https://dns.quad9.net/dns-query"
    };

    public override async Task<Message> QueryAsync(Message request, CancellationToken cancel = default)
    {
        if (request.HasAnswers)
        {
            await Logger.LogAsync("Request already contains answers; returning as is.", ELogType.DEBUG, false).ConfigureAwait(false);
            return request;
        }

        if (request.Questions.Count == 0)
        {
            throw new Exception("EonaCatDns: Request must have at least 1 question");
        }

        Servers = Servers.OrderBy(_ => Random.Next()).ToList(); // Shuffle servers for random selection
        await Logger.LogAsync($"Shuffled DoH servers: {string.Join(", ", Servers)}", ELogType.DEBUG, false).ConfigureAwait(false);

        var responses = await QueryServersAsync(request, cancel);

        var fastestResponse = responses.FirstOrDefault(response => response != null && response.HasAnswerRecords);
        if (fastestResponse != null)
        {
            await Logger.LogAsync($"Fastest response received from server: {fastestResponse.ServerUrl}", ELogType.INFO, false).ConfigureAwait(false);
        }
        else
        {
            await Logger.LogAsync("No valid responses received from any DoH server.", ELogType.WARNING, false).ConfigureAwait(false);
        }

        return fastestResponse;
    }

    private async Task<List<Message>> QueryServersAsync(Message request, CancellationToken cancel)
    {
        var tasks = Servers.Select(server => QueryServerAsync(request, server, cancel)).ToList();
        var responses = new List<Message>();

        foreach (var task in tasks)
        {
            try
            {
                var response = await task.ConfigureAwait(false);
                if (response != null)
                {
                    responses.Add(response);
                    if (response.HasAnswerRecords)
                    {
                        break; // Stop once we have a valid response
                    }
                }
            }
            catch (Exception ex)
            {
                await Logger.LogAsync(ex, "Error querying DoH server", false).ConfigureAwait(false);
            }
        }

        return responses;
    }

    private async Task<Message> QueryServerAsync(Message request, string server, CancellationToken cancel)
    {
        var question = request.Questions.FirstOrDefault();
        if (question == null)
        {
            await Logger.LogAsync("No valid questions found in the request; skipping server query.", ELogType.WARNING, false).ConfigureAwait(false);
            return null;
        }

        try
        {
            using var memoryStream = new MemoryStream();
            request.Write(memoryStream);
            var content = new ByteArrayContent(memoryStream.ToArray());
            content.Headers.ContentType = DnsWireFormatHeaderValue;

            var response = request.CreateResponse();
            await Logger.LogAsync($"Sending DoH request for '{question.Name}' ('{question.Type}') to '{server}'", ELogType.DEBUG, false).ConfigureAwait(false);

            await GetDohResponseAsync(server, content, response, cancel).ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            await Logger.LogAsync(ex, $"Error querying DoH server: {server}", false).ConfigureAwait(false);
            return null;
        }
    }

    private static async Task GetDohResponseAsync(string server, ByteArrayContent content, Message response, CancellationToken cancel)
    {
        var question = response.Questions.FirstOrDefault();
        if (question == null)
        {
            await Logger.LogAsync("Invalid question in response; cannot process.", ELogType.ERROR, false).ConfigureAwait(false);
            return;
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, server) { Content = content };
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var httpResponse = await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
            stopwatch.Stop();

            httpResponse.EnsureSuccessStatusCode();
            response.ResponseTime = stopwatch.Elapsed;
            response.ServerUrl = server;

            if (httpResponse.Content.Headers.ContentType?.MediaType != DnsWireFormat)
            {
                throw new HttpRequestException($"Expected content-type '{DnsWireFormat}', but got '{httpResponse.Content.Headers.ContentType?.MediaType}'");
            }

            await Logger.LogAsync($"Processing DoH response for '{question.Name}' ('{question.Type}') from '{server}'", ELogType.DEBUG, false).ConfigureAwait(false);

            using var responseBodyStream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var responseMemoryStream = new MemoryStream();
            await responseBodyStream.CopyToAsync(responseMemoryStream).ConfigureAwait(false);
            responseMemoryStream.Position = 0;

            await response.Read(responseMemoryStream).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Logger.LogAsync(ex, $"Error processing DoH response from '{server}'", false).ConfigureAwait(false);
        }
    }
}
