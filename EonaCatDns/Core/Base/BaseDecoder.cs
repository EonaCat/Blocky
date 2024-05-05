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

internal sealed partial class BaseDecoder : Decoder, ICryptoTransform
{
    private const int AlgorithmTypeBase16 = 0;
    private const int AlgorithmTypeBase32 = 1;
    private const int AlgorithmTypeBase64 = 2;
    private const int AlgorithmTypeOther = 3;

    private readonly int _algorithmType;

    public BaseDecoder(BaseAlphabet baseNAlphabet)
    {
        Alphabet = baseNAlphabet ?? throw new ArgumentNullException("EonaCatDns: " + nameof(baseNAlphabet));
        _algorithmType = Alphabet.Alphabet.Length switch
        {
            16 => AlgorithmTypeBase16,
            32 => AlgorithmTypeBase32,
            64 => AlgorithmTypeBase64,
            _ => AlgorithmTypeOther
        };
    }

    public BaseAlphabet Alphabet { get; }

    int ICryptoTransform.InputBlockSize => Alphabet.EncodingBlockSize;

    int ICryptoTransform.OutputBlockSize => Alphabet.DecodingBlockSize;

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
        var outputBuffer = new byte[GetCharCount(inputBuffer, inputOffset, inputCount, true)];
        Convert(inputBuffer, inputOffset, inputCount, outputBuffer, 0, outputBuffer.Length, true, out _, out _, out _);
        return outputBuffer;
    }

    void IDisposable.Dispose()
    {
        Reset();
    }

    public override unsafe int GetCharCount(byte* bytes, int count, bool flush)
    {
        return GetCharCount(count, flush);
    }

    public override int GetCharCount(byte[] bytes, int index, int count)
    {
        return GetCharCount(count, true);
    }

    public override int GetCharCount(byte[] bytes, int index, int count, bool flush)
    {
        return GetCharCount(count, flush);
    }

    public int GetMaxCharCount(int byteCount)
    {
        return (byteCount + Alphabet.EncodingBlockSize - 1) / Alphabet.EncodingBlockSize * Alphabet.DecodingBlockSize;
    }

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        Convert(bytes, byteIndex, byteCount, chars, charIndex, chars.Length - charIndex, true, out _, out var charsUsed,
            out _);
        return charsUsed;
    }

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex, bool flush)
    {
        Convert(bytes, byteIndex, byteCount, chars, charIndex, chars.Length - charIndex, flush, out _,
            out var charsUsed, out _);
        return charsUsed;
    }

    public override unsafe int GetChars(byte* bytes, int byteCount, char* chars, int charCount, bool flush)
    {
        Convert(bytes, byteCount, chars, charCount, flush, out _, out var charsUsed, out _);
        return charsUsed;
    }

    public void Convert(byte[] bytes, int byteIndex, int byteCount, byte[] chars, int charIndex, int charCount,
        bool flush, out int bytesUsed, out int charsUsed, out bool completed)
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

        switch (_algorithmType)
        {
            case AlgorithmTypeBase16:
                EncodeBase16(bytes.AsSpan(byteIndex, byteCount), chars.AsSpan(charIndex, charCount), flush,
                    out bytesUsed, out charsUsed, out completed);
                break;

            case AlgorithmTypeBase32:
                EncodeBase32(bytes.AsSpan(byteIndex, byteCount), chars.AsSpan(charIndex, charCount), flush,
                    out bytesUsed, out charsUsed, out completed);
                break;

            case AlgorithmTypeBase64:
                EncodeBase64(bytes.AsSpan(byteIndex, byteCount), chars.AsSpan(charIndex, charCount), flush,
                    out bytesUsed, out charsUsed, out completed);
                break;

            default:
                EncodeAny(new ReadOnlySpan<byte>(bytes, byteIndex, byteCount), chars.AsSpan(charIndex, charCount),
                    flush, out bytesUsed, out charsUsed, out completed);
                break;
        }
    }

    public override unsafe void Convert(byte* bytes, int byteCount, char* chars, int charCount, bool flush,
        out int bytesUsed, out int charsUsed, out bool completed)
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

        switch (_algorithmType)
        {
            case AlgorithmTypeBase16:
                EncodeBase16(new ReadOnlySpan<byte>(bytes, byteCount), new Span<char>(chars, charCount), flush,
                    out bytesUsed, out charsUsed, out completed);
                break;

            case AlgorithmTypeBase32:
                EncodeBase32(new ReadOnlySpan<byte>(bytes, byteCount), new Span<char>(chars, charCount), flush,
                    out bytesUsed, out charsUsed, out completed);
                break;

            case AlgorithmTypeBase64:
                EncodeBase64(new ReadOnlySpan<byte>(bytes, byteCount), new Span<char>(chars, charCount), flush,
                    out bytesUsed, out charsUsed, out completed);
                break;

            default:
                EncodeAny(new ReadOnlySpan<byte>(bytes, byteCount), new Span<char>(chars, charCount), flush,
                    out bytesUsed, out charsUsed, out completed);
                break;
        }
    }

    public override void Convert(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex, int charCount,
        bool flush, out int bytesUsed, out int charsUsed, out bool completed)
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

        switch (_algorithmType)
        {
            case AlgorithmTypeBase16:
                EncodeBase16(bytes.AsSpan(byteIndex, byteCount), chars.AsSpan(charIndex, charCount), flush,
                    out bytesUsed, out charsUsed, out completed);
                break;

            case AlgorithmTypeBase32:
                EncodeBase32(bytes.AsSpan(byteIndex, byteCount), chars.AsSpan(charIndex, charCount), flush,
                    out bytesUsed, out charsUsed, out completed);
                break;

            case AlgorithmTypeBase64:
                EncodeBase64(bytes.AsSpan(byteIndex, byteCount), chars.AsSpan(charIndex, charCount), flush,
                    out bytesUsed, out charsUsed, out completed);
                break;

            default:
                EncodeAny(new ReadOnlySpan<byte>(bytes, byteIndex, byteCount), chars.AsSpan(charIndex, charCount),
                    flush, out bytesUsed, out charsUsed, out completed);
                break;
        }
    }

    public override void Convert(ReadOnlySpan<byte> bytes, Span<char> chars, bool flush, out int bytesUsed,
        out int charsUsed, out bool completed)
    {
        switch (_algorithmType)
        {
            case AlgorithmTypeBase16:
                EncodeBase16(bytes, chars, flush, out bytesUsed, out charsUsed, out completed);
                break;

            case AlgorithmTypeBase32:
                EncodeBase32(bytes, chars, flush, out bytesUsed, out charsUsed, out completed);
                break;

            case AlgorithmTypeBase64:
                EncodeBase64(bytes, chars, flush, out bytesUsed, out charsUsed, out completed);
                break;

            default:
                EncodeAny(bytes, chars, flush, out bytesUsed, out charsUsed, out completed);
                break;
        }
    }

    public override int GetCharCount(ReadOnlySpan<byte> bytes, bool flush)
    {
        return GetCharCount(bytes.Length, flush);
    }

    public override int GetChars(ReadOnlySpan<byte> bytes, Span<char> chars, bool flush)
    {
        Convert(bytes, chars, flush, out _, out var charsUsed, out _);
        return charsUsed;
    }

    private void EncodeAny<TInputT, TOutputT>(ReadOnlySpan<TInputT> input, Span<TOutputT> output, bool flush,
        out int inputUsed, out int outputUsed, out bool completed) where TInputT : unmanaged where TOutputT : unmanaged
    {
        if (input.IsEmpty || output.IsEmpty)
        {
            inputUsed = outputUsed = 0;
            completed = true;
            return;
        }

        // #1: preparing
        var i = 0;
        var alphabetChars = Alphabet.Alphabet ?? throw new InvalidOperationException();
        var inputBlockSize = Alphabet.EncodingBlockSize;
        var outputBlockSize = Alphabet.DecodingBlockSize;
        var encodingMask = (ulong)alphabetChars.Length - 1;
        var encodingBits = Alphabet.EncodingBits;
        var inputOffset = 0;
        var inputCount = input.Length;
        var outputOffset = 0;
        var outputCount = output.Length;

        // #2: encoding whole blocks

        var wholeBlocksToProcess = Math.Min(inputCount / inputBlockSize, outputCount / outputBlockSize);
        var inputBlock = 0UL; // 1 byte for Base16, 5 bytes for Base32 and 3 bytes for Base64
        var outputBlock = 0UL; // 2 bytes for Base16, 8 bytes for Base32 and 4 bytes for Base64
        while (wholeBlocksToProcess-- > 0)
        {
            // filling input
            if (typeof(TInputT) == typeof(byte))
            {
                for (i = 0; i < inputBlockSize; i++)
                {
                    inputBlock <<= 8;
                    inputBlock |= (byte)(object)input[inputOffset++];
                }
            }

            if (typeof(TInputT) == typeof(char))
            {
                for (i = 0; i < inputBlockSize; i++)
                {
                    inputBlock <<= 8;
                    inputBlock |= (char)(object)input[inputOffset++];
                }
            }

            // encoding
            for (i = 0; i < outputBlockSize; i++)
            {
                outputBlock <<= 8;
                outputBlock |= alphabetChars[(int)(inputBlock & encodingMask)];
                inputBlock >>= encodingBits;
            }

            // flush output
            if (typeof(TOutputT) == typeof(byte))
            {
                for (i = 0; i < outputBlockSize; i++)
                {
                    output[outputOffset++] = (TOutputT)(object)(byte)(outputBlock & 255);
                    outputBlock >>= 8;
                }
            }

            if (typeof(TOutputT) == typeof(char))
            {
                for (i = 0; i < outputBlockSize; i++)
                {
                    output[outputOffset++] = (TOutputT)(object)(char)(outputBlock & 255);
                    outputBlock >>= 8;
                }
            }

            outputCount -= outputBlockSize;
            inputCount -= inputBlockSize;
        }

        // #3: encoding partial blocks
        outputBlock = 0;
        inputBlock = 0;
        var finalOutputBlockSize = (int)Math.Ceiling(Math.Min(inputCount, inputBlockSize) * 8.0 / encodingBits);

        // filling input for final block
        if (typeof(TInputT) == typeof(byte))
        {
            for (i = 0; i < inputBlockSize && i < inputCount; i++)
            {
                inputBlock <<= 8;
                inputBlock |= (byte)(object)input[inputOffset++];
            }
        }

        if (typeof(TInputT) == typeof(char))
        {
            for (i = 0; i < inputBlockSize; i++)
            {
                inputBlock <<= 8;
                inputBlock |= (char)(object)input[inputOffset++];
            }
        }

        // align with encodingBits
        inputBlock <<= encodingBits - Math.Min(inputBlockSize, inputCount) * 8 % encodingBits;

        // fill output with paddings
        for (i = 0; i < outputBlockSize; i++)
        {
            outputBlock <<= 8;
            outputBlock |= (byte)Alphabet.Padding;
        }

        // encode final block
        for (i = 0; i < finalOutputBlockSize; i++)
        {
            outputBlock <<= 8;
            outputBlock |= alphabetChars[(int)(inputBlock & encodingMask)];
            inputBlock >>= encodingBits;
        }

        if (Alphabet.HasPadding && inputCount > 0)
        {
            finalOutputBlockSize = outputBlockSize;
        }

        // flush final block
        if (finalOutputBlockSize > outputCount || !flush)
        {
            finalOutputBlockSize = 0; // cancel flushing output
            inputOffset -= Math.Min(inputBlockSize, inputCount); // rewind input
        }
        else
        {
            inputCount -= Math.Min(inputBlockSize, inputCount);
        }

        if (typeof(TOutputT) == typeof(byte))
        {
            for (i = 0; i < finalOutputBlockSize; i++)
            {
                output[outputOffset++] = (TOutputT)(object)(byte)(outputBlock & 255);
                outputBlock >>= 8;
            }
        }

        if (typeof(TOutputT) == typeof(char))
        {
            for (i = 0; i < finalOutputBlockSize; i++)
            {
                output[outputOffset++] = (TOutputT)(object)(char)(outputBlock & 255);
                outputBlock >>= 8;
            }
        }

        inputUsed = inputOffset;
        outputUsed = outputOffset;
        completed = inputCount == 0; // true if all input is used
    }

    private int GetCharCount(int count, bool flush)
    {
        if (count == 0)
        {
            return 0;
        }

        var wholeBlocksSize = checked(count / Alphabet.EncodingBlockSize * Alphabet.DecodingBlockSize);
        var finalBlockSize = (int)Math.Ceiling(count % Alphabet.EncodingBlockSize * 8.0 / Alphabet.EncodingBits);
        if (Alphabet.HasPadding && finalBlockSize != 0)
        {
            finalBlockSize = Alphabet.DecodingBlockSize;
        }

        if (!flush)
        {
            finalBlockSize = 0;
        }

        return checked(wholeBlocksSize + finalBlockSize);
    }

    public override string ToString()
    {
        return $"Base{Alphabet.Alphabet.Length}Decoder, {new string(Alphabet.Alphabet)}";
    }
}