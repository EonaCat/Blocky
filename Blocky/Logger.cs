﻿/*
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
using EonaCat.Logger;
using EonaCat.Logger.Managers;

namespace EonaCat.Blocky;
// Blocky
// Blocking domains the way you want it.
// Copyright EonaCat (Jeroen Saey) 2017-2025
// https://blocky.EonaCat.com

public static class Logger
{
    private static LogManager LogManager;
    public static bool UseLocalTime { get; set; }
    public static ELogType MaxLogType { get; set; } = ELogType.TRACE;
    public static string LogFolder => Config.LogFolder;
    public static string CurrentLogFile => LogManager?.CurrentLogFile;

    public static async Task LogAsync(string message, ELogType logType = ELogType.INFO, bool writeToConsole = true)
    {
        await Task.Run(async () => { await LogManager.WriteAsync(message, logType, writeToConsole); })
            .ConfigureAwait(false);
    }

    public static async Task LogAsync(Exception exception, string message = "", bool writeToConsole = true)
    {
        await Task.Run(async () => { await LogManager.WriteAsync(exception, message, writeToConsole: writeToConsole); })
            .ConfigureAwait(false);
    }

    public static void Configure()
    {
        var loggerSettings = new LoggerSettings
        {
            Id = "BlockyLogger",
            MaxLogType = MaxLogType,
            UseLocalTime = UseLocalTime,
            FileLoggerOptions =
            {
                LogDirectory = LogFolder,
                FileSizeLimit = 20 * 1024 * 1024,
                UseLocalTime = UseLocalTime
            }
        };
        LogManager = new LogManager(loggerSettings);
    }
}