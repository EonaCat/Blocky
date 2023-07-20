using EonaCat.Json;
using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;

namespace EonaCat.Blocky.Helpers
{
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
        public void PostMessage(string text, string username = null, string channel = null)
        {
            var payload = new Payload
            {
                Channel = channel,
                Username = username,
                Text = text
            };

            PostMessage(payload);
        }

        /// <summary>
        ///     Post a message using a Payload object
        /// </summary>
        /// <param name="payload"></param>
        public void PostMessage(Payload payload)
        {
            var payloadJson = JsonHelper.ToJson(payload);

            using var client = new WebClient();
            var data = new NameValueCollection
            {
                ["payload"] = payloadJson
            };

            var response = client.UploadValues(_uri, "POST", data);
            _encoding.GetString(response);
        }
    }
}