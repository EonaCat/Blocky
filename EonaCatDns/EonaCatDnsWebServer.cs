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

using System;
using System.IO;
using System.Threading.Tasks;
using EonaCat.Helpers;
using EonaCat.Logger.EonaCatCoreLogger.Extensions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace EonaCat.Dns;

public class EonaCatDnsWebServer
{
    private const string LogFolderName = "logs";
    private readonly IWebHost _builder;

    public EonaCatDnsWebServer(bool logInLocalTime = false, string webInterface = "127.0.0.1", int webServicePort = 80,
        string[] args = null)
    {
        try
        {
            LogFolder = Path.Combine(DllInfo.Applicationpath, LogFolderName);
            Directory.CreateDirectory(LogFolder);

            var webHost = CreateWebHostBuilder(logInLocalTime, webInterface, webServicePort, args);
            _builder = webHost.Build();
        }
        catch (Exception exception)
        {
            File.WriteAllText($"{LogFolder}{Path.DirectorySeparatorChar}{DllInfo.Name.ToLower()}Failures.log",
                $"{DateTime.Now} [{PlatformHelper.GetOsPlatform()}]: {exception}{Environment.NewLine}");
        }
    }

    private string LogFolder { get; }

    public bool IsRunning { get; set; }

    public IWebHostBuilder CreateWebHostBuilder(bool logInLocalTime = false, string webInterface = "127.0.0.1",
        int webServicePort = 80,
        string[] args = null)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(DllInfo.Applicationpath)
            .Build();

        var host = WebHost.CreateDefaultBuilder(args ?? Array.Empty<string>())
            .UseKestrel(options => options.AddServerHeader = false)
            .UseContentRoot(DllInfo.Applicationpath)
            .UseConfiguration(config)
            .UseIISIntegration()
            .UseUrls($"http://{webInterface}:{webServicePort}/")
            .UseStartup<Startup>();

        host.ConfigureLogging(x => x.AddEonaCatFileLogger(options =>
        {
            options.FileNamePrefix = "web";
            options.LogDirectory = Path.Combine(DllInfo.Applicationpath, LogFolderName);
            options.FileSizeLimit = 20 * 1024 * 1024;
            options.UseLocalTime = logInLocalTime;
        }));

        return host;
    }

    public async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        await EonaCatDns.Managers.WriteToLog("Starting adminPanel.").ConfigureAwait(false);
        Task.Run(() => _builder.RunAsync());
        IsRunning = true;
        await EonaCatDns.Managers.WriteToLog("AdminPanel was started successfully.").ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        await _builder.StopAsync().ConfigureAwait(false);
        IsRunning = false;
        await EonaCatDns.Managers.WriteToLog("AdminPanel was stopped successfully.").ConfigureAwait(false);
    }
}