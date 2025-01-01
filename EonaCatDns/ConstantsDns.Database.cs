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

using System.IO;

namespace EonaCat.Dns;

public static partial class ConstantsDns
{
    public static class Database
    {
        public static string Name { get; internal set; } = "eonacatdns.db";
        public static string LogsName { get; internal set; } = "eonacatdnslog.db";
        public static string DomainsName { get; internal set; } = "eonacatdnsdomain.db";
        public static string Directorypath { get; internal set; } = DllInfo.AppFolder;
        public static string Databasepath { get; internal set; } = Path.Combine(Directorypath, Name);
        public static string DomainDatabasepath { get; internal set; } = Path.Combine(Directorypath, DomainsName);
        public static string LogsDatabasepath { get; internal set; } = Path.Combine(Directorypath, LogsName);
    }
}