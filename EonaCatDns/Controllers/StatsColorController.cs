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
using EonaCat.Dns.Managers;
using EonaCat.Dns.Models;
using Microsoft.AspNetCore.Mvc;

namespace EonaCat.Dns.Controllers;

public class StatsColorController : ControllerBase
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

        var query = DatabaseManager.Settings.Where(x => x.Name.EndsWith("COLOR"))
            .OrderByDescending(a => a.Name);

        if (!string.IsNullOrEmpty(search))
        {
            if (search.StartsWith("*") && search.EndsWith("*"))
            {
                query = query.Where(a => (a.Name != null && a.Name.ToLower().Contains(search)) ||
                                         (a.Value != null && a.Value.ToLower().Contains(search)));
            }
            else if (search.StartsWith("*"))
            {
                query = query.Where(a => (a.Name != null && a.Name.ToLower().EndsWith(search)) ||
                                         (a.Value != null && a.Value.ToLower().EndsWith(search)));
            }
            else if (search.EndsWith("*"))
            {
                query = query.Where(a => (a.Name != null && a.Name.ToLower().StartsWith(search)) ||
                                         (a.Value != null && a.Value.ToLower().StartsWith(search)));
            }
            else
            {
                query = query.Where(a => (a.Name != null && a.Name.ToLower().Equals(search)) ||
                                         (a.Value != null && a.Value.ToLower().Equals(search)));
            }
        }

        var totalRecords = await query.CountAsync().ConfigureAwait(false);

        var settings = await query.Skip(dataTablesRequest.Start)
            .Take(dataTablesRequest.End)
            .ToListAsync().ConfigureAwait(false);

        var result = settings.Select(setting => new ColorViewModel
        {
            Name = setting.Name,
            Value = setting.Value
        }).OrderByDescending(x => x.Name).ToList();

        switch (dataTablesRequest.SortColumn)
        {
            case 0:
                result = isAscending
                    ? result.OrderBy(x => x.Name).ToList()
                    : result.OrderByDescending(x => x.Name).ToList();
                break;
            case 1:
                result = isAscending
                    ? result.OrderBy(x => x.Value).ToList()
                    : result.OrderByDescending(x => x.Value).ToList();
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

        return PartialView("../StatsColor/Index");
    }

    [HttpPost]
    public async Task<ActionResult> UpdateAsync(ColorViewModel color)
    {
        IsSessionValid();
        if (string.IsNullOrEmpty(color.Name))
        {
            return RedirectToAction("Index");
        }

        var databaseStatsColor = await DatabaseManager.Settings.FirstOrDefaultAsync(x => x.Name.Equals(color.Name))
            .ConfigureAwait(false);
        if (databaseStatsColor == null)
        {
            return RedirectToAction("Index");
        }

        databaseStatsColor.Value = color.Value;

        // Update the entity
        await DatabaseManager.Settings.InsertOrUpdateAsync(databaseStatsColor).ConfigureAwait(false);

        // Update the colors
        await StatsManagerApi.LoadStatsColorsAsync().ConfigureAwait(false);

        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<ActionResult> GetByNameAsync(string name)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }

        if (name == null || !name.EndsWith("COLOR"))
        {
            return RedirectToAction("Index");
        }

        ColorViewModel model = null;

        var color = await DatabaseManager.Settings.FirstOrDefaultAsync(x => x.Name.Equals(name)).ConfigureAwait(false);
        if (color != null)
        {
            model = new ColorViewModel
            {
                Name = color.Name,
                Value = color.Value
            };
        }

        return PartialView("statsColorModal", model);
    }
}