using EonaCat.Json;

namespace EonaCat.Blocky.Helpers;
// Blocky
// Blocking domains the way you want it.
// Copyright EonaCat (Jeroen Saey) 2017-2023
// https://blocky.EonaCat.com

public class Payload
{
    [JsonProperty("channel")] public string Channel { get; set; }

    [JsonProperty("username")] public string Username { get; set; }

    [JsonProperty("text")] public string Text { get; set; }
}