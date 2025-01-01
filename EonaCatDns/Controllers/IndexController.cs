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
using System.Threading.Tasks;
using EonaCat.Dns.Database;
using EonaCat.Dns.Managers;
using EonaCat.Dns.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EonaCat.Dns.Controllers;

public class IndexController : ControllerBase
{
    public ActionResult Index(string viewName = "Index")
    {
        return View(viewName, GetIndexModel());
    }

    [HttpPost]
    public async Task<ActionResult> LogOut()
    {
        var model = GetIndexModel();
        HttpContext.Session.Remove("UserId");
        model.IsLoggedIn = false;
        model.Name = null;
        model.Password = null;
        model.UserId = null;
        if (UserTokens.ContainsKey(model.Token))
        {
            UserTokens.Remove(model.Token);
        }

        EonaCatDns.Managers.SessionManager.DeleteSession(model.Token);
        model.Token = null;
        await Logger.LogAsync($"User '{model.Username}' logged out");
        model.Username = null;
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<ActionResult> ChangePasswordAsync(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Index();
        }

        if (model.NewPassword != null && model.OldPassword != null)
        {
            var userModel = GetIndexModel();
            if (userModel.IsLoggedIn)
            {
                var userId = Convert.ToInt32(userModel.UserId);
                var currentUser = await DatabaseManager.Users.FirstOrDefaultAsync(x => x.Id == userId)
                    .ConfigureAwait(false);
                if (currentUser != null &&
                    currentUser.Password ==
                    UserManager.GetPasswordHash(userModel.Username, model.OldPassword))
                {
                    currentUser.Password =
                        UserManager.GetPasswordHash(userModel.Username, model.NewPassword);
                    await DatabaseManager
                        .UpdateUserAsync(UserManager.Instance.ChangeCredentials(userModel.Username, model.NewPassword))
                        .ConfigureAwait(false);
                    await Managers.Managers.WriteToLog($"Password updated for user '{userModel.Username}'")
                        .ConfigureAwait(false);
                    CreateAlertBag("Your password was changed successfully!", "Changed");
                }
                else
                {
                    CreateAlertBag("Invalid old password", "Error", "danger");
                }
            }
            else
            {
                CreateAlertBag("User not logged in", "Error", "danger");
            }
        }
        else
        {
            CreateAlertBag("Invalid username or password", "Error", "danger");
        }

        return Index();
    }

    [HttpPost]
    public async Task<ActionResult> LoginAsync(IndexViewModel model)
    {
        if (ModelState.IsValid)
        {
            if (model.Username != null && model.Password != null)
            {
                model.Username = model.Username.ToLower();
                model.Password = UserManager.GetPasswordHash(model.Username, model.Password);
                var user = await DatabaseManager.Users
                    .FirstOrDefaultAsync(u => u.Username == model.Username && u.Password == model.Password)
                    .ConfigureAwait(false);

                if (user != null)
                {
                    var token = EonaCatDns.Managers.SessionManager.CreateSession(user.Username);
                    HttpContext.Session.SetString("UserId", user.Id.ToString());
                    HttpContext.Session.SetString("name", user.Name);
                    HttpContext.Session.SetString("username", user.Username);
                    HttpContext.Session.SetString("token", token);
                    HttpContext.Session.SetString("statsRefreshInterval",
                        ConstantsDns.Stats.RefreshInterval.ToString());
                    await Logger.LogAsync($"User '{model.Username}' logged in");
                    CreateAlertBag(model.Username, "Welcome", useTempData: true);
                    UserTokens.Add(token, user.Username);
                    return base.RedirectToAction("index");
                }
            }
        }

        CreateAlertBag("Invalid username or password", "Error", "danger");
        return View("Index", model);
    }

    private void CreateAlertBag(string message, string title = "status", string currentClass = "success",
        bool useTempData = false)
    {
        if (useTempData)
        {
            TempData["AlertType"] = currentClass;
            TempData["AlertTitle"] = title;
            TempData["AlertMessage"] = message;
        }
        else
        {
            ViewBag.AlertType = currentClass;
            ViewBag.AlertTitle = title;
            ViewBag.AlertMessage = message;
        }
    }
}