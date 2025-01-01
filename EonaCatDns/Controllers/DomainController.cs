using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Dns.Extensions;
using EonaCat.Dns.Models;
using Microsoft.AspNetCore.Mvc;
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

        var query = DatabaseManager.Domains.GetAll().OrderByDescending(a => a.Id);
        var search = dataTablesRequest.Search?.ToLower();

        if (!string.IsNullOrEmpty(search))
        {
            if (int.TryParse(search, out var id))
            {
                query = query.Where(a => a.Id == id);
            }
            else
            {
                if (search.StartsWith("*") && search.EndsWith("*"))
                {
                    query = query.Where(a => (a.Url != null && a.Url.ToLower().Contains(search)) ||
                                             (a.FromBlockList != null && a.FromBlockList.ToLower().Contains(search)));
                }
                else if (search.StartsWith("*"))
                {
                    query = query.Where(a => (a.Url != null && a.Url.ToLower().EndsWith(search)) ||
                                             (a.FromBlockList != null && a.FromBlockList.ToLower().EndsWith(search)));
                }
                else if (search.EndsWith("*"))
                {
                    query = query.Where(a => (a.Url != null && a.Url.ToLower().StartsWith(search)) ||
                                             (a.FromBlockList != null && a.FromBlockList.ToLower().StartsWith(search)));
                }
                else
                {
                    query = query.Where(a => (a.Url != null && a.Url.ToLower().Equals(search)) ||
                                             (a.FromBlockList != null && a.FromBlockList.ToLower().Equals(search)));
                }
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

        SortResult(result, dataTablesRequest.SortColumn, dataTablesRequest.SortDirection);

        return DataTableResponse.ToJson(dataTablesRequest.Echo, totalRecords, result);
    }

    private static void SortResult(List<DomainViewModel> result, int sortColumn, string sortDirection)
    {
        switch (sortColumn)
        {
            case 0:
                result = sortDirection.Equals("ASC"
, System.StringComparison.CurrentCultureIgnoreCase)
                    ? result.OrderBy(x => x.Id).ToList()
                    : result.OrderByDescending(x => x.Id).ToList();
                break;
            case 1:
                result = sortDirection.Equals("ASC"
, System.StringComparison.CurrentCultureIgnoreCase)
                    ? result.OrderBy(x => x.Url).ToList()
                    : result.OrderByDescending(x => x.Url).ToList();
                break;
            case 2:
                result = sortDirection.Equals("ASC"
, System.StringComparison.CurrentCultureIgnoreCase)
                    ? result.OrderBy(x => x.ForwardIp).ToList()
                    : result.OrderByDescending(x => x.ForwardIp).ToList();
                break;
            case 3:
                result = sortDirection.Equals("ASC"
, System.StringComparison.CurrentCultureIgnoreCase)
                    ? result.OrderBy(x => x.FromBlockList).ToList()
                    : result.OrderByDescending(x => x.FromBlockList).ToList();
                break;
            case 4:
                result = sortDirection.Equals("ASC"
, System.StringComparison.CurrentCultureIgnoreCase)
                    ? result.OrderBy(x => x.ListType).ToList()
                    : result.OrderByDescending(x => x.ListType).ToList();
                break;
            case 5:
                result = sortDirection.Equals("ASC"
, System.StringComparison.CurrentCultureIgnoreCase)
                    ? result.OrderBy(x => x.Category).ToList()
                    : result.OrderByDescending(x => x.Category).ToList();
                break;
        }
    }

    public PartialViewResult List()
    {
        return IsSessionValid() ? PartialView("../Domain/Index") : null;
    }

    [HttpPost]
    public async Task<ActionResult> UpdateAsync(DomainViewModel domain)
    {
        if (!IsSessionValid())
        {
            return RedirectToAction("Index");
        }

        if (!int.TryParse(domain.Category, out var categoryId))
        {
            categoryId = 0;
        }

        var isNew = domain.Id < 0;
        var databaseDomain = isNew
            ? new Domain()
            : await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Id == domain.Id).ConfigureAwait(false);

        if (databaseDomain != null)
        {
            databaseDomain.Url = domain.Url;
            databaseDomain.CategoryId = categoryId > 0 ? categoryId : null;
            databaseDomain.ListType = domain.ListType.Convert<ListType>();
            databaseDomain.ForwardIp = domain.ForwardIp;
        }

        await DatabaseManager.Domains.InsertOrUpdateAsync(databaseDomain).ConfigureAwait(false);
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<ActionResult> GetByIdAsync(string id)
    {
        if (!IsSessionValid())
        {
            return RedirectToAction("Index");
        }

        var isNew = string.IsNullOrEmpty(id) || Convert.ToInt32(id) < 0;

        DomainViewModel model = null;
        Domain domain = null;

        if (!isNew)
        {
            if (!int.TryParse(id, out var domainId))
            {
                return RedirectToAction("Index");
            }
            domain = await DatabaseManager.Domains.FirstOrDefaultAsync(x => x.Id.Equals(domainId)).ConfigureAwait(false);
        }

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
        else if (isNew)
        {
            model = new DomainViewModel();
            model.Id = -1;
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
            return RedirectToAction("Index");
        }

        Blocker.UpdateSetup = true;
        return RedirectToAction("Index");
    }

    [HttpPost]
    public ActionResult UpdateBlockList()
    {
        if (!IsSessionValid())
        {
            return RedirectToAction("Index");
        }

        Blocker.UpdateBlockList = true;
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<ActionResult> DeleteAsync(string id)
    {
        if (!IsSessionValid() || !int.TryParse(id, out var domainId) || domainId <= 0)
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