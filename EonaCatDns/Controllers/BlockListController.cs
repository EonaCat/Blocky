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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Dns.Models;
using Microsoft.AspNetCore.Mvc;
using BlockList = EonaCat.Dns.Database.Models.Entities.BlockList;

namespace EonaCat.Dns.Controllers;

public class BlockListController : ControllerBase
{
    public ActionResult Index()
    {
        return RedirectToAction("Index", "Index");
    }

    public async Task<string> GetListAsync(DataTableRequest dataTablesRequest)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }

        var search = dataTablesRequest.Search?.ToLower();
        var isAscending = dataTablesRequest.SortDirection?.ToUpper() == "ASC";

        var query = DatabaseManager.BlockLists.GetAll().OrderByDescending(a => a.Id);

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
                                             (a.Name != null && a.Name.ToLower().Contains(search)));
                }
                else if (search.StartsWith("*"))
                {
                    query = query.Where(a => (a.Url != null && a.Url.ToLower().EndsWith(search)) ||
                                             (a.Name != null && a.Name.ToLower().EndsWith(search)));
                }
                else if (search.EndsWith("*"))
                {
                    query = query.Where(a => (a.Url != null && a.Url.ToLower().StartsWith(search)) ||
                                             (a.Name != null && a.Name.ToLower().StartsWith(search)));
                }
                else
                {
                    query = query.Where(a => (a.Url != null && a.Url.ToLower().Equals(search)) ||
                                             (a.Name != null && a.Name.ToLower().Equals(search)));
                }
            }
        }

        var totalRecords = await query.CountAsync().ConfigureAwait(false);

        var blockLists = await query.Skip(dataTablesRequest.Start)
            .Take(dataTablesRequest.End)
            .ToListAsync().ConfigureAwait(false);

        var result = blockLists.Select(blockList => new BlockListViewModel
        {
            Id = blockList.Id,
            Url = blockList.Url,
            Name = blockList.Name,
            CreationDate = blockList.CreationDate,
            IsEnabled = blockList.IsEnabled,
            LastResult = blockList.LastResult,
            LastUpdated = blockList.LastUpdated,
            LastUpdateStartTime = blockList.LastUpdateStartTime,
            TotalEntries = blockList.TotalEntries,
            IsUpdating = Blocker.RunningBlockerTasks.Any(x => x.Uri.AbsoluteUri == blockList.Url)
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
                    ? result.OrderBy(x => x.Url).ToList()
                    : result.OrderByDescending(x => x.Url).ToList();
                break;
            case 3:
                result = isAscending
                    ? result.OrderBy(x => x.IsEnabled).ToList()
                    : result.OrderByDescending(x => x.IsEnabled).ToList();
                break;
            case 4:
                result = isAscending
                    ? result.OrderBy(x => x.LastResult).ToList()
                    : result.OrderByDescending(x => x.LastResult).ToList();
                break;
            case 5:
                result = isAscending
                    ? result.OrderBy(x => x.LastUpdated).ToList()
                    : result.OrderByDescending(x => x.LastUpdated).ToList();
                break;
            case 6:
                result = isAscending
                    ? result.OrderBy(x => x.LastUpdateStartTime).ToList()
                    : result.OrderByDescending(x => x.LastUpdateStartTime).ToList();
                break;
            default:
                result = dataTablesRequest.SortColumn switch
                {
                    6 => isAscending
                        ? result.OrderBy(x => x.CreationDate).ToList()
                        : result.OrderByDescending(x => x.CreationDate).ToList(),
                    7 => isAscending
                        ? result.OrderBy(x => x.TotalEntries).ToList()
                        : result.OrderByDescending(x => x.TotalEntries).ToList(),
                    _ => result
                };
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

        return PartialView("../BlockList/Index");
    }

    [HttpPost]
    public Task<ActionResult> WatchMode()
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return Task.FromResult<ActionResult>(null);
        }

        Server.WatchMode = !Server.WatchMode;
        return Task.FromResult<ActionResult>(RedirectToAction("Index"));
    }

    [HttpPost]
    public async Task<ActionResult> UpdateAsync(BlockListViewModel blockList)
    {
        if (!IsSessionValid())
        {
            return Content("OK");
        }

        var isNew = blockList.Id == 0;
        BlockList databaseBlockList = null;
        if (!isNew)
        {
            databaseBlockList = await DatabaseManager.BlockLists.FirstOrDefaultAsync(x => x.Id == blockList.Id)
                .ConfigureAwait(false);
        }

        if (databaseBlockList != null)
        {
            databaseBlockList.Url = blockList.Url;
            databaseBlockList.Name = blockList.Name;
            databaseBlockList.IsEnabled = blockList.IsEnabled;
        }
        else
        {
            databaseBlockList = new BlockList
            {
                Name = blockList.Name,
                CreationDate = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                IsEnabled = blockList.IsEnabled,
                Url = blockList.Url
            };
        }

        // Update the entity
        await DatabaseManager.BlockLists.InsertOrUpdateAsync(databaseBlockList).ConfigureAwait(false);
        return Content("OK");
    }

    [HttpGet]
    public async Task<ActionResult> GetByIdAsync(string id)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return Content("OK");
        }

        _ = long.TryParse(id, out var result);
        var isNew = result < 0;

        BlockListViewModel model = null;
        var blockList = isNew
            ? null
            : await DatabaseManager.BlockLists.FirstOrDefaultAsync(x => x.Id == result).ConfigureAwait(false);

        if (blockList != null)
        {
            model = new BlockListViewModel
            {
                Id = blockList.Id,
                CreationDate = blockList.CreationDate,
                IsEnabled = blockList.IsEnabled,
                LastResult = blockList.LastResult,
                LastUpdated = blockList.LastUpdated,
                LastUpdateStartTime = blockList.LastUpdateStartTime,
                Name = blockList.Name,
                TotalEntries = blockList.TotalEntries,
                Url = blockList.Url
            };
        }
        else if (isNew)
        {
            model = new BlockListViewModel();
        }

        return PartialView("blockListModal", model);
    }

    [HttpPost]
    public async Task<ActionResult> UpdateListAsync(string id)
    {
        if (!IsSessionValid())
        {
            RedirectToAction("Index");
            return null;
        }

        _ = long.TryParse(id, out var blockListId);
        if (blockListId <= 0)
        {
            return RedirectToAction("Index");
        }

        var blockList = await DatabaseManager.BlockLists.FirstOrDefaultAsync(x => x.Id == blockListId)
            .ConfigureAwait(false);
        if (blockList != null)
        {
            DatabaseManager.UpdateBlockList(blockList.Url);
        }

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

        _ = long.TryParse(id, out var blockListId);
        if (blockListId <= 0)
        {
            return RedirectToAction("Index");
        }

        var blockList = await DatabaseManager.BlockLists.FirstOrDefaultAsync(x => x.Id == blockListId)
            .ConfigureAwait(false);
        if (blockList != null)
        {
            await DatabaseManager.BlockLists.DeleteAsync(blockList).ConfigureAwait(false);
        }

        return RedirectToAction("Index");
    }
}