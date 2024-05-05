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

using System.Collections.Generic;
using System.Text;
using EonaCat.Dns.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EonaCat.Dns.Controllers;

public class ControllerBase : Controller
{
    protected static Dictionary<string, string> UserTokens = new();

    protected IndexViewModel GetIndexModel()
    {
        ViewBag.Title = !DllInfo.HideVersion
            ? $"{DllInfo.ApplicationName} - {DllInfo.ApplicationVersion}"
            : $"{DllInfo.ApplicationName}";

        var model = new IndexViewModel
        {
            Username = HttpContext.Session.GetString("username"),
            IsLoggedIn = HttpContext.Session.TryGetValue("UserId", out var id),
            Name = HttpContext.Session.GetString("name"),
            Token = HttpContext.Session.GetString("token"),
            UserId = id != null ? Encoding.Default.GetString(id) : null
        };

        // Extra security check
        model.IsLoggedIn = model.IsLoggedIn && UserTokens.ContainsKey(model.Token!) &&
                           UserTokens[model.Token] == model.Username;

        if (!model.IsLoggedIn)
            // Clear all the userData
        {
            model = new IndexViewModel();
        }

        return model;
    }

    protected bool IsSessionValid()
    {
        var model = GetIndexModel();
        return model.IsLoggedIn;
    }
}