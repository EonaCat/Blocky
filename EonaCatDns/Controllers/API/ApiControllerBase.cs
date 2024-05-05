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

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EonaCat.Dns.Exceptions;
using EonaCat.Json;
using Microsoft.AspNetCore.Mvc;

namespace EonaCat.Dns.Controllers.API;

[Controller]
public abstract class ApiControllerBase : Controller
{
    [ActionContext] public ActionContext ActionContext { get; set; }

    internal static bool IsAuthenticated(string token, bool throwExceptionIfNotAuthenticated = true)
    {
        var isAuthenticated = EonaCatDns.Managers.ApiIsSessionValid(token);
        if (throwExceptionIfNotAuthenticated && !isAuthenticated)
        {
            throw new WebInvalidTokenException("EonaCatDns: Invalid token or session expired.");
        }

        return isAuthenticated;
    }

    internal static void WriteOkStatus(JsonTextWriter jsonWriter)
    {
        jsonWriter.WritePropertyName("status");
        jsonWriter.WriteValue("ok");
    }

    internal static void WriteNOkStatus(JsonTextWriter jsonWriter)
    {
        jsonWriter.WritePropertyName("status");
        jsonWriter.WriteValue("nok");
    }

    internal static void StartJsonWriter(JsonTextWriter jsonWriter)
    {
        jsonWriter.WriteStartObject();
    }

    internal static void EndJsonWriter(JsonTextWriter jsonWriter)
    {
        // Write default values
        jsonWriter.WritePropertyName("name");
        jsonWriter.WriteValue(DllInfo.Name);

        jsonWriter.WritePropertyName("version");
        jsonWriter.WriteValue(DllInfo.Version);
        jsonWriter.WriteEndObject();

        jsonWriter.Flush();
    }

    internal static string ReturnMemoryStreamAsString(MemoryStream memoryStream)
    {
        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    protected IActionResult JsonMemoryStream(MemoryStream memoryStream, HttpStatusCode httpStatusCode)
    {
        var content = Content(ReturnMemoryStreamAsString(memoryStream));
        content.ContentType = ConstantsDns.ContentType.Json;
        content.StatusCode = (int)httpStatusCode;
        return content;
    }

    internal static async Task WriteJsonTextWriterException(Exception exception, JsonTextWriter jsonWriter)
    {
        if (exception is WebInvalidTokenException)
        {
            jsonWriter.WritePropertyName("status");
            jsonWriter.WriteValue("invalid-token");

            jsonWriter.WritePropertyName("errorMessage");
            jsonWriter.WriteValue(exception.Message);
        }
        else
        {
            await EonaCatDns.Managers.WriteToLog(exception).ConfigureAwait(false);

            jsonWriter.WritePropertyName("status");
            jsonWriter.WriteValue("error");

            jsonWriter.WritePropertyName("errorMessage");
            jsonWriter.WriteValue(exception.Message);

            if (!Debugger.IsAttached)
            {
                return;
            }

            jsonWriter.WritePropertyName("stackTrace");
            jsonWriter.WriteValue(exception.StackTrace);
        }
    }
}