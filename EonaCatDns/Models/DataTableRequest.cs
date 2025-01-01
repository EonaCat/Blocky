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

using Microsoft.AspNetCore.Mvc;

namespace EonaCat.Dns.Models;

public class DataTableRequest
{
    [FromQuery(Name = "sEcho")] public string Echo { get; set; }

    [FromQuery(Name = "iDisplayStart")] public int Start { get; set; }

    [FromQuery(Name = "iDisplayLength")] public int End { get; set; }

    [FromQuery(Name = "sSearch")] public string Search { get; set; }

    [FromQuery(Name = "iSortCol_0")] public int SortColumn { get; set; }

    [FromQuery(Name = "sSortDir_0")] public string SortDirection { get; set; }
}