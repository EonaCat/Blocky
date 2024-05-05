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
using System.Security.Cryptography;
using System.Text;

namespace EonaCat.Dns.Core.Base;

public sealed class BaseEncoder : Encoder, ICryptoTransform
{
    public BaseEncoder(BaseAlphabet baseNAlphabet)
    {
        Alphabet = baseNAlphabet ?? throw new ArgumentNullException("EonaCatDns: " + nameof(baseNAlphabet));
    }

    public BaseAlphabet Alphabet { get; }

    int ICryptoTransform.InputBlockSize => Alphabet.DecodingBlockSize;

    int ICryptoTransform.OutputBlockSize => Alphabet.EncodingBlockSize;

    bool ICryptoTransform.CanTransformMultipleBlocks => true;

    bool ICryptoTransform.CanReuseTransform => true;

    int ICryptoTransform.TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer,
        int outputOffset)
    {
        Convert(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset, outputBuffer.Length - outputOffset,
            true, out _, out var outputUsed, out _);
        return outputUsed;
    }

    byte[] ICryptoTransform.TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
    {
        var outputBuffer = new byte[GetByteCount(inputBuffer, inputOffset, inputCount, true)];
        Convert(inputBuffer, inputOffset, inputCount, outputBuffer, 0, outputBuffer.Length, true, out _, out _, out _);
        return outputBuffer;
    }

    void IDisposable.Dispose()
    {
        Reset();
    }

    public override int GetByteCount(char[] characters, int index, int count, bool flush)
    {
        if (characters == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(characters));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(count));
        }

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(index));
        }

        if (count == 0)
        {
            return 0;
        }

        var alphabetInverse = Alphabet.AlphabetInverse;
        var bitsPerInputChar = Alphabet.EncodingBits;
        var inputEnd = index + count;
        for (; index < inputEnd; index++)
        {
            var baseNCharacter = characters[index];
            if (baseNCharacter > 127 || alphabetInverse[baseNCharacter] == BaseAlphabet.NotInAlphabet)
            {
                count--;
            }
        }

        if (!flush)
        {
            count -= count % Alphabet.DecodingBlockSize;
        }

        var bytesCount = (int)checked((ulong)count * (ulong)bitsPerInputChar / 8);
        return bytesCount;
    }

    public override unsafe int GetByteCount(char* characters, int count, bool flush)
    {
        if (characters == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(characters));
        }

        switch (count)
        {
            case < 0:
                throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(count));
            case 0:
                return 0;
        }

        var alphabetInverse = Alphabet.AlphabetInverse;
        var bitsPerInputChar = Alphabet.EncodingBits;
        var inputEnd = count;
        for (var index = 0; index < inputEnd; index++)
        {
            var baseNCharacter = characters[index];

            if (baseNCharacter > 127 || alphabetInverse[baseNCharacter] == BaseAlphabet.NotInAlphabet)
            {
                count--;
            }
        }

        if (!flush)
        {
            count -= count % Alphabet.DecodingBlockSize;
        }

        var bytesCount = (int)checked((ulong)count * (ulong)bitsPerInputChar / 8);
        return bytesCount;
    }

    public int GetByteCount(string chars, int index, int count, bool flush)
    {
        if (chars == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(chars));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(count));
        }

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(index));
        }

        if (count == 0)
        {
            return 0;
        }

        var alphabetInverse = Alphabet.AlphabetInverse;
        var bitsPerInputChar = Alphabet.EncodingBits;
        var inputEnd = index + count;
        for (; index < inputEnd; index++)
        {
            var baseNChar = chars[index];
            if (baseNChar > 127 || alphabetInverse[baseNChar] == BaseAlphabet.NotInAlphabet)
            {
                count--;
            }
        }

        if (!flush)
        {
            count -= count % Alphabet.DecodingBlockSize;
        }

        var bytesCount = (int)checked((ulong)count * (ulong)bitsPerInputChar / 8);
        return bytesCount;
    }

    public int GetByteCount(byte[] characters, int index, int count, bool flush)
    {
        if (characters == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(characters));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(count));
        }

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(index));
        }

        if (count == 0)
        {
            return 0;
        }

        var alphabetInverse = Alphabet.AlphabetInverse;
        var bitsPerInputChar = Alphabet.EncodingBits;
        var inputEnd = index + count;
        for (; index < inputEnd; index++)
        {
            var baseNCharacters = characters[index];

            if (baseNCharacters > 127 || alphabetInverse[baseNCharacters] == BaseAlphabet.NotInAlphabet)
            {
                count--;
            }
        }

        if (!flush)
        {
            count -= count % Alphabet.DecodingBlockSize;
        }

        var bytesCount = (int)checked((ulong)count * (ulong)bitsPerInputChar / 8);
        return bytesCount;
    }

    public int GetMaxByteCount(int charCount)
    {
        var bitsPerInputChar = Alphabet.EncodingBits;
        var bytesCount = (int)checked((ulong)charCount * (ulong)bitsPerInputChar / 8);
        return bytesCount;
    }

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush)
    {
        Convert(chars, charIndex, charCount, bytes, byteIndex, bytes.Length - byteIndex, flush, out _,
            out var bytesUsed, out _);
        return bytesUsed;
    }

    public override unsafe int GetBytes(char* chars, int charCount, byte* bytes, int byteCount, bool flush)
    {
        Convert(chars, charCount, bytes, byteCount, flush, out _, out var bytesUsed, out _);
        return bytesUsed;
    }

    public override unsafe void Convert(char* chars, int charCount, byte* bytes, int byteCount, bool flush,
        out int charsUsed, out int bytesUsed, out bool completed)
    {
        if (bytes == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(bytes));
        }

        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(byteCount));
        }

        if (chars == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(chars));
        }

        if (charCount < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(charCount));
        }

        DecodeInternal(new ReadOnlySpan<char>(chars, charCount), new Span<byte>(bytes, byteCount), flush, out charsUsed,
            out bytesUsed, out completed);
    }

    public override void Convert(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, int byteCount,
        bool flush, out int charsUsed, out int bytesUsed, out bool completed)
    {
        if (bytes == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(bytes));
        }

        if (byteIndex < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(byteIndex));
        }

        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(byteCount));
        }

        if (byteIndex + byteCount > bytes.Length)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(byteCount));
        }

        if (chars == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(chars));
        }

        if (charIndex < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(charIndex));
        }

        if (charIndex > chars.Length)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(charIndex));
        }

        DecodeInternal<char, byte>(chars.AsSpan(charIndex, charCount), bytes.AsSpan(byteIndex, byteCount), flush,
            out charsUsed, out bytesUsed, out completed);
    }

    public void Convert(byte[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, int byteCount,
        bool flush, out int charsUsed, out int bytesUsed, out bool completed)
    {
        if (bytes == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(bytes));
        }

        if (byteIndex < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(byteIndex));
        }

        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(byteCount));
        }

        if (byteIndex + byteCount > bytes.Length)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(byteCount));
        }

        if (chars == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(chars));
        }

        if (charIndex < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(charIndex));
        }

        if (charIndex > chars.Length)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(charIndex));
        }

        DecodeInternal<byte, byte>(chars.AsSpan(charIndex, charCount), bytes.AsSpan(byteIndex, byteCount), flush,
            out charsUsed, out bytesUsed, out completed);
    }

    public void Convert(string chars, int charIndex, int charCount, byte[] bytes, int byteIndex, int byteCount,
        bool flush, out int charsUsed, out int bytesUsed, out bool completed)
    {
        if (bytes == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(bytes));
        }

        if (byteIndex < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(byteIndex));
        }

        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(byteCount));
        }

        if (byteIndex + byteCount > bytes.Length)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(byteCount));
        }

        if (chars == null)
        {
            throw new NullReferenceException("EonaCatDns: " + nameof(chars));
        }

        if (charIndex < 0)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(charIndex));
        }

        if (charIndex > chars.Length)
        {
            throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(charIndex));
        }

        DecodeInternal(chars.AsSpan(charIndex, charCount), bytes.AsSpan(byteIndex, byteCount), flush, out charsUsed,
            out bytesUsed, out completed);
    }

    public override int GetBytes(ReadOnlySpan<char> chars, Span<byte> bytes, bool flush)
    {
        Convert(chars, bytes, flush, out _, out var bytesUsed, out _);
        return bytesUsed;
    }

    public void Convert(ReadOnlySpan<byte> chars, Span<byte> bytes, bool flush, out int charsUsed, out int bytesUsed,
        out bool completed)
    {
        DecodeInternal(chars, bytes, flush, out charsUsed, out bytesUsed, out completed);
    }

    public override void Convert(ReadOnlySpan<char> chars, Span<byte> bytes, bool flush, out int charsUsed,
        out int bytesUsed, out bool completed)
    {
        DecodeInternal(chars, bytes, flush, out charsUsed, out bytesUsed, out completed);
    }

    public override int GetByteCount(ReadOnlySpan<char> chars, bool flush)
    {
        if (chars.Length == 0 || chars == null)
        {
            return 0;
        }

        var alphabetInverse = Alphabet.AlphabetInverse;
        var bitsPerInputChar = Alphabet.EncodingBits;
        var count = chars.Length;
        foreach (var baseNChar in chars)
            if (baseNChar > 127 || alphabetInverse[baseNChar] == BaseAlphabet.NotInAlphabet)
            {
                count--;
            }

        var bytesCount = (int)checked((ulong)count * (ulong)bitsPerInputChar / 8);
        return bytesCount;
    }

    private void DecodeInternal<TInputT, TOutputT>(ReadOnlySpan<TInputT> input, Span<TOutputT> output, bool flush,
        out int inputUsed, out int outputUsed, out bool completed) where TInputT : unmanaged where TOutputT : unmanaged
    {
        inputUsed = outputUsed = 0;
        completed = true;

        if (input.IsEmpty || output.IsEmpty)
        {
            return;
        }

        var alphabetInverse = Alphabet.AlphabetInverse;
        var inputBlockSize = Alphabet.DecodingBlockSize;
        var encodingBits = Alphabet.EncodingBits;
        var inputOffset = 0;
        var inputCount = input.Length;
        var outputOffset = 0;
        var outputCount = output.Length;

        while (outputCount > 0)
        {
            // filling input & decoding
            var outputBlock = 0UL; // 1 byte for Base16, 5 bytes for Base32 and 3 bytes for Base64
            var originalInputUsed = inputUsed;
            int i;
            for (i = 0; i < inputBlockSize && inputCount > 0; i++)
            {
                uint baseNCode = 0;
                if (typeof(TInputT) == typeof(byte))
                {
                    baseNCode = (byte)(object)input[inputOffset + inputUsed++];
                }

                if (typeof(TInputT) == typeof(char))
                {
                    baseNCode = (char)(object)input[inputOffset + inputUsed++];
                }

                inputCount--;

                if (baseNCode > 127 || alphabetInverse[baseNCode] == BaseAlphabet.NotInAlphabet)
                {
                    i--;
                    continue;
                }

                outputBlock <<= encodingBits;
                outputBlock |= alphabetInverse[baseNCode];
            }

            var outputSize = i * encodingBits / 8;
            outputBlock >>= i * encodingBits % 8; // align

            // flushing output
            if (outputSize > outputCount ||
                outputSize == 0 ||
                (i != inputBlockSize && !flush))
            {
                inputUsed = originalInputUsed; // unwind inputUsed
                break;
            }

            if (typeof(TOutputT) == typeof(byte))
            {
                for (i = 0; i < outputSize; i++)
                {
                    output[outputOffset + outputUsed + (outputSize - 1 - i)] =
                        (TOutputT)(object)(byte)(outputBlock & 255);
                    outputBlock >>= 8;
                }
            }

            if (typeof(TOutputT) == typeof(char))
            {
                for (i = 0; i < outputSize; i++)
                {
                    output[outputOffset + outputUsed + (outputSize - 1 - i)] =
                        (TOutputT)(object)(char)(outputBlock & 255);
                    outputBlock >>= 8;
                }
            }

            outputUsed += outputSize;
            outputCount -= outputSize;
        }

        completed = inputCount == 0;
    }

    public override string ToString()
    {
        return $"Base{Alphabet.Alphabet.Length}Encoder, {new string(Alphabet.Alphabet)}";
    }
}