using System.Diagnostics;
using System.Threading.Tasks;
using EonaCat.Dns.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EonaCat.Blocky.Controllers;
// Blocky
// Blocking domains the way you want it.
// Copyright EonaCat (Jeroen Saey) 2017-2023
// https://blocky.EonaCat.com

public class BlockyIndexController : Controller
{
    private readonly IHubContext<BlockyHub> _blockyHub;

    public BlockyIndexController(IHubContext<BlockyHub> blockyHub)
    {
        _blockyHub = blockyHub;
    }

    public Task Notify()
    {
        return _blockyHub.Clients.All.SendAsync("GetUpdatedData");
    }

    public IActionResult BlockyIndex()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}