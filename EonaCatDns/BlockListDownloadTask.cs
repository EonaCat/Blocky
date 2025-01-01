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
        /// <summary>
        /// Generate the download task
        /// </summary>
        /// <param name="blockListsFolder"></param>
        /// <param name="blockList"></param>
        /// <param name="progressToConsole"></param>
        /// <returns></returns>
        public async Task GenerateDownloadTaskAsync(string blockListsFolder, BlockList blockList, bool progressToConsole = false)
        {
            var blockListFileName = GetBlockListFileName(blockList.Url);
            var blockListFilePath = Path.Combine(blockListsFolder, blockListFileName);

            try
            {
                await Logger.LogAsync($"Downloading file for {blockList.Url}", ELogType.DEBUG).ConfigureAwait(false);
                await WebHelper.DownloadFile(new Uri(blockList.Url), blockListFilePath + ".downloading").ConfigureAwait(false);
                await Logger.LogAsync($"File downloaded for {blockList.Url}", ELogType.DEBUG).ConfigureAwait(false);

                if (!File.Exists(blockListFilePath + ".downloading"))
                {
                    await Logger.LogAsync("BlockList download path doesn't exist", ELogType.ERROR).ConfigureAwait(false);
                    return;
                }

                File.Move(blockListFilePath + ".downloading", blockListFilePath, true);
                await ProcessBlockListAsync(blockList, blockListFilePath, progressToConsole).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.LogAsync($"Failed to download or process block list from {blockList.Url}: {ex.Message}", ELogType.ERROR).ConfigureAwait(false);
            }
        }

        private static async Task<int> CountTotalLinesAsync(string filePath)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            using var reader = new StreamReader(fileStream);

            int totalLines = 0;
            while (await reader.ReadLineAsync() is not null)
            {
                totalLines++;
            }

            return totalLines;
        }

        private static async Task ProcessBlockListAsync(BlockList blockList, string blockListFilePath, bool progressToConsole)
        {
            var domains = new List<string>();

            try
            {
                using var fileStream = new FileStream(blockListFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                using var reader = new StreamReader(fileStream);

                var tasks = new List<Task>();

                int totalNumberOfLines = await CountTotalLinesAsync(blockListFilePath).ConfigureAwait(false);
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    tasks.Add(ParseLineAsync(line, domains, blockList.Url, progressToConsole, totalNumberOfLines));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                await Logger.LogAsync($"DNS Server failed to update block list from: {blockList.Url} : {blockListFilePath}\r\n{ex.Message}", ELogType.ERROR);
            }

            // Update block list and save domains to the database
            await UpdateBlockListAndSaveToDatabase(blockList, domains);
        }

        private static async Task ParseLineAsync(string line, List<string> domains, string blockListUrl, bool progressToConsole, int totalNumberOfLines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                return;

            line = line.Trim();

            var words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2)
                return;

            var hostname = words[0];
            var ipAddress = words[1];

            if (IPAddress.TryParse(hostname, out _))
            {
                // Swap if the first word is an IP address
                var temp = hostname;
                hostname = ipAddress;
                ipAddress = temp;
            }

            if (string.IsNullOrWhiteSpace(hostname) || Blocker.AllowList.Contains(hostname))
                return;

            domains.Add(hostname);

            if (progressToConsole)
            {
                await ReportProgressAsync(new Uri(blockListUrl), domains.Count, totalNumberOfLines);
            }
        }

        private static async Task ReportProgressAsync(Uri blockListUrl, int currentItemIndex, int totalItems)
        {
            double progressPercentage = (double)currentItemIndex / totalItems * 100;
            await Logger.LogAsync($"{progressPercentage:F2}% ({currentItemIndex}/{totalItems}) - {blockListUrl}", ELogType.TRAFFIC);
        }

        private static async Task UpdateBlockListAndSaveToDatabase(BlockList blockList, List<string> domains)
        {
            try
            {
                if (domains.Count == 0)
                    return;

                blockList.LastUpdateStartTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                blockList.TotalEntries = domains.Count;
                blockList = await DatabaseManager.BlockLists.InsertOrUpdateAsync(blockList);

                await SaveDomainsToDatabase(blockList.Url, domains);
                await Logger.LogAsync("DNS Server Blocked zone was updated from: " + blockList.Url, ELogType.DEBUG);
            }
            catch (Exception ex)
            {
                await Logger.LogAsync($"Failed to update block list and save domains to database: {ex.Message}", ELogType.ERROR);
            }
        }

        private static async Task SaveDomainsToDatabase(string blockListUrl, List<string> domains)
        {
            var address = ConstantsDns.DefaultRedirectionAddress;
            var domainList = domains.Select(domain => new Domain
            {
                Url = domain,
                ForwardIp = address,
                ListType = ListType.Blocked,
                FromBlockList = blockListUrl
            }).ToList();

            var count = await DatabaseManager.Domains.BulkInsertOrUpdateAsync(domainList);
            await Logger.LogAsync($"{count.Count()} domain entries saved to database", ELogType.DEBUG);
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
