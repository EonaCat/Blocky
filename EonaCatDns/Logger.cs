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

using EonaCat.Logger;
using EonaCat.Logger.Managers;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace EonaCat.Dns
{
    public static class Logger
    {
        public static bool UseLocalTime { get; set; }
        public static ELogType MaxLogType { get; set; } = ELogType.TRACE;
        public static string LogFolder => DllInfo.LogFolder;
        public static string CurrentLogFile => LogManager?.CurrentLogFile;
        public static bool IsDisabled { get; set; }
        private static LogManager LogManager { get; set; }

        public static void DeleteCurrentLogFile()
        {
            if (IsDisabled)
                return;

            LogManager.DeleteCurrentLogFile();
        }

        public static async Task DownloadLogAsync(HttpContext context, string filePath)
        {
            var response = context.Response;
            response.ContentType = "application/octet-stream";
            response.Headers.Add("Content-Disposition", "attachment; filename=\"" + Path.GetFileName(filePath) + "\"");

            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath) ?? string.Empty, Path.GetFileName(filePath));
            var completionSource = new TaskCompletionSource<bool>();

            // Event handler for changes to the file
            void OnFileChanged(object sender, FileSystemEventArgs e)
            {
                if (e.ChangeType == WatcherChangeTypes.Changed || e.ChangeType == WatcherChangeTypes.Created)
                {
                    completionSource.TrySetResult(true);
                }
            }

            watcher.EnableRaisingEvents = true;
            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;

            // Read initial file contents and send it to the response
            var buffer = new byte[4096];
            try
            {
                if (!response.HasStarted) // Check if the response has not been sent to the client
                {
                    int bytesRead;
                    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        await response.Body.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception)
            {
                // Do nothing
            }
            finally
            {
                // Clean up resources
                watcher.Changed -= OnFileChanged;
                watcher.Created -= OnFileChanged;
                watcher.Dispose();
            }
        }

        public static void Log(string message, ELogType logType = ELogType.INFO, bool writeToConsole = true)
        {
            if (IsDisabled)
                return;

            LogManager.Write(message, logType, writeToConsole);
        }

        public static void Log(Exception exception, string message = "", bool writeToConsole = true)
        {
            if (IsDisabled)
                return;

            LogManager.Write(exception, module: message, writeToConsole: writeToConsole);
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
                    UseLocalTime = UseLocalTime,
                },
            };
            LogManager = new LogManager(loggerSettings);
        }
    }
}
