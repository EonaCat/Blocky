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
using System.Globalization;
using System.Linq;
using System.Text;

namespace EonaCat.Dns.Core;

public class DomainName : IEquatable<DomainName>
{
    private const string Dot = ".";
    private const char DotChar = '.';
    private const string DotEscaped = @"\.";
    private const char BackslashChar = '\\';
    private const string BackslashEscaped = @"\092";

    public static DomainName Root = new(string.Empty);

    private readonly List<string> _labels = new();

    public DomainName(string name)
    {
        Parse(name);
    }

    public DomainName(params string[] labels)
    {
        _labels.AddRange(labels);
    }

    public IReadOnlyList<string> Labels => _labels;

    public bool Equals(DomainName that)
    {
        if (that is null)
        {
            return false;
        }

        var labelsCount = _labels.Count;
        if (labelsCount != that._labels.Count)
        {
            return false;
        }

        for (var i = 0; i < labelsCount; i++)
            if (!LabelsEqual(_labels[i], that._labels[i]))
            {
                return false;
            }

        return true;
    }

    public static DomainName Join(params DomainName[] names)
    {
        var joinedName = new DomainName();
        foreach (var name in names) joinedName._labels.AddRange(name.Labels);

        return joinedName;
    }

    public override string ToString()
    {
        return string.Join(Dot, Labels.Select(EscapeLabel));
    }

    private static string EscapeLabel(string label)
    {
        var stringBuilder = new StringBuilder();
        foreach (var character in label)
            switch (character)
            {
                case BackslashChar:
                    stringBuilder.Append(BackslashEscaped);
                    break;

                case DotChar:
                    stringBuilder.Append(DotEscaped);
                    break;

                default:
                {
                    if (character <= 32 || character > 126)
                    {
                        stringBuilder.Append(BackslashChar);
                        stringBuilder.Append(((int)character).ToString("000", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        stringBuilder.Append(character);
                    }

                    break;
                }
            }

        return stringBuilder.ToString();
    }

    public DomainName ToCanonical()
    {
        var labels = Labels
            .Select(label => label.ToLowerInvariant())
            .ToArray();
        return new DomainName(labels);
    }

    public bool BelongsTo(DomainName domain)
    {
        return this == domain || IsSubDomainOf(domain);
    }

    public bool IsSubDomainOf(DomainName domain)
    {
        if (domain == null)
        {
            return false;
        }

        if (_labels.Count <= domain._labels.Count)
        {
            return false;
        }

        var labelsCount = _labels.Count - 1;
        var domainLabelCount = domain._labels.Count - 1;
        for (; 0 <= domainLabelCount; labelsCount--, domainLabelCount--)
            if (!LabelsEqual(_labels[labelsCount], domain._labels[domainLabelCount]))
            {
                return false;
            }

        return true;
    }

    public DomainName Parent()
    {
        return _labels.Count == 0 ? null : new DomainName(_labels.Skip(1).ToArray());
    }

    private void Parse(string name)
    {
        _labels.Clear();
        var label = new StringBuilder();
        var nameLength = name.Length;

        for (var i = 0; i < nameLength; i++)
        {
            var character = name[i];

            switch (character)
            {
                case BackslashChar:
                {
                    character = name[i++];
                    if (!char.IsDigit(character))
                    {
                        label.Append(character);
                    }
                    else
                    {
                        var number = character - '0';
                        number = number * 10 + (name[i++] - '0');
                        number = number * 10 + (name[i++] - '0');
                        label.Append((char)number);
                    }

                    continue;
                }
                case DotChar:
                    _labels.Add(label.ToString());
                    label.Clear();
                    continue;
                default:
                    // Just part of the label.
                    label.Append(character);
                    break;
            }
        }

        if (label.Length > 0)
        {
            _labels.Add(label.ToString());
        }
    }

    public override int GetHashCode()
    {
        return ToString().ToLowerInvariant().GetHashCode();
    }

    public override bool Equals(object obj)
    {
        var that = obj as DomainName;
        return that != null && Equals(that);
    }

    public static bool operator ==(DomainName a, DomainName b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null)
        {
            return false;
        }

        return b is not null && a.Equals(b);
    }

    public static bool operator !=(DomainName a, DomainName b)
    {
        return !(a == b);
    }

    public static implicit operator DomainName(string s)
    {
        return new DomainName(s);
    }

    public static bool LabelsEqual(string a, string b)
    {
        return 0 == StringComparer.InvariantCultureIgnoreCase.Compare(a, b);
    }
}