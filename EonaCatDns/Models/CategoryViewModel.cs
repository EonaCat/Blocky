﻿/*
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

using EonaCat.Dns.Database.Models.Entities;
using System.Collections.Generic;

namespace EonaCat.Dns.Models;

public class CategoryViewModel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Domain> Domains { get; set; }
}