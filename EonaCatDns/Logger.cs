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
using System.IO;
using System.Threading.Tasks;
using EonaCat.Logger;
using EonaCat.Logger.Managers;
using Microsoft.AspNetCore.Http;

namespace EonaCat.Dns;

public static class Logger
{
    private const int BUFFER_SIZE = 4096;

    public static bool UseLocalTime { get; set; }
    public static ELogType MaxLogType { get; set; } = ELogType.DEBUG;
    public static string LogFolder => DllInfo.LogFolder;
    public static string CurrentLogFile => LogManager?.CurrentLogFile;
    public static bool IsDisabled { get; set; }
    private static LogManager LogManager { get; set; }

    public static void DeleteCurrentLogFile()
    {
        if (IsDisabled)
        {
            return;
        }

        LogManager.DeleteCurrentLogFile();
    }

    public static async Task DownloadLogAsync(HttpContext context, string filePath)
    {
        var response = context.Response;
        response.ContentType = "application/octet-stream";
        response.Headers.Append("Content-Disposition", "attachment; filename=\"" + Path.GetFileName(filePath) + "\"");

        var responseCompleted = false;

        try
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Read initial file contents and send it to the response
            var buffer = new byte[BUFFER_SIZE];
            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                if (!responseCompleted)
                {
                    await response.Body.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                    await response.Body.FlushAsync()
                        .ConfigureAwait(false); // Flush the response body to ensure data is sent immediately
                }
                else
                {
                    await LogAsync("Cannot write to the response body after the response has completed.",
                        ELogType.WARNING).ConfigureAwait(false);
                    break; // Exit the loop if response has already started
                }
        }
        catch (Exception ex)
        {
            await LogAsync($"Error downloading log file: {ex.Message}", ELogType.ERROR).ConfigureAwait(false);
        }
        finally
        {
            // Set the flag to indicate that the response has completed
            responseCompleted = true;
        }
    }


    public static async Task LogAsync(string message, ELogType logType = ELogType.INFO, bool writeToConsole = true)
    {
        if (IsDisabled)
        {
            return;
        }

        await LogManager.WriteAsync(message, logType, writeToConsole).ConfigureAwait(false);
    }

    public static async Task LogAsync(Exception exception, string message = "", bool writeToConsole = true)
    {
        if (IsDisabled)
        {
            return;
        }

        await LogManager.WriteAsync(exception, message, writeToConsole: writeToConsole).ConfigureAwait(false);
    }

    public static void Configure()
    {
        var loggerSettings = new LoggerSettings
        {
            Id = "EonaCatDnsLogger",
            MaxLogType = MaxLogType,
            UseLocalTime = UseLocalTime,
            FileLoggerOptions =
            {
                LogDirectory = DllInfo.LogFolder,
                FileSizeLimit = 20_000_000, // 20 MB
                UseLocalTime = UseLocalTime
            }
        };
        LogManager = new LogManager(loggerSettings);
    }
}