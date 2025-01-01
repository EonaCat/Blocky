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
using System.Reflection;

namespace EonaCat.Dns.Extensions;

public static class StringExtensions
{
    public static T Convert<T>(this string value)
    {
        if (typeof(T).GetTypeInfo().IsEnum)
        {
            Enum.TryParse(typeof(T), value, true, out var result);
            return (T)result;
        }

        return (T)System.Convert.ChangeType(value, typeof(T));
    }
}