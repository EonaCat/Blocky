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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EonaCat.Dns.Core.Records;
using EonaCat.Helpers.Controls;

namespace EonaCat.Dns.Core.Servers
{
    internal partial class Ns
    {
        private readonly ConcurrentDictionary<string, List<ResourceRecord>> _cache = new();

        internal Catalog Catalog { get; set; }
        public bool AnswerAllQuestions { get; set; }
        public bool IsCacheDisabled { get; set; }

        public Ns()
        {
            var cacheTimer = new EonaCatTimer(TimeSpan.FromSeconds(10), CleanExpiredRecords);
            cacheTimer.Start();
        }

        private void CleanExpiredRecords()
        {
            if (IsCacheDisabled)
                return;

            foreach (var (question, resourceRecords) in _cache.ToArray())
            {
                var validRecords = resourceRecords
                    .Where(r => !(r.IsExpired || r.HasPacketError))
                    .ToList();
                _cache[question] = validRecords;
            }
        }

        public async Task<Message> QueryAsync(Message request, CancellationToken cancel = default)
        {
            var response = request.CreateResponse();
            var tasks = request.Questions.Select(question => QueryQuestionAsync(question, response, cancel));
            await Task.WhenAll(tasks).ConfigureAwait(false);

            if (response.Answers.Count > 0)
                response.Header.ResponseCode = ResponseCode.NoError;

            await AddAdditionalRecordsAsync(response).ConfigureAwait(false);
            return await AddSecurityExtensionsAsync(request, response).ConfigureAwait(false);
        }

        private async Task QueryQuestionAsync(Question question, Message response, CancellationToken cancel)
        {
            if (TryGetAnswerFromCache(question.ToString(), out var cachedRecords) && !Server.WatchMode)
            {
                response.IsFromCache = true;
                response.Answers.AddRange(cachedRecords);
                return;
            }

            var found = await GetAnswerFromCatalogAsync(question, response, cancel).ConfigureAwait(false);
            var soa = GetAuthorityFromCatalog(question.Name);

            if (!found && response.Header.ResponseCode == ResponseCode.NoError)
                response.Header.ResponseCode = ResponseCode.NameError;

            if (found && soa != null)
            {
                var currentQuestion = new Question { Name = soa.Name, Class = soa.Class, Type = RecordType.Ns };
                await GetAnswerFromCatalogAsync(currentQuestion, response, cancel).ConfigureAwait(false);
                response.AuthorityRecords.AddRange(response.Answers.OfType<NsRecord>());
            }

            if (response.Header.ResponseCode == ResponseCode.NameError && soa != null)
                response.AuthorityRecords.Add(soa);

            CacheAnswer(question.ToString(), response.Answers);
        }

        private bool TryGetAnswerFromCache(string question, out List<ResourceRecord> cachedRecords)
        {
            if (IsCacheDisabled)
            {
                cachedRecords = new List<ResourceRecord>();
                return false;
            }

            return _cache.TryGetValue(question, out cachedRecords);
        }

        protected internal void CacheAnswer(string question, List<ResourceRecord> records)
        {
            if (IsCacheDisabled)
                return;

            _cache[question] = records;
        }

        private Task<bool> GetAnswerFromCatalogAsync(Question question, Message response, CancellationToken cancel)
        {
            if (Catalog == null || !Catalog.TryGetValue(question.Name, out var node))
                return Task.FromResult(false);

            response.Header ??= new DnsHeader();
            response.Header.AuthoritativeAnswer |= (AuthoritativeAnswer)Convert.ToInt32(node.IsAuthoritative && question.Class != RecordClass.Any);

            var resources = node.Resources
                .Where(x => (question.Class == RecordClass.Any || x.Class == question.Class) &&
                            (question.Type == RecordType.Any || x.Type == question.Type))
                .ToList();

            if (resources.Count == 0)
            {
                response.Questions.Add(question);
                resources = node.Resources.ToList();
            }

            response.Answers.AddRange(resources);
            return Task.FromResult(true);
        }

        protected SoaRecord GetAuthorityFromCatalog(DomainName domainName)
        {
            var name = domainName;
            while (name != null)
            {
                if (Catalog != null && Catalog.TryGetValue(name, out var node))
                {
                    var soa = node.Resources.OfType<SoaRecord>().FirstOrDefault();
                    if (soa != null)
                        return soa;
                }

                name = name.Parent();
            }

            return null;
        }

        protected async Task AddAdditionalRecordsAsync(Message response)
        {
            var extras = new Message();
            var totalAdditionalRecords =
                response.Answers.Count + response.AdditionalRecords.Count + response.AuthorityRecords.Count;
            extras.Answers = new List<ResourceRecord>(totalAdditionalRecords);

            await Task.WhenAll(response.Answers.Concat(response.AdditionalRecords).Concat(response.AuthorityRecords)
                .Select(resource => ProcessAdditionalRecordAsync(resource, extras))).ConfigureAwait(false);

            response.AdditionalRecords.AddRange(extras.Answers);
        }

        private async Task ProcessAdditionalRecordAsync(ResourceRecord resource, Message response)
        {
            switch (resource.Type)
            {
                case RecordType.A:
                case RecordType.Aaaa:
                    await GetAnswerFromCatalogAsync(CreateQuestion(resource.Name, resource.Class,
                        resource.Type == RecordType.A ? RecordType.Aaaa : RecordType.A), response, default)
                        .ConfigureAwait(false);
                    break;

                case RecordType.Ns:
                    await GetAnswerAsync(((NsRecord)resource).Authority, resource.Class, response).ConfigureAwait(false);
                    break;

                case RecordType.Ptr:
                    var ptr = (PtrRecord)resource;
                    await GetAnswerFromCatalogAsync(CreateQuestion(ptr.DomainName, resource.Class, RecordType.Any),
                        response, default).ConfigureAwait(false);
                    break;

                case RecordType.Soa:
                    await GetAnswerAsync(((SoaRecord)resource).PrimaryName, resource.Class, response).ConfigureAwait(false);
                    break;

                case RecordType.Srv:
                    await GetAnswerFromCatalogAsync(CreateQuestion(resource.Name, resource.Class, RecordType.Txt),
                        response, default).ConfigureAwait(false);
                    await GetAnswerAsync(((SrvRecord)resource).Target, resource.Class, response).ConfigureAwait(false);
                    break;
            }
        }

        private async Task GetAnswerAsync(DomainName name, RecordClass recordClass, Message response)
        {
            await GetAnswerFromCatalogAsync(CreateQuestion(name, recordClass, RecordType.A), response, default)
                .ConfigureAwait(false);
            await GetAnswerFromCatalogAsync(CreateQuestion(name, recordClass, RecordType.Aaaa), response, default)
                .ConfigureAwait(false);
        }

        private static Question CreateQuestion(DomainName name, RecordClass recordClass, RecordType recordType)
        {
            return new Question
            {
                Name = name,
                Class = recordClass,
                Type = recordType
            };
        }
    }
}
