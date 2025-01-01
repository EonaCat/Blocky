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
using EonaCat.Helpers.Commands;

namespace EonaCat.Dns.Models;

internal class TaskItem
{
    public Uri Uri { get; set; }
    public double Progress { get; set; }
    public int Current { get; set; }
    public int Total { get; set; }
    public string Status { get; set; }
    public AsyncCommand<object> Task { get; set; }
}