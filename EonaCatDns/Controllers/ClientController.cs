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

using System.Linq;
using System.Threading.Tasks;
using EonaCat.Dns.Database;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Models;
using Microsoft.AspNetCore.Mvc;

namespace EonaCat.Dns.Controllers;

public class ClientController : ControllerBase
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

        var query = DatabaseManager.Clients.GetAll().OrderByDescending(a => a.Ip);

        if (!string.IsNullOrEmpty(search))
        {
            if (search.StartsWith("*") && search.EndsWith("*"))
            {
                query = query.Where(a => (a.Ip != null && a.Ip.ToLower().Contains(search)) ||
                                         (a.Name != null && a.Name.ToLower().Contains(search)));
            }
            else if (search.StartsWith("*"))
            {
                query = query.Where(a => (a.Ip != null && a.Ip.ToLower().EndsWith(search)) ||
                                         (a.Name != null && a.Name.ToLower().EndsWith(search)));
            }
            else if (search.EndsWith("*"))
            {
                query = query.Where(a => (a.Ip != null && a.Ip.ToLower().StartsWith(search)) ||
                                         (a.Name != null && a.Name.ToLower().StartsWith(search)));
            }
            else
            {
                query = query.Where(a => (a.Ip != null && a.Ip.ToLower().Equals(search)) ||
                                         (a.Name != null && a.Name.ToLower().Equals(search)));
            }
        }

        var totalRecords = await query.CountAsync().ConfigureAwait(false);

        var clients = await query.Skip(dataTablesRequest.Start)
            .Take(dataTablesRequest.End)
            .ToListAsync().ConfigureAwait(false);

        var result = clients.Select(client => new ClientViewModel
        {
            Ip = client.Ip,
            IsBlocked = client.IsBlocked,
            Name = client.Name
        }).OrderByDescending(x => x.Name).ToList();

        switch (dataTablesRequest.SortColumn)
        {
            case 0:
                result = isAscending
                    ? result.OrderBy(x => x.Ip).ToList()
                    : result.OrderByDescending(x => x.Ip).ToList();
                break;
            case 1:
                result = isAscending
                    ? result.OrderBy(x => x.IsBlocked).ToList()
                    : result.OrderByDescending(x => x.IsBlocked).ToList();
                break;
            case 2:
                result = isAscending
                    ? result.OrderBy(x => x.Name).ToList()
                    : result.OrderByDescending(x => x.Name).ToList();
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

        return PartialView("../Client/Index");
    }

    [HttpPost]
    public async Task<ActionResult> UpdateAsync(ClientViewModel client)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }

        if (string.IsNullOrEmpty(client.Ip))
        {
            return RedirectToAction("Index");
        }

        var databaseClient = await DatabaseManager.Clients.FirstOrDefaultAsync(x => x.Ip.Equals(client.Ip))
            .ConfigureAwait(false);
        if (databaseClient != null)
        {
            databaseClient.IsBlocked = client.IsBlocked;
            databaseClient.Name = client.Name;
        }
        else
        {
            databaseClient = new Client
            {
                IsBlocked = client.IsBlocked,
                Name = client.Name,
                Ip = client.Ip
            };
        }

        // Update the entity
        await DatabaseManager.Clients.InsertOrUpdateAsync(databaseClient).ConfigureAwait(false);

        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<ActionResult> GetByIpAsync(string ip)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }

        var isNew = string.IsNullOrEmpty(ip);

        ClientViewModel model = null;

        var client = isNew
            ? null
            : await DatabaseManager.Clients.FirstOrDefaultAsync(x => x.Ip.Equals(ip)).ConfigureAwait(false);

        if (client != null)
        {
            model = new ClientViewModel
            {
                Ip = client.Ip,
                Name = client.Name,
                IsBlocked = client.IsBlocked
            };
        }
        else if (isNew)
        {
            model = new ClientViewModel();
        }

        return PartialView("clientModal", model);
    }

    [HttpPost]
    public async Task<ActionResult> DeleteAsync(string ip)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }

        var client = await DatabaseManager.Clients.FirstOrDefaultAsync(x => x.Ip.Equals(ip)).ConfigureAwait(false);
        if (client != null)
        {
            await DatabaseManager.Clients.DeleteAsync(client).ConfigureAwait(false);
        }

        return RedirectToAction("Index");
    }
}