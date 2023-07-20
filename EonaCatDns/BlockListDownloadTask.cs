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

using EonaCat.Dns.Core;
using EonaCat.Dns.Database;
using EonaCat.Dns.Database.Models.Entities;
using EonaCat.Dns.Helpers;
using EonaCat.Dns.Models;
using EonaCat.Helpers.Helpers;
using EonaCat.Logger;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BlockList = EonaCat.Dns.Database.Models.Entities.BlockList;

namespace EonaCat.Dns;

public class BlockListDownloadTask
{
    public async Task GenerateDownloadTaskAsync(string address, string blockListsFolder, BlockList blockList)
    {
        var blockListFileName = BitConverter
            .ToString(SHA256.HashData(Encoding.UTF8.GetBytes(blockList.Url)))
            .Replace("-", "")
            .ToLower();

        var blockListFilePath = Path.Join(blockListsFolder, blockListFileName);
        var blockListDownloadFilePath = blockListFilePath + ".downloading";

        try
        {
            Logger.Log($"Downloading file for {blockList.Url}", ELogType.DEBUG);
            await WebHelper.DownloadFile(new Uri(blockList.Url), blockListDownloadFilePath).ConfigureAwait(false);
            Logger.Log($"File downloaded for {blockList.Url}", ELogType.DEBUG);
        }
        catch (Exception exception)
        {
            Logger.Log($"DNS Server failed to download block list and will use previously downloaded file (if available): {blockList.Url}\n{exception}", ELogType.ERROR);
            return;
        }

        if (File.Exists(blockListDownloadFilePath))
        {
            try
            {
                if (File.Exists(blockListFilePath))
                {
                    File.Delete(blockListFilePath);
                }
                File.Move(blockListDownloadFilePath, blockListFilePath);
            }
            catch (IOException)
            {
                // File in use by another process
                return;
            }
        }
        else
        {
            Logger.Log("BlockList download path doesn't exist");
            return;
        }

        if (File.Exists(blockListFilePath))
        {
            var fileContents = ReadLinesFromFile(blockListFilePath, false);
            blockList.LastUpdateStartTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            blockList.TotalEntries = fileContents.TotalLines;
            blockList = await DatabaseManager.BlockLists.InsertOrUpdateAsync(blockList).ConfigureAwait(false);

            if (blockList == null)
            {
                return;
            }

            var domains = new List<string>();
            await ParseBlockListFileAsync(new Uri(blockList.Url), domains, blockListFilePath, fileContents).ConfigureAwait(false);

            Logger.Log($"Retrieving distinct values for {blockList.Url}");
            domains = domains.Distinct().ToList();
            Logger.Log($"Retrieved distinct values for {blockList.Url}");

            Logger.Log($"Saving domains to database for {blockList.Url}");
            await SaveDomainsToDatabase(new Uri(blockList.Url).AbsoluteUri, domains, address).ConfigureAwait(false);
            domains.Clear();
            Logger.Log($"Saved domains to database for {blockList.Url}");

            blockList.LastUpdated = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            blockList.LastResult = fileContents.Status;
            await DatabaseManager.BlockLists.InsertOrUpdateAsync(blockList).ConfigureAwait(false);

            await Blocker.GetBlockListCountFromDatabaseAsync().ConfigureAwait(false);
        }
        else
        {
            Logger.Log("BlockList path doesn't exist");
        }
    }

    private async Task SaveDomainsToDatabase(string blockListUrl, List<string> domains, string address = null)
    {
        if (string.IsNullOrEmpty(address))
        {
            address = ConstantsDns.DefaultRedirectionAddress;
        }

        var messageBuilder = new StringBuilder().Append(blockListUrl).Append(" saving");

        using (new PerformanceTimer(messageBuilder.ToString()))
        {
            var domainList = domains.Select(domain => new Domain
            {
                Url = domain,
                ForwardIp = address,
                ListType = ListType.Blocked,
                FromBlockList = blockListUrl
            }).ToList();

            await DatabaseManager.Domains.BulkInsertOrUpdateAsync(domainList).ConfigureAwait(false);

            Logger.Log("Domain entries saved to database");

            await Blocker.GetBlockListCountFromDatabaseAsync().ConfigureAwait(false);
        }
    }

    private static FileContents ReadLinesFromFile(string path, bool countOnly = true)
    {
        var totalLines = 0;
        var lines = new List<string>();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        if (countOnly)
        {
            totalLines = File.ReadAllLines(path).Length;
        }
        else
        {
            lines = File.ReadAllLines(path).ToList();
            totalLines = lines.Count;
        }

        watch.Stop();
        var elapsedMs = watch.ElapsedMilliseconds;

        var fileContents = new FileContents
        {
            Lines = lines,
            ElapsedMilliSeconds = elapsedMs,
            Path = path,
            TotalLines = totalLines
        };
        return fileContents;
    }

    private static async Task ParseBlockListFileAsync(Uri blockListUrl, List<string> domains, string blockListFilePath, FileContents fileContents)
    {
        // Remove all domains from the specified blockList
        await DatabaseManager.Domains.Where(x => x.FromBlockList == blockListUrl.AbsoluteUri).DeleteAsync().ConfigureAwait(false);
        var exceptions = new StringBuilder();

        try
        {
            using (var timer = new PerformanceTimer($"{blockListUrl.AbsoluteUri} retrieval"))
            {
                fileContents.Status = "OK";

                var taskList = Blocker.RunningBlockerTasks.ToList();
                var task = taskList.FirstOrDefault(x => x.Uri.AbsoluteUri == blockListUrl.AbsoluteUri);
                if (task == null)
                {
                    fileContents.Status = "Invalid blockList task";
                    return;
                }

                for (var i = 0; i < fileContents.Lines.Count; i++)
                {
                    var line = fileContents.Lines[i];

                    try
                    {
                        line = line.TrimStart(' ', '\t').TrimEnd();
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                        {
                            continue; //skip comment/empty line
                        }

                        var firstWord = PopWord(ref line);
                        var secondWord = PopWord(ref line);

                        string ipAddress = null;
                        string hostname = null;

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
                            continue;
                        }

                        domains.Add(hostname);
                    }
                    catch (Exception exception)
                    {
                        exceptions.AppendLine(exception.Message);
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                    finally
                    {
                        ReportProgress(blockListUrl, i + 1, fileContents.TotalLines, task);
                    }
                }

                if (exceptions.Length == 0)
                {
                    Logger.Log($"Parsed file for {blockListUrl.AbsoluteUri}", ELogType.DEBUG);
                    Logger.Log("DNS Server Blocked zone was updated from: " + blockListFilePath, ELogType.DEBUG);
                }
                else
                {
                    Logger.Log($"Failed to parse file for {blockListUrl.AbsoluteUri}", ELogType.ERROR);
                    Logger.Log(exceptions.ToString(), ELogType.ERROR);
                    fileContents.Status = "ERROR";
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log("DNS Server failed to update block list from: " + blockListUrl.AbsoluteUri + " : " + blockListFilePath +
                       "\r\n" + ex.Message, ELogType.ERROR);
            fileContents.Status = "ERROR";
        }
    }

    private static void ReportProgress(Uri blockListUrl, int currentItemIndex, int totalItems, TaskItem task)
    {
        var currentProgress = Blocker.GetProgress(currentItemIndex, totalItems);
        task.Progress = currentProgress;
        task.Current = currentItemIndex;
        task.Total = totalItems;
        task.Uri = blockListUrl;
        task.Status = string.Empty;
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