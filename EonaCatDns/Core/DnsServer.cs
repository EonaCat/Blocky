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

using System.Threading.Tasks;

namespace EonaCat.Dns.Core;

internal class DnsServer
{
    private readonly EonaCatDnsConfig _config;

    public DnsServer(EonaCatDnsConfig config)
    {
        _config = config;
    }

    private Server Server { get; set; }

    public bool IsRunning { get; private set; }

    public async Task StopAsync()
    {
        await Server.Stop().ConfigureAwait(false);
    }

    public async Task StartAsync()
    {
        Server = new Server();
        Server.WithConfig(_config);
        await Server.StartAsync().ConfigureAwait(false);
        IsRunning = Server.IsRunning;
    }
}