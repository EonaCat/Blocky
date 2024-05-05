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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Helpers;
using EonaCat.Dns.Models;
using EonaCat.Helpers.Helpers;
using EonaCat.Logger;
using BlockList = EonaCat.Dns.Database.Models.Entities.BlockList;

namespace EonaCat.Dns;

public class BlockListDownloadTask
{
    private const int FILE_PARSING_WAIT_IN_MILLISECONDS = 1;
    public bool ProgressToConsole { get; set; }

    public async Task GenerateDownloadTaskAsync(string address, string blockListsFolder, BlockList blockList,
        bool progressToConsole = false)
    {
        ProgressToConsole = progressToConsole;
        var blockListFileName = GetBlockListFileName(blockList.Url);
        var blockListFilePath = Path.Combine(blockListsFolder, blockListFileName);
        var blockListDownloadFilePath = blockListFilePath + ".downloading";

        try
        {
            await Logger.LogAsync($"Downloading file for {blockList.Url}", ELogType.DEBUG);
            await WebHelper.DownloadFile(new Uri(blockList.Url), blockListDownloadFilePath);
            await Logger.LogAsync($"File downloaded for {blockList.Url}", ELogType.DEBUG);

            if (!File.Exists(blockListDownloadFilePath))
            {
                await Logger.LogAsync("BlockList download path doesn't exist", ELogType.ERROR);
                return;
            }

            File.Move(blockListDownloadFilePath, blockListFilePath, true);
            await ProcessBlockList(blockList, blockListFilePath);
        }
        catch (Exception ex)
        {
            await Logger.LogAsync($"Failed to download or process block list from {blockList.Url}: {ex.Message}",
                ELogType.ERROR);
        }
    }

    private async Task ProcessBlockList(BlockList blockList, string blockListFilePath)
    {
        // Read all the lines from the file
        await Task.Run(async () =>
        {
            var fileContents = await ReadLinesFromFileAsync(blockListFilePath);
            if (fileContents != null)
            {
                blockList.LastUpdateStartTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                blockList.TotalEntries = fileContents.TotalLines;
                blockList = await DatabaseManager.BlockLists.InsertOrUpdateAsync(blockList);
                await ParseBlockListFileAsync(new Uri(blockList.Url), blockListFilePath, fileContents);
            }
        }).ConfigureAwait(false);
    }

    private static async Task<FileContents> ReadLinesFromFileAsync(string path)
    {
        var lines = new ConcurrentBag<string>();
        var watch = Stopwatch.StartNew();

        try
        {
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var reader = new StreamReader(fileStream))
            {
                while (await reader.ReadLineAsync() is { } line)
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line);
                    }
            }
        }
        catch (Exception exception)
        {
            await Logger.LogAsync($"Error reading file '{path}': {exception.Message}", ELogType.ERROR);
            return null;
        }

        watch.Stop();
        var elapsedMs = watch.ElapsedMilliseconds;

        return new FileContents
        {
            Lines = lines.ToList(),
            ElapsedMilliSeconds = elapsedMs,
            Path = path,
            TotalLines = lines.Count
        };
    }

    private static string GetBlockListFileName(string url)
    {
        return BitConverter
            .ToString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))
            .Replace("-", "")
            .ToLower();
    }

    private static async Task SaveDomainsToDatabase(string blockListUrl, List<string> domains)
    {
        if (domains.Count == 0)
        {
            return;
        }

        var address = ConstantsDns.DefaultRedirectionAddress;
        var domainList = domains.Select(domain => new Domain
        {
            Url = domain,
            ForwardIp = address,
            ListType = ListType.Blocked,
            FromBlockList = blockListUrl
        }).ToList();

        try
        {
            await Task.Run(async () =>
            {
                var count = await DatabaseManager.Domains.BulkInsertOrUpdateAsync(domainList).ConfigureAwait(false);
                await Logger.LogAsync($"{count} domain entries saved to database", ELogType.DEBUG)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Logger.LogAsync($"Failed to save domain entries to database: {ex.Message}", ELogType.ERROR)
                .ConfigureAwait(false);
        }
    }

    private async Task ParseBlockListFileAsync(Uri blockListUrl, string blockListFilePath, FileContents fileContents)
    {
        var domains = new ConcurrentBag<string>();

        // Remove domains from blocklist asynchronously
        await DatabaseManager.Domains.Where(x => x.FromBlockList == blockListUrl.AbsoluteUri).DeleteAsync()
            .ConfigureAwait(false);

        try
        {
            using (new PerformanceTimer($"{blockListUrl.AbsoluteUri} retrieval"))
            {
                fileContents.Status = "OK";

                var task = Blocker.RunningBlockerTasks.FirstOrDefault(
                    x => x.Uri.AbsoluteUri == blockListUrl.AbsoluteUri);
                if (task == null)
                {
                    fileContents.Status = "Invalid blockList task";
                    return;
                }

                var tasks = fileContents.Lines
                    .Select(async (line, index) =>
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                        {
                            await ReportProgressAsync(blockListUrl, index + 1, fileContents.TotalLines, task)
                                .ConfigureAwait(false);
                            return;
                        }

                        line = line.TrimStart(' ', '\t').TrimEnd();

                        var firstWord = PopWord(ref line);
                        var secondWord = PopWord(ref line);

                        string ipAddress = null;
                        string hostname;

                        if (!IPAddress.TryParse(firstWord, out var _))
                        {
                            hostname = firstWord;
                            ipAddress = secondWord;
                        }
                        else
                        {
                            ipAddress = firstWord;
                            hostname = secondWord;
                        }

                        if (string.IsNullOrWhiteSpace(hostname))
                        {
                            hostname = firstWord;
                        }

                        if (string.IsNullOrWhiteSpace(hostname) || Blocker.AllowList.Contains(hostname))
                        {
                            await ReportProgressAsync(blockListUrl, index + 1, fileContents.TotalLines, task)
                                .ConfigureAwait(false);
                            return;
                        }

                        domains.Add(hostname);
                        await ReportProgressAsync(blockListUrl, index + 1, fileContents.TotalLines, task)
                            .ConfigureAwait(false);
                        await Task.Delay(FILE_PARSING_WAIT_IN_MILLISECONDS).ConfigureAwait(false);
                    });

                await Task.WhenAll(tasks);

                if (fileContents.Status == "OK")
                {
                    await Logger.LogAsync($"Parsed file for {blockListUrl.AbsoluteUri}", ELogType.DEBUG)
                        .ConfigureAwait(false);

                    await Task.Run(async () =>
                    {
                        await SaveDomainsToDatabase(blockListUrl.AbsoluteUri, domains.ToList()).ConfigureAwait(false);
                        await Logger.LogAsync("DNS Server Blocked zone was updated from: " + blockListFilePath,
                                ELogType.DEBUG)
                            .ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }
                else
                {
                    await Logger.LogAsync($"Failed to parse file for {blockListUrl.AbsoluteUri}", ELogType.ERROR)
                        .ConfigureAwait(false);
                    await Logger.LogAsync($"Reason: {fileContents.Status}", ELogType.ERROR).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            await Logger.LogAsync("DNS Server failed to update block list from: " + blockListUrl.AbsoluteUri + " : " +
                                  blockListFilePath +
                                  "\r\n" + ex.Message, ELogType.ERROR).ConfigureAwait(false);
            fileContents.Status = "ERROR";
        }
    }

    private async Task ReportProgressAsync(Uri blockListUrl, int currentItemIndex, int totalItems, TaskItem task)
    {
        var currentProgress = Blocker.GetProgress(currentItemIndex, totalItems);
        task.Progress = currentProgress;
        task.Current = currentItemIndex;
        task.Total = totalItems;
        task.Uri = blockListUrl;
        task.Status = string.Empty;

        if (ProgressToConsole)
        {
            await Logger
                .LogAsync($"{currentItemIndex}: {currentProgress}% ({currentItemIndex}/{totalItems}) - {blockListUrl}",
                    ELogType.TRAFFIC).ConfigureAwait(false);
        }
    }

    private static string PopWord(ref string line)
    {
        if (line == "")
        {
            return line;
        }

        line = line.TrimStart(' ', '\t');

        var i = line.IndexOf(' ');

        if (i < 0)
        {
            i = line.IndexOf('\t');
        }

        string word;

        if (i < 0)
        {
            word = line;
            line = "";
        }
        else
        {
            word = line.Substring(0, i);
            line = line.Substring(i + 1);
        }

        return word;
    }
}