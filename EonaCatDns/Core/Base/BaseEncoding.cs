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
using System.Text;

namespace EonaCat.Dns.Core.Base
{
    public sealed class BaseEncoding : Encoding
    {
        public static readonly BaseEncoding Base16UpperCase = new BaseEncoding(BaseAlphabet.Base16UpperCaseAlphabet, "base16-upper");

        public static readonly BaseEncoding Base16LowerCase = new BaseEncoding(BaseAlphabet.Base16LowerCaseAlphabet, "base16-lower");

        public static readonly BaseEncoding Base32 = new BaseEncoding(BaseAlphabet.Base32Alphabet, "base32");

        private readonly BaseEncoder _encoder;
        private readonly BaseDecoder _baseDecoder;

        public BaseAlphabet Alphabet { get; }

        public override string EncodingName { get; }

        public override bool IsSingleByte => Alphabet.EncodingBlockSize == 1;

        public BaseEncoding(BaseAlphabet baseNAlphabet, string encodingName)
        {
            EncodingName = encodingName ?? throw new ArgumentNullException("EonaCatDns: " + nameof(encodingName));
            Alphabet = baseNAlphabet ?? throw new ArgumentNullException("EonaCatDns: " + nameof(baseNAlphabet));

            _encoder = new BaseEncoder(baseNAlphabet);
            _baseDecoder = new BaseDecoder(baseNAlphabet);
        }

        public override int GetByteCount(char[] chars, int index, int count)
        {
            return _encoder.GetByteCount(chars, index, count, flush: true);
        }

        public override int GetByteCount(string s)
        {
            return _encoder.GetByteCount(s, 0, s.Length, flush: true);
        }

        public override unsafe int GetByteCount(char* chars, int count)
        {
            return _encoder.GetByteCount(chars, count, flush: true);
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            return _encoder.GetBytes(chars, charIndex, charCount, bytes, byteIndex, flush: true);
        }

        public override int GetBytes(string s, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            _encoder.Convert(s, charIndex, charCount, bytes, byteIndex, bytes.Length - byteIndex, flush: true, out _, out var bytesUsed, out _);
            return bytesUsed;
        }

        public override unsafe int GetBytes(char* chars, int charCount, byte* bytes, int byteCount)
        {
            return _encoder.GetBytes(chars, charCount, bytes, byteCount, flush: true);
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            return _baseDecoder.GetCharCount(bytes, index, count);
        }

        public override unsafe int GetCharCount(byte* bytes, int count)
        {
            return _baseDecoder.GetCharCount(bytes, count, flush: true);
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            return _baseDecoder.GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush: true);
        }

        public override unsafe int GetChars(byte* bytes, int byteCount, char* chars, int charCount)
        {
            return _baseDecoder.GetChars(bytes, byteCount, chars, charCount, flush: true);
        }

        public override unsafe string GetString(byte[] bytes, int index, int count)
        {
            var charCount = _baseDecoder.GetCharCount(bytes, index, count);
            if (charCount == 0)
            {
                return string.Empty;
            }
            var output = new string('\0', charCount);
            fixed (char* outputPtr = output)
            {
                _baseDecoder.Convert(new ReadOnlySpan<byte>(bytes, index, count), new Span<char>(outputPtr, output.Length), flush: true, out _, out _, out _);
            }
            return output;
        }

        public override int GetMaxByteCount(int charCount)
        {
            return _encoder.GetMaxByteCount(charCount);
        }

        public override int GetMaxCharCount(int byteCount)
        {
            return _baseDecoder.GetMaxCharCount(byteCount);
        }

        public override Decoder GetDecoder()
        {
            return _baseDecoder;
        }

        public override Encoder GetEncoder()
        {
            return _encoder;
        }

        public override string ToString() => EncodingName;
    }
}