﻿/*
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
using System.Net;
using EonaCat.Json;
using EonaCat.Json.Linq;

namespace EonaCat.Dns.Converters;

internal class IpEndPointConverter : Converter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(IPEndPoint);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var ipEndPoint = (IPEndPoint)value;
        var jObject = new JObject
        {
            { "Address", JToken.FromObject(ipEndPoint.Address, serializer) },
            { "Port", ipEndPoint.Port }
        };
        jObject.WriteTo(writer);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var jObject = JObject.Load(reader);
        var address = jObject["Address"]?.ToObject<IPAddress>(serializer);
        var port = (int)jObject["Port"];
        return new IPEndPoint(address!, port);
    }
}