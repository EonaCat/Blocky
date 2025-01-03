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

using EonaCat.Dns.Core;
using SQLite;

namespace EonaCat.Dns.Database.Models.Entities;

public class Domain : EntityBase
{
    public int? CategoryId { get; set; }

    [Ignore] public virtual Category Category { get; set; }

    public string Url { get; set; }
    public string ForwardIp { get; set; }
    public ListType ListType { get; set; }
    public string FromBlockList { get; set; }
}