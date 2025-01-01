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

using System.Linq;
using System.Threading.Tasks;
using EonaCat.Dns.Database;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Models;
using Microsoft.AspNetCore.Mvc;

namespace EonaCat.Dns.Controllers;

public class CategoryController : ControllerBase
{
    public ActionResult Index()
    {
        return RedirectToAction("Index", "Index");
    }

    public async Task<string> GetListAsync(DataTableRequest dataTablesRequest)
    {
        if (!IsSessionValid())
        {
            return null;
        }

        var search = dataTablesRequest.Search?.ToLower();
        var isAscending = dataTablesRequest.SortDirection?.ToUpper() == "ASC";

        var query = DatabaseManager.Categories.GetAll().OrderByDescending(a => a.Id);

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
                    query = query.Where(a => a.Name != null && a.Name.ToLower().Contains(search));
                }
                else if (search.StartsWith("*"))
                {
                    query = query.Where(a => a.Name != null && a.Name.ToLower().EndsWith(search));
                }
                else if (search.EndsWith("*"))
                {
                    query = query.Where(a => a.Name != null && a.Name.ToLower().StartsWith(search));
                }
                else
                {
                    query = query.Where(a => a.Name != null && a.Name.ToLower().Equals(search));
                }
            }
        }

        var totalRecords = await query.CountAsync().ConfigureAwait(false);

        var categories = await query.Skip(dataTablesRequest.Start)
            .Take(dataTablesRequest.End)
            .ToListAsync().ConfigureAwait(false);

        var result = categories.Select(category => new CategoryViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Domains = category.Domains
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
                    ? result.OrderBy(x => x.Name).ToList()
                    : result.OrderByDescending(x => x.Name).ToList();
                break;
            case 2:
                result = isAscending
                    ? result.OrderBy(x => x.Domains).ToList()
                    : result.OrderByDescending(x => x.Domains).ToList();
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

        return PartialView("../Category/Index");
    }

    [HttpPost]
    public async Task<ActionResult> UpdateAsync(CategoryViewModel category)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }

        Category databaseCategory = null;
        var isNew = category.Id == 0;
        if (!isNew)
        {
            databaseCategory = await DatabaseManager.Categories.FirstOrDefaultAsync(x => x.Id == category.Id)
                .ConfigureAwait(false);
        }

        if (databaseCategory != null)
        {
            databaseCategory.Name = category.Name;
        }
        else
        {
            databaseCategory = new Category
            {
                Name = category.Name
            };
        }

        // Update the entity
        await DatabaseManager.Categories.InsertOrUpdateAsync(databaseCategory).ConfigureAwait(false);
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

        _ = int.TryParse(id, out var categoryId);
        var isNew = categoryId == 0;

        var category = isNew
            ? null
            : await DatabaseManager.Categories.FirstOrDefaultAsync(x => x.Id == categoryId).ConfigureAwait(false);

        CategoryViewModel model = null;

        if (category != null)
        {
            model = new CategoryViewModel
            {
                Id = category.Id,
                Name = category.Name
            };
        }
        else if (isNew)
        {
            model = new CategoryViewModel();
        }

        return PartialView("categoryModal", model);
    }

    [HttpPost]
    public async Task<ActionResult> DeleteAsync(string id)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }

        _ = int.TryParse(id, out var categoryId);
        if (categoryId <= 0)
        {
            return RedirectToAction("Index");
        }

        var category = await DatabaseManager.Categories.FirstOrDefaultAsync(x => x.Id == categoryId)
            .ConfigureAwait(false);
        if (category != null)
        {
            await DatabaseManager.Categories.DeleteAsync(category).ConfigureAwait(false);
        }

        return RedirectToAction("Index");
    }
}