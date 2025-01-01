/*
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
using System.Linq;
using System.Threading.Tasks;
using EonaCat.Dns.Database;
using EonaCat.Dns.Models;
using EonaCat.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace EonaCat.Dns.Controllers;

public class LogsController : ControllerBase
{
    public ActionResult Index()
    {
        return RedirectToAction("Index", "Index");
    }

    public async Task<string> GetListAsync(DataTableRequest dataTablesRequest)
    {
        if (!IsSessionValid())
        {
            return "OK";
        }

        var search = dataTablesRequest.Search?.ToLower();
        var isAscending = dataTablesRequest.SortDirection?.ToUpper() == "ASC";

        var query = DatabaseManager.Logs.GetAll().OrderByDescending(a => a.Id);

        if (!string.IsNullOrEmpty(search))
        {
            if (int.TryParse(search, out var id))
            {
                query = query.Where(a => a.Id == id);
            }
            else if (DateTime.TryParse(dataTablesRequest.Search, out var dateTime))
            {
                var unixDateTime = dateTime.ToUnixTime();
                query = query.Where(a => a.DateTime == unixDateTime);
            }
            else
            {
                if (search.StartsWith("*") && search.EndsWith("*"))
                {
                    query = query.Where(a => (a.ClientIp != null && a.ClientIp.ToLower().Contains(search)) ||
                                             (a.Request != null && a.Request.ToLower().Contains(search)));
                }
                else if (search.StartsWith("*"))
                {
                    query = query.Where(a => (a.ClientIp != null && a.ClientIp.ToLower().EndsWith(search)) ||
                                             (a.Request != null && a.Request.ToLower().EndsWith(search)));
                }
                else if (search.EndsWith("*"))
                {
                    query = query.Where(a => (a.ClientIp != null && a.ClientIp.ToLower().StartsWith(search)) ||
                                             (a.Request != null && a.Request.ToLower().StartsWith(search)));
                }
                else
                {
                    query = query.Where(a => (a.ClientIp != null && a.ClientIp.ToLower().Equals(search)) ||
                                             (a.Request != null && a.Request.ToLower().Equals(search)));
                }
            }
        }

        var totalRecords = await query.CountAsync().ConfigureAwait(false);

        var logs = await query.Skip(dataTablesRequest.Start)
            .Take(dataTablesRequest.End)
            .ToListAsync().ConfigureAwait(false);

        var result = logs.Select(log => new LogViewModel
        {
            Id = log.Id,
            ClientIp = log.ClientIp,
            Date = DateTimeHelper.FromUnixTime(log.DateTime).ToString(ConstantsDns.DateTimeFormats.ClientDate),
            Request = log.Request,
            Result = log.Result.ToString()
        }).ToList();

        switch (dataTablesRequest.SortColumn)
        {
            case 0:
                result = isAscending
                    ? result.OrderBy(x => x.Id).ToList()
                    : result.OrderByDescending(x => x.Id).ToList();
                break;
            case 1:
                result = isAscending
                    ? result.OrderBy(x => x.ClientIp).ToList()
                    : result.OrderByDescending(x => x.ClientIp).ToList();
                break;
            case 2:
                result = isAscending
                    ? result.OrderBy(x => x.Date).ToList()
                    : result.OrderByDescending(x => x.Date).ToList();
                break;
            case 3:
                result = isAscending
                    ? result.OrderBy(x => x.Request).ToList()
                    : result.OrderByDescending(x => x.Request).ToList();
                break;
            case 4:
                result = isAscending
                    ? result.OrderBy(x => x.Result).ToList()
                    : result.OrderByDescending(x => x.Result).ToList();
                break;
        }

        return DataTableResponse.ToJson(dataTablesRequest.Echo, totalRecords, result);
    }


    public PartialViewResult List()
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }

        return PartialView("../BlockyLog/Index");
    }
}