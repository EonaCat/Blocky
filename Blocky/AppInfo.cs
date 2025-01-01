using System.IO;
using System.Reflection;
using EonaCat.Dns;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace EonaCat.Blocky;
// Blocky
// Blocking domains the way you want it.
// Copyright EonaCat (Jeroen Saey) 2017-2025
// https://blocky.EonaCat.com

public static class AppInfo
{
    public const string Name = "Blocky";

    static AppInfo()
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

    internal static string BaseUrl { get; private set; }

    internal static string VersionName { get; }

    internal static IConfiguration Configuration { get; set; }

    public static string Applicationpath { get; private set; }

    private static string GetVersion()
    {
        if (HideVersion)
        {
            return string.Empty;
        }

        return AssemblyVersion;
    }

    internal static void SetBaseUrl(this IApplicationBuilder app, bool setBlockyRedirectionServerUrl = true)
    {
        //  Add headers and set the baseUrl
        app.Use((context, next) =>
        {
            context.Response.Headers.Remove("server");
            context.Response.Headers.Remove("X-Firefox-Spdy");
            context.Response.Headers.Remove("x-sourcefiles");
            context.Response.Headers.Remove("X-powered-by");

            context.Response.Headers["server"] = $"Blocky {GetVersion()} - (Running on EonaCatDns {DllInfo.Version})";
            context.Response.Headers["X-powered-by"] = "EonaCatDns";
            context.Response.Headers["Created-by"] = "EonaCat (Jeroen Saey)";

            if (string.IsNullOrEmpty(BaseUrl))
            {
                BaseUrl = $"{context.Request.Scheme}://{context.Request.Host}/";

                if (setBlockyRedirectionServerUrl)
                {
                    //  Set the blocky redirection server
                    var configuration = Configuration.GetSection("Blocky");
                    Config.BlockyRedirectionserver =
                        string.IsNullOrEmpty(configuration.GetValue("RedirectionAddress", $"{BaseUrl}ad"))
                            ? $"{BaseUrl}ad"
                            : configuration.GetValue("RedirectionAddress", $"{BaseUrl}ad");
                }
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

        Applicationpath += Path.DirectorySeparatorChar;
        if (!Directory.Exists(Applicationpath))
        {
            Directory.CreateDirectory(Applicationpath);
        }
    }
}