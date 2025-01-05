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
using System.Collections.Generic;
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
using EonaCat.Helpers.Helpers;
using EonaCat.Logger;
using BlockList = EonaCat.Dns.Database.Models.Entities.BlockList;

namespace EonaCat.Dns
{
    public class BlockListDownloadTask
    {
        public async Task GenerateDownloadTaskAsync(string blockListsFolder, BlockList blockList, bool progressToConsole = false)
        {
            Directory.CreateDirectory(blockListsFolder);
            await ProcessSingleBlockListAsync(blockListsFolder, blockList, progressToConsole).ConfigureAwait(false);
        }

        private async Task ProcessSingleBlockListAsync(string blockListsFolder, BlockList blockList, bool progressToConsole)
        {
            var blockListFilePath = Path.Combine(blockListsFolder, GetBlockListFileName(blockList.Url));

            try
            {
                await Logger.LogAsync($"Downloading file for {blockList.Url}", ELogType.DEBUG).ConfigureAwait(false);
                var tempFilePath = blockListFilePath + ".downloading";

                await WebHelper.DownloadFile(new Uri(blockList.Url), tempFilePath).ConfigureAwait(false);

                if (!File.Exists(tempFilePath))
                {
                    throw new FileNotFoundException("Downloaded file not found.", tempFilePath);
                }

                File.Move(tempFilePath, blockListFilePath, true);
                await ProcessBlockListAsync(blockList, blockListFilePath, progressToConsole).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.LogAsync($"Failed to download or process block list from {blockList.Url}: {ex.Message}", ELogType.ERROR).ConfigureAwait(false);
            }
        }

        private static async Task ProcessBlockListAsync(BlockList blockList, string blockListFilePath, bool progressToConsole)
        {
            var domains = new HashSet<string>();
            try
            {
                await StartUpdateBlockListToDatabase(blockList).ConfigureAwait(false);
                using var fileStream = new FileStream(blockListFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                using var reader = new StreamReader(fileStream);

                int totalLines = await CountTotalLinesAsync(reader).ConfigureAwait(false);
                fileStream.Seek(0, SeekOrigin.Begin);

                string line;
                int currentLine = 0;

                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    currentLine++;
                    ParseLine(line, domains);

                    if (progressToConsole && currentLine % 100 == 0)
                    {
                        await ReportProgressAsync(new Uri(blockList.Url), currentLine, totalLines).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                await Logger.LogAsync($"Failed to process block list from {blockList.Url}: {ex.Message}", ELogType.ERROR).ConfigureAwait(false);
            }

            await EndBlockListAndSaveToDatabase(blockList, domains).ConfigureAwait(false);
        }

        private static async Task<int> CountTotalLinesAsync(StreamReader reader)
        {
            int lineCount = 0;
            while (await reader.ReadLineAsync().ConfigureAwait(false) != null)
            {
                lineCount++;
            }
            return lineCount;
        }

        private static void ParseLine(string line, HashSet<string> domains)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                return;

            var words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2) return;

            var (hostname, ipAddress) = IPAddress.TryParse(words[0], out _) ? (words[1], words[0]) : (words[0], words[1]);

            if (!string.IsNullOrWhiteSpace(hostname) && !Blocker.AllowList.Contains(hostname))
            {
                domains.Add(hostname);
            }
        }

        private static async Task ReportProgressAsync(Uri blockListUrl, int currentLine, int totalLines)
        {
            double progress = (double)currentLine / totalLines * 100;
            await Logger.LogAsync($"{progress:F2}% ({currentLine}/{totalLines}) - {blockListUrl}", ELogType.TRAFFIC).ConfigureAwait(false);
        }

        private static async Task StartUpdateBlockListToDatabase(BlockList blockList)
        {
            try
            {
                blockList.LastUpdateStartTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                await DatabaseManager.BlockLists.InsertOrUpdateAsync(blockList).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.LogAsync($"Failed to initiate block list update: {ex.Message}", ELogType.ERROR).ConfigureAwait(false);
            }
        }

        private static async Task EndBlockListAndSaveToDatabase(BlockList blockList, HashSet<string> domains)
        {
            try
            {
                if (domains.Count > 0)
                {
                    blockList.TotalEntries = domains.Count;
                    blockList.LastUpdated = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                    await DatabaseManager.BlockLists.InsertOrUpdateAsync(blockList).ConfigureAwait(false);

                    await SaveDomainsToDatabase(blockList.Url, domains).ConfigureAwait(false);
                    await Blocker.GetBlockListCountFromDatabaseAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await Logger.LogAsync($"Failed to finalize block list update: {ex.Message}", ELogType.ERROR).ConfigureAwait(false);
            }
        }

        private static async Task SaveDomainsToDatabase(string blockListUrl, HashSet<string> domains)
        {
            var domainList = domains.Select(domain => new Domain
            {
                Url = domain,
                ForwardIp = ConstantsDns.DefaultRedirectionAddress,
                ListType = ListType.Blocked,
                FromBlockList = blockListUrl
            }).ToList();

            var insertedCount = await DatabaseManager.Domains.BulkInsertOrUpdateAsync(domainList).ConfigureAwait(false);
            await Logger.LogAsync($"{insertedCount} domain entries saved to database", ELogType.DEBUG).ConfigureAwait(false);
        }

        private static string GetBlockListFileName(string url)
        {
            return BitConverter
                .ToString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))
                .Replace("-", "")
                .ToLower();
        }
    }
}
