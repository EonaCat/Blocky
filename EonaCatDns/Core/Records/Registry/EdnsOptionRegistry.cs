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

using System;
using System.Collections.Generic;

namespace EonaCat.Dns.Core.Records.Registry;

public static class EdnsOptionRegistry
{
    public static Dictionary<EdnsOptionType, Func<EdnsOptionBase>> Options;

    static EdnsOptionRegistry()
    {
        Options = new Dictionary<EdnsOptionType, Func<EdnsOptionBase>>();
        AddToStore<EdnsPaddingOption>();
        AddToStore<EdnsNsidOption>();
        AddToStore<EdnsKeepaliveOption>();
        AddToStore<EdnsDauOption>();
        AddToStore<EdnsDhuOption>();
        AddToStore<EdnsN3UOption>();
    }

    public static void AddToStore<T>() where T : EdnsOptionBase, new()
    {
        var option = new T();
        Options.Add(option.Type, () => new T());
    }
}