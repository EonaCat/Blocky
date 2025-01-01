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
using System.Collections.Generic;

namespace EonaCat.Dns.Extensions;

public static class CollectionExtension
{
    private static readonly Random Random = new();

    public static T RandomElement<T>(this IList<T> list)
    {
        return list[Random.Next(list.Count)];
    }

    public static T RandomElement<T>(this T[] array)
    {
        return array[Random.Next(array.Length)];
    }

    public static void Shuffle<T>(this IList<T> list)
    {
        var length = list.Count;
        var random = new Random();

        for (var i = length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[j], list[i]) = (list[i], list[j]);
        }
    }
}