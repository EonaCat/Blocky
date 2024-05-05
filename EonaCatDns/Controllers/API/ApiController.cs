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
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Exceptions;
using EonaCat.Json;
using Microsoft.AspNetCore.Mvc;
using BlockList = EonaCat.Dns.Core.BlockList;

namespace EonaCat.Dns.Controllers.API;

public class ApiController : ApiControllerBase
{
    private const string ResponseTag = "response";

    [HttpPost("api/stats")]
    public async Task<IActionResult> StatsAsync(string token, string type)
    {
        var httpStatusCode = HttpStatusCode.OK;

        using var memoryStream = new MemoryStream();
        using var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream));
        try
        {
            StartJsonWriter(jsonWriter);

            var isAuthenticated = IsAuthenticated(token, false);

            await jsonWriter.WritePropertyNameAsync(ResponseTag).ConfigureAwait(false);
            await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

            await EonaCatDns.Managers.ApiGetStatsAsync(type, jsonWriter, isAuthenticated, false).ConfigureAwait(false);
            await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);

            WriteOkStatus(jsonWriter);
        }
        catch (WebInvalidTokenException invalidTokenWebServiceException)
        {
            await WriteJsonTextWriterException(invalidTokenWebServiceException, jsonWriter).ConfigureAwait(false);
            httpStatusCode = invalidTokenWebServiceException.StatusCode;
        }
        catch (Exception exception)
        {
            await WriteJsonTextWriterException(exception, jsonWriter).ConfigureAwait(false);
            httpStatusCode = HttpStatusCode.BadRequest;
        }

        EndJsonWriter(jsonWriter);
        return JsonMemoryStream(memoryStream, httpStatusCode);
    }

    [HttpPost("api/allowDomain")]
    public async Task<IActionResult> AllowDomain(string token, string id)
    {
        using var memoryStream = new MemoryStream();
        using var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream));
        var httpStatusCode = HttpStatusCode.OK;

        try
        {
            StartJsonWriter(jsonWriter);

            IsAuthenticated(token);

            await jsonWriter.WritePropertyNameAsync(ResponseTag).ConfigureAwait(false);
            await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

            _ = int.TryParse(id, out var domainId);
            var isNew = domainId == 0;
            var domain = isNew
                ? null
                : await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Id == domainId).ConfigureAwait(false);
            if (domain == null)
            {
                WriteNOkStatus(jsonWriter);
            }
            else
            {
                domain.ListType = ListType.Allowed;
                await DatabaseManager.Domains.InsertOrUpdateAsync(domain).ConfigureAwait(false);
                BlockList.RemoveFromCache(domain.Url);
            }

            await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);

            WriteOkStatus(jsonWriter);
        }
        catch (WebInvalidTokenException invalidTokenWebServiceException)
        {
            await WriteJsonTextWriterException(invalidTokenWebServiceException, jsonWriter).ConfigureAwait(false);
            httpStatusCode = invalidTokenWebServiceException.StatusCode;
        }
        catch (Exception exception)
        {
            await WriteJsonTextWriterException(exception, jsonWriter).ConfigureAwait(false);
            httpStatusCode = HttpStatusCode.BadRequest;
        }

        EndJsonWriter(jsonWriter);
        return JsonMemoryStream(memoryStream, httpStatusCode);
    }

    [HttpGet("api/blocklistProgress")]
    public async Task<IActionResult> BlocklistProgress(string token, string url)
    {
        try
        {
            IsAuthenticated(token);
            var currentTask = Blocker.RunningBlockerTasks.ToList().FirstOrDefault(x => x.Uri.AbsoluteUri == url);
            if (currentTask != null)
            {
                var jsonResult = new
                {
                    currentTask.Current,
                    currentTask.Total,
                    currentTask.Progress,
                    currentTask.Status
                };

                var content = new JsonResult(jsonResult)
                {
                    ContentType = ConstantsDns.ContentType.Json
                };
                return await Task.FromResult<IActionResult>(content).ConfigureAwait(false);
            }

            var completedResult = new
            {
                Current = 100,
                Total = 100,
                Progress = 100,
                Status = "Completed",
                IsUpdating = false
            };

            var empty = new JsonResult(completedResult)
            {
                ContentType = ConstantsDns.ContentType.Json
            };
            return await Task.FromResult<IActionResult>(empty).ConfigureAwait(false);
            ;
        }
        catch (WebInvalidTokenException invalidTokenWebServiceException)
        {
            using var memoryStream = new MemoryStream();
            using var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream));
            StartJsonWriter(jsonWriter);
            await WriteJsonTextWriterException(invalidTokenWebServiceException, jsonWriter).ConfigureAwait(false);
            EndJsonWriter(jsonWriter);
            return await Task.FromResult(JsonMemoryStream(memoryStream, HttpStatusCode.Forbidden))
                .ConfigureAwait(false);
        }
    }

    [HttpPost("api/logdetails")]
    public async Task<IActionResult> LogDetails(string token, string id)
    {
        using var memoryStream = new MemoryStream();
        using var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream));
        var httpStatusCode = HttpStatusCode.OK;

        try
        {
            StartJsonWriter(jsonWriter);

            IsAuthenticated(token);

            await jsonWriter.WritePropertyNameAsync(ResponseTag).ConfigureAwait(false);
            await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

            _ = int.TryParse(id, out var logId);
            var isNew = logId == 0;
            var log = isNew
                ? null
                : await DatabaseManager.Logs.FirstOrDefaultAsync(x => x.Id == logId).ConfigureAwait(false);
            if (log == null)
            {
                WriteNOkStatus(jsonWriter);
            }
            else
            {
                await jsonWriter.WritePropertyNameAsync("details").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync(log.Raw).ConfigureAwait(false);
            }

            await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);

            WriteOkStatus(jsonWriter);
        }
        catch (WebInvalidTokenException invalidTokenWebServiceException)
        {
            await WriteJsonTextWriterException(invalidTokenWebServiceException, jsonWriter).ConfigureAwait(false);
            httpStatusCode = invalidTokenWebServiceException.StatusCode;
        }
        catch (Exception exception)
        {
            await WriteJsonTextWriterException(exception, jsonWriter).ConfigureAwait(false);
            httpStatusCode = HttpStatusCode.BadRequest;
        }

        EndJsonWriter(jsonWriter);
        return JsonMemoryStream(memoryStream, httpStatusCode);
    }

    [HttpPost("api/defaultDomain")]
    public async Task<IActionResult> DefaultDomain(string token, string id)
    {
        var httpStatusCode = HttpStatusCode.OK;
        using var memoryStream = new MemoryStream();
        using var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream));
        try
        {
            StartJsonWriter(jsonWriter);

            IsAuthenticated(token);

            await jsonWriter.WritePropertyNameAsync(ResponseTag).ConfigureAwait(false);
            await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

            _ = int.TryParse(id, out var domainId);
            var isNew = domainId == 0;
            var domain = isNew
                ? null
                : await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Id == domainId).ConfigureAwait(false);
            if (domain == null)
            {
                WriteNOkStatus(jsonWriter);
            }
            else
            {
                domain.ListType = ListType.Default;
                await DatabaseManager.Domains.InsertOrUpdateAsync(domain).ConfigureAwait(false);
                BlockList.RemoveFromCache(domain.Url);
            }

            await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);

            WriteOkStatus(jsonWriter);
        }
        catch (WebInvalidTokenException invalidTokenWebServiceException)
        {
            await WriteJsonTextWriterException(invalidTokenWebServiceException, jsonWriter).ConfigureAwait(false);
            httpStatusCode = invalidTokenWebServiceException.StatusCode;
        }
        catch (Exception exception)
        {
            await WriteJsonTextWriterException(exception, jsonWriter).ConfigureAwait(false);
            httpStatusCode = HttpStatusCode.BadRequest;
        }

        EndJsonWriter(jsonWriter);
        return JsonMemoryStream(memoryStream, httpStatusCode);
    }

    [HttpPost("api/blockClient")]
    public Task<IActionResult> BlockClient(string token, string name)
    {
        return UpdateClientAccess(token, name, true);
    }

    [HttpPost("api/allowClient")]
    public Task<IActionResult> AllowClient(string token, string name)
    {
        return UpdateClientAccess(token, name, false);
    }

    private async Task<IActionResult> UpdateClientAccess(string token, string name, bool isBlocked)
    {
        using var memoryStream = new MemoryStream();
        using var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream));
        var httpStatusCode = HttpStatusCode.OK;
        try
        {
            StartJsonWriter(jsonWriter);

            IsAuthenticated(token);

            await jsonWriter.WritePropertyNameAsync(ResponseTag).ConfigureAwait(false);
            await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

            Client client = null;
            if (IPAddress.TryParse(name, out _))
            {
                client = await DatabaseManager.Clients.FirstOrDefaultAsync(x => x.Ip == name).ConfigureAwait(false);
            }

            if (client == null)
            {
                WriteNOkStatus(jsonWriter);
            }
            else
            {
                client.IsBlocked = isBlocked;
                await DatabaseManager.Clients.InsertOrUpdateAsync(client).ConfigureAwait(false);
            }

            await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);

            WriteOkStatus(jsonWriter);
        }
        catch (WebInvalidTokenException invalidTokenWebServiceException)
        {
            await WriteJsonTextWriterException(invalidTokenWebServiceException, jsonWriter).ConfigureAwait(false);
            httpStatusCode = invalidTokenWebServiceException.StatusCode;
        }
        catch (Exception exception)
        {
            await WriteJsonTextWriterException(exception, jsonWriter).ConfigureAwait(false);
            httpStatusCode = HttpStatusCode.BadRequest;
        }

        EndJsonWriter(jsonWriter);
        return JsonMemoryStream(memoryStream, httpStatusCode);
    }

    [HttpPost("api/blockDomain")]
    public async Task<IActionResult> BlockDomain(string token, string id)
    {
        var httpStatusCode = HttpStatusCode.OK;
        using var memoryStream = new MemoryStream();
        using var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream));
        try
        {
            StartJsonWriter(jsonWriter);

            IsAuthenticated(token);

            await jsonWriter.WritePropertyNameAsync(ResponseTag).ConfigureAwait(false);
            await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

            _ = int.TryParse(id, out var domainId);
            var isNew = domainId == 0;
            var domain = isNew
                ? null
                : await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Id == domainId).ConfigureAwait(false);
            if (domain == null)
            {
                WriteNOkStatus(jsonWriter);
            }
            else
            {
                domain.ListType = ListType.Blocked;
                await DatabaseManager.Domains.InsertOrUpdateAsync(domain).ConfigureAwait(false);
                BlockList.AddToCache(domain.Url);
            }

            await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);

            WriteOkStatus(jsonWriter);
        }
        catch (WebInvalidTokenException invalidTokenWebServiceException)
        {
            await WriteJsonTextWriterException(invalidTokenWebServiceException, jsonWriter).ConfigureAwait(false);
            httpStatusCode = invalidTokenWebServiceException.StatusCode;
        }
        catch (Exception exception)
        {
            await WriteJsonTextWriterException(exception, jsonWriter).ConfigureAwait(false);
            httpStatusCode = HttpStatusCode.BadRequest;
        }

        EndJsonWriter(jsonWriter);
        return JsonMemoryStream(memoryStream, httpStatusCode);
    }

    [HttpPost("api/logs")]
    public async Task<IActionResult> Logs(string token)
    {
        var httpStatusCode = HttpStatusCode.OK;
        using var memoryStream = new MemoryStream();
        using var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream));
        try
        {
            StartJsonWriter(jsonWriter);

            IsAuthenticated(token);

            jsonWriter.WritePropertyName(ResponseTag);
            jsonWriter.WriteStartObject();

            EonaCatDns.Managers.ApiListLogs(jsonWriter);
            jsonWriter.WriteEndObject();

            WriteOkStatus(jsonWriter);
        }
        catch (WebInvalidTokenException invalidTokenWebServiceException)
        {
            await WriteJsonTextWriterException(invalidTokenWebServiceException, jsonWriter).ConfigureAwait(false);
            httpStatusCode = invalidTokenWebServiceException.StatusCode;
        }
        catch (Exception exception)
        {
            await WriteJsonTextWriterException(exception, jsonWriter).ConfigureAwait(false);
            httpStatusCode = HttpStatusCode.BadRequest;
        }

        EndJsonWriter(jsonWriter);
        return JsonMemoryStream(memoryStream, httpStatusCode);
    }

    [HttpPost("api/deleteLog")]
    public async Task<IActionResult> DeleteLog(string token, string log)
    {
        var httpStatusCode = HttpStatusCode.OK;
        using var memoryStream = new MemoryStream();
        using var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream));
        try
        {
            StartJsonWriter(jsonWriter);

            IsAuthenticated(token);

            jsonWriter.WritePropertyName(ResponseTag);
            jsonWriter.WriteStartObject();

            await Managers.Managers.ApiDeleteLog(log).ConfigureAwait(false);
            jsonWriter.WriteEndObject();

            WriteOkStatus(jsonWriter);
        }
        catch (WebInvalidTokenException invalidTokenWebServiceException)
        {
            await WriteJsonTextWriterException(invalidTokenWebServiceException, jsonWriter).ConfigureAwait(false);
            httpStatusCode = invalidTokenWebServiceException.StatusCode;
        }
        catch (Exception exception)
        {
            await WriteJsonTextWriterException(exception, jsonWriter).ConfigureAwait(false);
            httpStatusCode = HttpStatusCode.BadRequest;
        }

        EndJsonWriter(jsonWriter);
        return JsonMemoryStream(memoryStream, httpStatusCode);
    }

    [HttpGet("api/viewLog")]
    public async Task<IActionResult> ViewLog(string token, string log)
    {
        try
        {
            IsAuthenticated(token);
            await EonaCatDns.Managers.ApiLogs(ActionContext.HttpContext.Response, $"{log}.log").ConfigureAwait(false);
        }
        catch (WebInvalidTokenException)
        {
            return Forbid("Invalid token or session expired.");
        }
        catch (Exception exception)
        {
            await EonaCatDns.Managers.WriteToLog(exception.Message).ConfigureAwait(false);
            return BadRequest("Unknown error occurred, please try again later!");
        }

        return new EmptyResult();
    }
}