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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EonaCat.Dns.Core.Base;
using EonaCat.Dns.Core.Helpers;
using EonaCat.Dns.Core.Records;
using EonaCat.Helpers;
using EonaCat.Logger;

namespace EonaCat.Dns.Core;

public class MasterReader
{
    private readonly TextReader _text;
    private DomainName _defaultDomainName;
    private TimeSpan? _defaultTtl;
    private bool _isEol;
    private int _parenthesesLevel;
    private int _previousCharacter = '\n';

    private bool _tokenStartsNewLine;

    internal MasterReader(TextReader text)
    {
        _text = text;
    }

    public DomainName Origin { get; set; } = DomainName.Root;

    public byte ReadByte()
    {
        return byte.Parse(ReadToken(), CultureInfo.InvariantCulture);
    }

    public ushort ReadUInt16()
    {
        return ushort.Parse(ReadToken(), CultureInfo.InvariantCulture);
    }

    public uint ReadUInt32()
    {
        return uint.Parse(ReadToken(), CultureInfo.InvariantCulture);
    }

    public DomainName ReadDomainName()
    {
        return MakeAbsoluteDomainName(ReadToken(true));
    }

    private DomainName MakeAbsoluteDomainName(string name)
    {
        // If an absolute name.
        if (name.EndsWith("."))
        {
            return new DomainName(name.Substring(0, name.Length - 1));
        }

        // Then its a relative name.
        return DomainName.Join(new DomainName(name), Origin);
    }

    public string ReadString()
    {
        return ReadToken();
    }

    public byte[] ReadBase64String()
    {
        // Handle embedded space and CRLFs inside of parens.
        var sb = new StringBuilder();
        while (!IsEndOfLine()) sb.Append(ReadToken());

        return Convert.FromBase64String(sb.ToString());
    }

    public TimeSpan ReadTimeSpan16()
    {
        return TimeSpan.FromSeconds(ReadUInt16());
    }

    public TimeSpan ReadTimeSpan32()
    {
        return TimeSpan.FromSeconds(ReadUInt32());
    }

    public IPAddress ReadIpAddress(int length = 4)
    {
        return IPAddress.Parse(ReadToken());
    }

    public RecordType ReadDnsType()
    {
        var token = ReadToken();
        if (token.StartsWith("TYPE_"))
        {
            return (RecordType)ushort.Parse(token.Substring(5), CultureInfo.InvariantCulture);
        }

        return (RecordType)Enum.Parse(typeof(RecordType), token);
    }

    public DateTime ReadDateTime()
    {
        var token = ReadToken();
        if (token.Length == 14)
        {
            return DateTime.ParseExact(
                token,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        return DateTimeHelper.Epoch.AddSeconds(ulong.Parse(token, CultureInfo.InvariantCulture));
    }

    public byte[] ReadResourceData()
    {
        var leading = ReadToken();
        if (leading != "#")
        {
            throw new FormatException("EonaCatDns: " + $"Expected RDATA leading '\\#', not '{leading}'.");
        }

        var length = ReadUInt32();
        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        // Get the hex string.
        var stringBuilder = new StringBuilder();
        while (stringBuilder.Length < length * 2)
        {
            var word = ReadToken();
            if (word.Length == 0)
            {
                break;
            }

            if (word.Length % 2 != 0)
            {
                throw new FormatException("EonaCatDns: " +
                                          $"The hex word ('{word}') must have an even number of digits.");
            }

            stringBuilder.Append(word);
        }

        if (stringBuilder.Length != length * 2)
        {
            throw new FormatException("EonaCatDns: " + "Wrong number of RDATA hex digits.");
        }

        // Convert hex string into byte array.
        try
        {
            return Base16Converter.ToBytes(stringBuilder.ToString());
        }
        catch (InvalidOperationException e)
        {
            throw new FormatException("EonaCatDns: " + e.Message);
        }
    }

    internal async Task<ResourceRecord> ReadResourceRecord(RootHintEntry rootHintEntry = null)
    {
        var domainName = _defaultDomainName;
        var recordClass = RecordClass.Internet;
        var ttl = _defaultTtl;
        RecordType? type = null;

        if (rootHintEntry != null)
        {
            domainName = rootHintEntry.Name;
            type = Enum.TryParse(rootHintEntry.Type, out RecordType t) ? t : RecordType.Unknown;
            recordClass = RecordClass.Internet;
            ttl = TimeSpan.FromMilliseconds(rootHintEntry.Ttl);
            return ReadResourceRecord(domainName, type, recordClass, ttl, rootHintEntry);
        }

        while (!type.HasValue)
        {
            var token = ReadToken(true);
            if (token == "")
            {
                return null;
            }

            if (_tokenStartsNewLine)
            {
                switch (token)
                {
                    case "$ORIGIN":
                        Origin = ReadDomainName();
                        break;

                    case "$TTL":
                        _defaultTtl = ttl = ReadTimeSpan32();
                        break;

                    case "@":
                        domainName = Origin;
                        _defaultDomainName = domainName;
                        break;

                    default:
                        domainName = MakeAbsoluteDomainName(token);
                        _defaultDomainName = domainName;
                        break;
                }

                continue;
            }

            if (token.All(char.IsDigit))
            {
                ttl = TimeSpan.FromSeconds(uint.Parse(token));
            }
            else if (token.StartsWith("IN"))
            {
                // Ignore
            }
            else if (token.StartsWith("UNKNOWN_CLASS_"))
            {
                await Logger.LogAsync($"Could not load masterFile record with unknown class: {token}", ELogType.WARNING)
                    .ConfigureAwait(false);
            }
            else
            {
                if (!Enum.TryParse(token, true, out RecordType t) || t == RecordType.Any)
                {
                    throw new InvalidDataException("EonaCatDns: " +
                                                   $"Unknown token '{token}', expected a Class, Type or TTL.");
                }

                type = t;
            }
        }

        if (domainName == null)
        {
            throw new InvalidDataException("EonaCatDns: " + "Missing resource record name.");
        }

        return ReadResourceRecord(domainName, type.Value, recordClass, ttl);
    }

    private ResourceRecord ReadResourceRecord(DomainName domainName, RecordType? type, RecordClass klass, TimeSpan? ttl,
        RootHintEntry rootHintEntry = null)
    {
        // Create the specific resource record based on the type.
        if (type == null)
        {
            return null;
        }

        var resource = RecordStore.Create(type.Value);
        resource.Name = domainName;
        resource.Type = type.Value;
        resource.Class = klass;
        if (resource is NsRecord nsRecord && rootHintEntry != null)
        {
            nsRecord.Authority = rootHintEntry.Value;
        }

        if (ttl.HasValue)
        {
            resource.Ttl = ttl.Value;
        }

        if (rootHintEntry == null)
            // Read the specific properties of the resource record.
        {
            resource.ReadData(this);
        }

        return resource;
    }

    public bool IsEndOfLine()
    {
        int character;
        while (_parenthesesLevel > 0)
        while ((character = _text.Peek()) >= 0)
        {
            if (character == ' ' || character == '\t' || character == '\r' || character == '\n')
            {
                character = _text.Read();
                _previousCharacter = character;
                continue;
            }

            if (character != ')')
            {
                return false;
            }

            _parenthesesLevel--;
            character = _text.Read();
            _previousCharacter = character;
            break;
        }

        if (_isEol)
        {
            return true;
        }

        while ((character = _text.Peek()) >= 0)
        {
            // Skip space or tab.
            if (character != ' ' && character != '\t')
            {
                return character == '\r' || character == '\n' || character == ';';
            }

            character = _text.Read();
            _previousCharacter = character;
        }

        // EOF is end of line
        return true;
    }

    private string ReadToken(bool ignoreEscape = false)
    {
        var stringBuilder = new StringBuilder();
        int character;
        var skipWhitespace = true;
        var inQuote = false;
        var inComment = false;
        _isEol = false;

        while ((character = _text.Read()) >= 0)
        {
            // Comments are terminated by a newline.
            if (inComment)
            {
                if (character == '\r' || character == '\n')
                {
                    inComment = false;
                    skipWhitespace = true;
                }

                _previousCharacter = character;
                continue;
            }

            // Handle escaped character.
            if (character == '\\')
            {
                if (ignoreEscape)
                {
                    if (stringBuilder.Length == 0)
                    {
                        _tokenStartsNewLine = _previousCharacter == '\r' || _previousCharacter == '\n';
                    }

                    stringBuilder.Append((char)character);
                    _previousCharacter = character;

                    character = _text.Read();
                    if (0 <= character)
                    {
                        stringBuilder.Append((char)character);
                        _previousCharacter = character;
                    }

                    continue;
                }

                _previousCharacter = character;

                // Handle decimal escapes
                var digitsTotal = 0;
                var decimalEscaped = 0;
                for (; digitsTotal <= 3; digitsTotal++)
                {
                    character = _text.Peek();
                    if (character is >= '0' and <= '9')
                    {
                        _text.Read();
                        decimalEscaped = decimalEscaped * 10 + (character - '0');
                        if (decimalEscaped > 255)
                        {
                            throw new FormatException("EonaCatDns: " + "Invalid value.");
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                character = digitsTotal > 0 ? decimalEscaped : _text.Read();

                stringBuilder.Append((char)character);
                skipWhitespace = false;
                _previousCharacter = (char)character;
                continue;
            }

            // Handle quoted strings.
            if (inQuote)
            {
                if (character == '"')
                {
                    break;
                }

                stringBuilder.Append((char)character);
                _previousCharacter = character;
                continue;
            }

            switch (character)
            {
                case '"':
                    inQuote = true;
                    _previousCharacter = character;
                    continue;
                // Ignore parentheses.
                case '(':
                    _parenthesesLevel++;
                    character = ' ';
                    break;
            }

            if (character == ')')
            {
                --_parenthesesLevel;
                character = ' ';
            }

            // Skip leading whitespace.
            if (skipWhitespace)
            {
                if (char.IsWhiteSpace((char)character))
                {
                    _previousCharacter = character;
                    continue;
                }

                skipWhitespace = false;
            }

            // Trailing whitespace, ends the token.
            if (char.IsWhiteSpace((char)character))
            {
                _previousCharacter = character;
                _isEol = character == '\r' || character == '\n';
                break;
            }

            // Handle start of comment.
            if (character == ';' || character == '#')
            {
                inComment = true;
                _previousCharacter = character;
                continue;
            }

            // Default handling, use the character as part of the token.
            if (stringBuilder.Length == 0)
            {
                _tokenStartsNewLine = _previousCharacter == '\r' || _previousCharacter == '\n';
            }

            stringBuilder.Append((char)character);
            _previousCharacter = character;
        }

        return stringBuilder.ToString();
    }
}