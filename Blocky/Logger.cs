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
using System.Threading.Tasks;
using EonaCat.Logger;
using EonaCat.Logger.Managers;

namespace EonaCat.Blocky
{
    // Blocky
    // Blocking domains the way you want it.
    // Copyright EonaCat (Jeroen Saey) 2017-2023
    // https://blocky.EonaCat.com

    public static class Logger
    {
        public static bool UseLocalTime { get; set; }
        public static ELogType MaxLogType { get; set; } = ELogType.TRACE;
        public static string LogFolder => Config.LogFolder;
        public static string CurrentLogFile => LogManager?.CurrentLogFile;
        private static LogManager LogManager;

        public static void Log(string message, ELogType logType = ELogType.INFO, bool writeToConsole = true)
        {
            Task.Run(() =>
            {
                LogManager.Write(message, logType, writeToConsole);
            }).ConfigureAwait(false);
        }

        public static void Log(Exception exception, string message = "", bool writeToConsole = true)
        {
            Task.Run(() =>
            {
                LogManager.Write(exception, module: message, writeToConsole: writeToConsole);
            }).ConfigureAwait(false);
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
                    UseLocalTime = UseLocalTime,
                }
            };
            LogManager = new LogManager(loggerSettings);
        }
    }
}