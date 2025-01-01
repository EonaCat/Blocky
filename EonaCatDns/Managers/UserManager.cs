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
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using EonaCat.Dns.Database.Models.Entities;

namespace EonaCat.Dns.Managers;

internal class UserManager
{
    internal static UserManager Instance;

    public UserManager()
    {
        Instance = this;
    }

    private ConcurrentDictionary<string, User> Credentials { get; } = new();
    internal ConcurrentDictionary<string, User> Users => Credentials;

    internal static string GetPasswordHash(string username, string password)
    {
        using HMAC hmac = new HMACSHA512(Encoding.UTF8.GetBytes(password));
        return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(username))).Replace("-", "").ToLower();
    }

    internal User ChangeCredentials(string username, string password)
    {
        username = username.ToLower();
        var passwordHash = GetPasswordHash(username, password);
        Credentials.TryGetValue(username, out var user);
        if (user == null)
        {
            return null;
        }

        user.Password = passwordHash;
        return user;
    }

    internal User LoadCredentials(User user)
    {
        return Credentials.AddOrUpdate(user.Name.ToLower(), user, delegate { return user; });
    }

    internal User ClearAndSetDefaultUser()
    {
        ClearCredentials();

        const string username = "eonacat";
        var passwordHash = GetPasswordHash(username, "admin");
        var defaultUser = new User { Name = username, Username = username, Password = passwordHash };
        return Credentials.AddOrUpdate(defaultUser.Username.ToLower(), defaultUser, delegate { return defaultUser; });
    }

    internal void ClearCredentials()
    {
        Credentials.Clear();
    }
}