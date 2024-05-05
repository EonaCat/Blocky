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

using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace EonaCat.Dns;

public static class DllInfo
{
    public const string Name = "EonaCatDns";

    static DllInfo()
    {
        CreateAppFolder();

        var isDebug = false;
#if DEBUG
        isDebug = true;
#endif
        VersionName = isDebug ? "DEBUG" : "RELEASE";
    }

    private static string AssemblyVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? version.ToString() : string.Empty;
        }
    }

    public static bool HideVersion { get; set; }
    public static string Version => GetVersion();

    public static string ApplicationName { get; set; }
    public static string ApplicationVersion { get; set; }

    internal static string BaseUrl { get; private set; }

    internal static string VersionName { get; }

    internal static IConfiguration Configuration { get; set; }

    public static string ConfigFolder => AppFolder;
    public static string AppFolder => Applicationpath;

    public static string LogFolder { get; internal set; }

    public static string Applicationpath { get; private set; }

    private static string GetVersion()
    {
        return HideVersion ? string.Empty : AssemblyVersion;
    }

    public static void SetApplicationName(string applicationName)
    {
        ApplicationName = applicationName;
    }

    public static void SetApplicationVersion(string applicationVersion)
    {
        ApplicationVersion = applicationVersion;
    }

    internal static void SetBaseUrl(this IApplicationBuilder app)
    {
        //  Add headers and set the baseUrl
        app.Use((context, next) =>
        {
            var applicationName = ApplicationName;
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                applicationName = Name;
            }

            var applicationVersion = ApplicationVersion;
            if (string.IsNullOrWhiteSpace(applicationVersion))
            {
                applicationVersion = GetVersion();
            }

            context.Response.Headers.Remove("server");
            context.Response.Headers.Remove("X-Firefox-Spdy");
            context.Response.Headers.Remove("x-sourcefiles");
            context.Response.Headers.Remove("X-powered-by");

            context.Response.Headers["server"] =
                $"{applicationName} {applicationVersion} - (Running on EonaCatDns {GetVersion()})";
            context.Response.Headers["X-powered-by"] = "EonaCatDns";
            context.Response.Headers["Created-by"] = "EonaCat (Jeroen Saey)";

            if (string.IsNullOrEmpty(BaseUrl))
            {
                BaseUrl = $"{context.Request.Scheme}://{context.Request.Host}/";
            }

            return next.Invoke();
        });
    }

    private static void CreateAppFolder()
    {
        if (string.IsNullOrEmpty(Applicationpath))
        {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                Applicationpath = Path.GetDirectoryName(assembly.Location);
            }
        }

        Applicationpath = Applicationpath + Path.DirectorySeparatorChar + Name.ToLower();
        if (!Directory.Exists(Applicationpath))
        {
            Directory.CreateDirectory(Applicationpath);
        }
    }
}