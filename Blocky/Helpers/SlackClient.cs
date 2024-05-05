using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using EonaCat.Json;

namespace EonaCat.Blocky.Helpers;
// Blocky
// Blocking domains the way you want it.
// Copyright EonaCat (Jeroen Saey) 2017-2023
// https://blocky.EonaCat.com

public class SlackClient
{
    private readonly Encoding _encoding = new UTF8Encoding();
    private readonly Uri _uri;

    /// <summary>
    ///     SlackClient
    /// </summary>
    /// <param name="urlWithAccessToken"></param>
    public SlackClient(string urlWithAccessToken)
    {
        _uri = new Uri(urlWithAccessToken);
    }

    /// <summary>
    ///     Post a message using simple strings
    /// </summary>
    /// <param name="text"></param>
    /// <param name="username"></param>
    /// <param name="channel"></param>
    public async Task PostMessageAsync(string text, string username = null, string channel = null)
    {
        var payload = new Payload
        {
            Channel = channel,
            Username = username,
            Text = text
        };

        await PostMessageAsync(payload).ConfigureAwait(false);
    }

    /// <summary>
    ///     Post a message using a Payload object
    /// </summary>
    /// <param name="payload"></param>
    public async Task<string> PostMessageAsync(Payload payload)
    {
        var payloadJson = JsonHelper.ToJson(payload);
        var data = new StringContent($"payload={payloadJson}", Encoding.UTF8, "application/x-www-form-urlencoded");

        using var client = new HttpClient();
        var response = await client.PostAsync(_uri, data).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }
}