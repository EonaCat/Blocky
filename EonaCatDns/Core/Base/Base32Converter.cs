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

namespace EonaCat.Dns.Core.Base;

public static class Base32Converter
{
    public static string ToString(byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException("EonaCatDns: " + nameof(bytes));
        }

        return ToString(bytes, 0, bytes.Length);
    }

    public static string ToString(byte[] bytes, int offset, int count)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException("EonaCatDns: " + nameof(bytes));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(offset));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(count));
        }

        if (offset + count > bytes.Length)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(count));
        }

        if (count == 0)
        {
            return string.Empty;
        }

        return BaseEncoding.Base32.GetString(bytes, offset, count);
    }

    public static byte[] ToBytes(string base32String)
    {
        if (base32String == null)
        {
            throw new ArgumentNullException("EonaCatDns: " + nameof(base32String));
        }

        return ToBytes(base32String, 0, base32String.Length);
    }

    public static byte[] ToBytes(string base32String, int offset, int count)
    {
        if (base32String == null)
        {
            throw new ArgumentNullException("EonaCatDns: " + nameof(base32String));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(offset));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(count));
        }

        if (offset + count > base32String.Length)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(count));
        }

        if (count == 0)
        {
            return Array.Empty<byte>();
        }

        return BaseEncoding.Base32.GetBytes(base32String, offset, count);
    }
}