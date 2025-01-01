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
using System.Threading;
using System.Threading.Tasks;

namespace EonaCat.Dns.Core.Clients;

internal abstract class DnsClientBase
{
    private int _nextQueryId = new Random().Next(ushort.MaxValue + 1);

    public ushort GetNextQueryId()
    {
        var next = Interlocked.Increment(ref _nextQueryId);
        return (ushort)next;
    }

    public Task<Message> QueryAsync(
        string name,
        RecordType recordType,
        CancellationToken cancel = default)
    {
        var query = new Message
        {
            Header = new DnsHeader
            {
                Id = GetNextQueryId(),
                IsRecursionDesired = true
            }
        };
        query.Questions.Add(new Question { Name = name, Type = recordType });

        return QueryAsync(query, cancel);
    }

    public Task<Message> QuerySecureAsync(
        string name,
        RecordType recordType,
        CancellationToken cancel = default)
    {
        var query = new Message
        {
            Header = new DnsHeader
            {
                Id = GetNextQueryId(),
                IsRecursionDesired = true
            }
        }.UseDnsSecurity();
        query.Questions.Add(new Question { Name = name, Type = recordType });

        return QueryAsync(query, cancel);
    }

    public abstract Task<Message> QueryAsync(
        Message request,
        CancellationToken cancel = default);

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}