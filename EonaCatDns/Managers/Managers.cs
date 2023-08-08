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

using EonaCat.Helpers.Helpers;
using EonaCat.Json;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Text.RegularExpressions;
using EonaCat.Dns.Managers.Stats;
using WebException = EonaCat.Dns.Exceptions.WebException;

namespace EonaCat.Dns.Managers;

internal class Managers
{
    internal Managers(StatsManager statsManager, SessionManager sessionManager, UserManager userManager,
        StatsManagerApi apiStatsManager)
    {
        UserManager = userManager;
        StatsManager = statsManager;
        SessionManager = sessionManager;
        ApiStatsManager = apiStatsManager;
    }

    public UserManager UserManager { get; }
    public StatsManager StatsManager { get; }
    public SessionManager SessionManager { get; }
    public StatsManagerApi ApiStatsManager { get; }
    private static bool IsRetrievingStats { get; set; }

    internal async Task ApiGetStatsAsync(string type, JsonTextWriter jsonWriter, bool isAuthenticated, bool forceNew)
    {
        try
        {
            if (IsRetrievingStats)
            {
                return;
            }

            IsRetrievingStats = true;

            var data = await ApiStatsManager.GetStatsAsync(type, jsonWriter, isAuthenticated, forceNew).ConfigureAwait(false);

            IsRetrievingStats = false;

            if (data == null || !data.Any())
            {
                return;
            }

            await StatsDataSetParser.CreateStatsArrayAsync(jsonWriter, data).ConfigureAwait(false);
            await StatsDataSetParser.CreateStatisticsDataArrayAsync(jsonWriter, data).ConfigureAwait(false);
            await StatsDataSetParser.CreateQueryTypesArrayAsync(jsonWriter, data).ConfigureAwait(false);
            StatsDataSetParser.ClearUnauthenticatedData(isAuthenticated, data);
            await StatsDataSetParser.CreateTopClientsArrayAsync(jsonWriter, data).ConfigureAwait(false);
            await StatsDataSetParser.CreateTopDomainsAsync(jsonWriter, data).ConfigureAwait(false);
            await StatsDataSetParser.CreateTopBlockedDomainsAsync(jsonWriter, data).ConfigureAwait(false);
            await StatsDataSetParser.CreateLastQueriesArrayAsync(jsonWriter, data).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.Log(exception);
        }
    }


    internal bool ApiIsSessionValid(string token)
    {
        return SessionManager.IsSessionValid(token);
    }

    internal void WriteToLog(Exception exception, bool writeToConsole = true)
    {
        Logger.Log(exception, string.Empty, writeToConsole);
    }

    internal void WriteToLog(string message, bool writeToConsole = true)
    {
        Logger.Log(message, writeToConsole: writeToConsole);
    }

    internal void ApiListLogs(JsonTextWriter jsonWriter)
    {
        var logFiles = Directory.GetFiles(DllInfo.LogFolder, "*.log");

        Array.Sort(logFiles);
        Array.Reverse(logFiles);

        jsonWriter.WritePropertyName("logFiles");
        jsonWriter.WriteStartArray();

        foreach (var logFile in logFiles)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("fileName");
            jsonWriter.WriteValue(Path.GetFileNameWithoutExtension(logFile));

            jsonWriter.WritePropertyName("size");
            jsonWriter.WriteValue(WebHelper.GetFormattedSize(new FileInfo(logFile).Length));

            jsonWriter.WriteEndObject();
        }

        jsonWriter.WriteEndArray();
    }

    internal static void ApiDeleteLog(string log)
    {
        if (string.IsNullOrEmpty(log))
        {
            throw new WebException("EonaCatDns: " + "EonaCatDns: Parameter 'log' missing.");
        }

        // Check if the log file is valid
        if (!IsValidLogFile(log))
        {
            return;
        }

        var logFile = Path.Combine(Logger.LogFolder, log + ".log");
        if (Logger.CurrentLogFile.Equals(logFile, StringComparison.CurrentCultureIgnoreCase))
        {
            Logger.DeleteCurrentLogFile();
        }
        else
        {
            File.Delete(logFile);
        }

        Logger.Log("Log file was deleted: " + log);
    }

    internal void ApiLogs(HttpResponse response, string logFileName)
    {
        try
        {
            // Check if the log file is valid
            if (!IsValidLogFile(logFileName))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var logFile = Path.Combine(Logger.LogFolder, logFileName);
            Logger.DownloadLogAsync(response.HttpContext, logFile).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.Log(exception);
        }
    }

    private static bool IsValidLogFile(string logFile)
    {
        // Check if the logFile contains more than one dot
        if (logFile.Count(c => c == '.') > 1)
            return false;

        // Check if the logFile contains directory separators
        if (logFile.Contains("/") || logFile.Contains("\\"))
            return false;

        // Whitelisted file extensions
        var whitelistPatterns = new[]
        {
            "*.txt",
            "*.log"
        };

        // Check if the logFile matches any whitelisted pattern
        return whitelistPatterns.Any(pattern => IsMatch(logFile, pattern));
    }

    private static bool IsMatch(string input, string pattern)
    {
        return Regex.IsMatch(input, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$");
    }
}