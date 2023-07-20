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

using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Dns.Extensions;
using EonaCat.Dns.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain = EonaCat.Dns.Database.Models.Entities.Domain;

namespace EonaCat.Dns.Controllers;

public class DomainController : ControllerBase
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

        var query = DatabaseManager.Domains.GetAll().OrderByDescending(a => a.Id);

        if (!string.IsNullOrEmpty(search))
        {
            if (int.TryParse(search, out var id))
            {
                query = query.Where(a => a.Id == id);
            }
            else
            {
                query = query.Where(a => (a.Url != null && a.Url.ToLower().Equals(search)) ||
                                         (a.FromBlockList != null && a.FromBlockList.Contains(search)));
            }
        }

        var totalRecords = await query.CountAsync().ConfigureAwait(false);

        var domains = await query.Skip(dataTablesRequest.Start)
                                 .Take(dataTablesRequest.End)
                                 .ToListAsync().ConfigureAwait(false);

        var result = domains.Select(d => new DomainViewModel
        {
            Id = d.Id,
            Url = d.Url,
            ForwardIp = d.ForwardIp,
            FromBlockList = !string.IsNullOrEmpty(d.FromBlockList) ? d.FromBlockList : DllInfo.ApplicationName,
            ListType = d.ListType.ToString(),
            Category = d.Category != null ? d.Category.Name : string.Empty
        }).ToList();

        switch (dataTablesRequest.SortColumn)
        {
            case 0 when isAscending:
                result = result.OrderBy(x => x.Id).ToList();
                break;
            case 0:
                result = result.OrderByDescending(x => x.Id).ToList();
                break;
            case 1 when isAscending:
                result = result.OrderBy(x => x.Url).ToList();
                break;
            case 1:
                result = result.OrderByDescending(x => x.Url).ToList();
                break;
            case 2 when isAscending:
                result = result.OrderBy(x => x.ForwardIp).ToList();
                break;
            case 2:
                result = result.OrderByDescending(x => x.ForwardIp).ToList();
                break;
            case 3 when isAscending:
                result = result.OrderBy(x => x.FromBlockList).ToList();
                break;
            case 3:
                result = result.OrderByDescending(x => x.FromBlockList).ToList();
                break;
            case 4 when isAscending:
                result = result.OrderBy(x => x.ListType).ToList();
                break;
            case 4:
                result = result.OrderByDescending(x => x.ListType).ToList();
                break;
            case 5 when isAscending:
                result = result.OrderBy(x => x.Category).ToList();
                break;
            case 5:
                result = result.OrderByDescending(x => x.Category).ToList();
                break;
        }

        return DataTableResponse.ToJson(dataTablesRequest.Echo, totalRecords, result);
    }


    public PartialViewResult List()
    {
        if (IsSessionValid())
        {
            return PartialView("../Domain/Index");
        }

        RedirectToAction("Index");
        return null;
    }

    [HttpPost]
    public async Task<ActionResult> UpdateAsync(DomainViewModel domain)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }

        int.TryParse(domain.Category, out var categoryId);
        var isNew = domain.Id == 0;

        Domain databaseDomain = null;
        if (!isNew)
        {
            databaseDomain = await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Id == domain.Id).ConfigureAwait(false);
        }

        if (databaseDomain != null)
        {
            databaseDomain.Url = domain.Url;
            databaseDomain.CategoryId = categoryId > 0 ? categoryId : null;
            databaseDomain.ListType = domain.ListType.Convert<ListType>();
            databaseDomain.ForwardIp = domain.ForwardIp;
        }
        else
        {
            databaseDomain = new Domain
            {
                Url = domain.Url,
                CategoryId = categoryId > 0 ? categoryId : null,
                ForwardIp = domain.ForwardIp,
                ListType = domain.ListType.Convert<ListType>()
            };
        }

        // Update the entity
        await DatabaseManager.Domains.InsertOrUpdateAsync(databaseDomain).ConfigureAwait(false);
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<ActionResult> GetByIdAsync(string id)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }
        int.TryParse(id, out var domainId);
        var isNew = domainId == 0;

        var model = new DomainViewModel();
        var domain = isNew ? null : await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Id == domainId).ConfigureAwait(false);

        if (domain != null)
        {
            model = new DomainViewModel
            {
                Id = domain.Id,
                Category = domain.CategoryId != null ? domain.Category.Name : string.Empty,
                ForwardIp = domain.ForwardIp,
                ListType = domain.ListType.ToString(),
                FromBlockList = domain.FromBlockList,
                Url = domain.Url
            };
        }

        model.Categories = (await DatabaseManager.Categories.GetAll().ToListAsync().ConfigureAwait(false))
            .Select(x => new KeyValuePair<string, long>(x.Name, x.Id)).ToList();

        return PartialView("domainModal", model);
    }



    [HttpPost]
    public ActionResult UpdateSetup()
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }
        Blocker.UpdateSetup = true;
        return RedirectToAction("Index");
    }

    [HttpPost]
    public ActionResult UpdateBlockList()
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }
        Blocker.UpdateBlockList = true;
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<ActionResult> DeleteAsync(string id)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }
        int.TryParse(id, out var domainId);
        if (domainId <= 0)
        {
            return RedirectToAction("Index");
        }

        var domain = await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Id == domainId).ConfigureAwait(false);
        if (domain != null)
        {
            await DatabaseManager.Domains.DeleteAsync(domain).ConfigureAwait(false);
        }

        return RedirectToAction("Index");
    }
}