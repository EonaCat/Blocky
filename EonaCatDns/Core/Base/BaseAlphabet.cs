﻿/*
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

namespace EonaCat.Dns.Core.Base;

public sealed class BaseAlphabet
{
    internal const byte NotInAlphabet = 255;
    public static readonly BaseAlphabet Base16UpperCaseAlphabet = new("0123456789ABCDEF".ToCharArray());

    public static readonly BaseAlphabet Base16LowerCaseAlphabet = new("0123456789abcdef".ToCharArray());

    public static readonly BaseAlphabet Base32Alphabet = new("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray(), '=');
    internal readonly char[] Alphabet;
    internal readonly byte[] AlphabetInverse;
    public readonly int DecodingBlockSize;
    public readonly int EncodingBits;
    public readonly int EncodingBlockSize;
    internal readonly char Padding;

    public BaseAlphabet(char[] alphabet, char padding = '\u00ff')
    {
        if (alphabet == null)
        {
            throw new ArgumentNullException("EonaCatDns: " + nameof(alphabet));
        }

        switch (alphabet.Length)
        {
            case 32:
                EncodingBlockSize = 5;
                DecodingBlockSize = 8;
                EncodingBits = 5;
                break;

            case 16:
                EncodingBits = 4;
                EncodingBlockSize = 1;
                DecodingBlockSize = 2;
                break;

            default:
                throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(alphabet));
        }

        Alphabet = alphabet;
        Padding = padding;
        AlphabetInverse = new byte[127];
        for (var i = 0; i < AlphabetInverse.Length; i++)
        {
            AlphabetInverse[i] = NotInAlphabet;
        }

        for (var i = 0; i < Alphabet.Length; i++)
        {
            var charNum = (int)alphabet[i];
            if (charNum < 0 || charNum > 127 || charNum == padding)
            {
                throw new ArgumentOutOfRangeException("EonaCatDns: " + nameof(alphabet));
            }

            AlphabetInverse[charNum] = (byte)i;
        }
    }

    internal bool HasPadding => Padding != '\u00ff';

    public override string ToString()
    {
        return $"Base{Alphabet.Length}, Padding: '{Padding}'({(HasPadding ? "y" : "n")})";
    }
}