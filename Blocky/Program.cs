using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Logger.EonaCatCoreLogger.Extensions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace EonaCat.Blocky;
// Blocky
// Blocking domains the way you want it.
// Copyright EonaCat (Jeroen Saey) 2017-2025
// https://blocky.EonaCat.com

public class Program
{
    private static readonly string LogFolderName = "logs";

    private static string LogFolder { get; set; }

    public static async Task Main(string[] args)
    {
        try
        {
            LogFolder = Path.Combine(AppInfo.Applicationpath, LogFolderName);
            CreateFolder(LogFolder);
            await CheckArgumentsAsync(args).ConfigureAwait(false);
            //await CheckArgumentsAsync(new string[] {"--ns","google.com"}).ConfigureAwait(false);

            GetServerUrlsFromCommandLine(args);
            var webHost = CreateWebHostBuilder(args);
            var builder = webHost.Build();

            await BlockyDns.StartAsync(Config.Loglevel).ConfigureAwait(false);
            await builder.RunAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await File.WriteAllTextAsync($"{LogFolder}{Path.DirectorySeparatorChar}blockyFailures.log",
                $"{DateTime.Now} [{GetOsPlatform()}]: {exception}{Environment.NewLine}").ConfigureAwait(false);
        }
    }

    private static async Task CheckArgumentsAsync(string[] args)
    {
        if (args is { Length: > 0 })
        {
            await Server.NsLookupAsync(args).ConfigureAwait(false);
            await Server.NetworkScanAsync(args).ConfigureAwait(false);
            await Server.MultiCastAsync(args).ConfigureAwait(false);
        }
    }

    private static void CreateFolder(string logFolder)
    {
        if (Directory.Exists(logFolder))
        {
            return;
        }

        if (logFolder != null)
        {
            Directory.CreateDirectory(logFolder);
        }
    }

    public static OSPlatform GetOsPlatform()
    {
        var osPlatform = OSPlatform.Create("Other Platform");

        //  Check if it's windows
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        osPlatform = isWindows ? OSPlatform.Windows : osPlatform;

        //  Check if it's osx
        var isOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        osPlatform = isOsx ? OSPlatform.OSX : osPlatform;

        //  Check if it's Linux
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        osPlatform = isLinux ? OSPlatform.Linux : osPlatform;
        return osPlatform;
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args = null)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppInfo.Applicationpath)
            .AddJsonFile("blocky.json", true)
            .Build();

        var blockySection = config.GetSection("Blocky");
        var blockyScheme = blockySection.GetValue("Scheme", "http");
        var blockyInterface = blockySection.GetValue("ListenV4", "127.0.0.1");
        var blockyPort = blockySection.GetValue("Port", 80);

        args ??= Array.Empty<string>();

        var host = WebHost.CreateDefaultBuilder(args)
            .UseKestrel(options => options.AddServerHeader = false)
            .UseContentRoot(AppInfo.Applicationpath)
            .UseConfiguration(config)
            .UseIISIntegration()
            .UseUrls($"{blockyScheme}://{blockyInterface}:{blockyPort}/")
            .UseStartup<Startup>();

        host.ConfigureLogging(x => x.AddEonaCatFileLogger(options =>
        {
            options.FileNamePrefix = "web";
            options.LogDirectory = $"{AppInfo.Applicationpath}{Path.DirectorySeparatorChar}{LogFolderName}";
            options.FileSizeLimit = 20 * 1024 * 1024;
            options.IsEnabled = true;
            options.UseLocalTime = Config.LogInLocalTime;
        }));

        return host;
    }

    public static IConfigurationRoot GetServerUrlsFromCommandLine(string[] args)
    {
        var config = new ConfigurationBuilder().AddCommandLine(args).Build();

        var serverport = config.GetValue<int?>("port") ?? 80;
        var serverurls = config.GetValue<string>("server.urls") ?? $"http://*:{serverport}";

        var configDictionary = new Dictionary<string, string>
        {
            { "server.urls", serverurls },
            { "port", serverport.ToString() }
        };

        return new ConfigurationBuilder()
            .AddCommandLine(args)
            .AddInMemoryCollection(configDictionary)
            .Build();
    }
}