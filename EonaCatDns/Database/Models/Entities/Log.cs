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
using EonaCat.Helpers;

namespace EonaCat.Dns.Database.Models.Entities;

public class Log : EntityBase
{
    public enum ResultType
    {
        Success,
        Blocked
    }

    public Log()
    {
        DateTime = System.DateTime.Now.ToUnixTime();
    }

    public string ClientIp { get; set; }
    public string Request { get; set; }
    public ResultType Result { get; set; }
    public RecordType RecordType { get; set; }
    public ResponseCode ResponseCode { get; set; }
    public bool IsFromCache { get; set; }
    public bool IsBlocked { get; set; }
    public string Raw { get; set; }
    public long DateTime { get; set; }
}