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

namespace EonaCat.Dns.Core.Base
{
    public static class Base16Converter
    {
        public static string ToString(byte[] bytes, bool lowerCase = false)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("EonaCatDns: " + nameof(bytes));
            }

            return ToString(bytes, 0, bytes.Length, lowerCase);
        }

        public static string ToString(byte[] bytes, int offset, int count, bool lowerCase = false)
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

            return (lowerCase ? BaseEncoding.Base16LowerCase : BaseEncoding.Base16UpperCase).GetString(bytes, offset, count);
        }

        public static byte[] ToBytes(string base16String)
        {
            if (base16String == null)
            {
                throw new ArgumentNullException("EonaCatDns: " + nameof(base16String));
            }

            return ToBytes(base16String, 0, base16String.Length);
        }

        public static byte[] ToBytes(string base16String, int offset, int count)
        {
            if (base16String == null)
            {
                throw new ArgumentNullException("EonaCatDns: " + nameof(base16String));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(offset));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(count));
            }

            if (offset + count > base16String.Length)
            {
                throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(count));
            }

            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            var isLowerCase = true;
            for (var i = offset; i < offset + count; i++)
            {
                if (base16String[i] >= 'A' && base16String[i] <= 'F')
                {
                    isLowerCase = false;
                }
            }

            return (isLowerCase ? BaseEncoding.Base16LowerCase : BaseEncoding.Base16UpperCase).GetBytes(base16String, offset, count);
        }
    }
}