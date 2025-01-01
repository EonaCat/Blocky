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
using EonaCat.Dns.Exceptions;
using EonaCat.Dns.Models;

namespace EonaCat.Dns.Managers;

internal class SessionManager
{
    public ConcurrentDictionary<string, UserSession> Sessions { get; } = new();

    internal bool IsSessionValid(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var session = GetSession(token);
        if (session == null)
        {
            return false;
        }

        if (session.HasExpired())
        {
            DeleteSession(token);
            return false;
        }

        session.UpdateLastSeen();
        return true;
    }

    internal string CreateSession(string username)
    {
        var token = GenerateToken(username);
        if (!Sessions.TryAdd(token, new UserSession(username)))
        {
            throw new WebException("EonaCatDns: " + "EonaCatDns: Error while creating session. Please try again.");
        }

        return token;
    }

    private static string GenerateToken(string username)
    {
        return Guid.NewGuid().ToString();
    }

    internal UserSession GetSession(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new WebException("EonaCatDns: " + "EonaCatDns: Parameter 'token' missing.");
        }

        return GetSessionTokenForUser(token);
    }

    private UserSession GetSessionTokenForUser(string token)
    {
        return Sessions.TryGetValue(token, out var session) ? session : null;
    }

    private UserSession DeleteSessionForUser(string token)
    {
        return Sessions.TryRemove(token, out var session) ? session : null;
    }

    internal UserSession DeleteSession(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new WebException("EonaCatDns: " + "EonaCatDns: Parameter 'token' missing.");
        }

        return DeleteSessionForUser(token);
    }
}