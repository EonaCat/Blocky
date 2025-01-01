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

using System.IO;

namespace EonaCat.Dns.Core;

public partial class DnsHeader
{
    public const int HeaderSize = 12;
    public byte Opcode4;

    public ushort Id { get; set; }

    public MessageType MessageType { get; set; }

    public bool IsQuery => MessageType == MessageType.Query;

    public bool IsResponse => !IsQuery;

    public AuthoritativeAnswer AuthoritativeAnswer { get; set; }

    public bool IsTruncated { get; set; }

    public bool IsRecursionDesired { get; set; }

    public bool IsRecursionAvailable { get; set; }

    public int Zero { get; set; }

    public bool IsAuthenticatedData { get; set; }

    public bool IsCheckingDisabled { get; set; }

    public ResponseCode ResponseCode { get; set; }
    public ushort QuestionCount { get; set; }
    public ushort AnswerCount { get; set; }
    public ushort AuthorityCount { get; set; }
    public ushort AdditionalRecordsCount { get; set; }

    public override string ToString()
    {
        var stringWriter = new StringWriter();

        stringWriter.Write($"{nameof(ResponseCode)}: ");
        stringWriter.Write(ResponseCode);
        stringWriter.WriteLine();

        stringWriter.Write($"{nameof(MessageType)}: ");
        stringWriter.Write(MessageType);
        stringWriter.WriteLine();

        stringWriter.Write($"{nameof(AuthoritativeAnswer)}: ");
        stringWriter.Write(AuthoritativeAnswer);
        stringWriter.WriteLine();

        stringWriter.Write($"{nameof(IsTruncated)}: ");
        stringWriter.Write(IsTruncated);
        stringWriter.WriteLine();

        stringWriter.Write($"{nameof(IsRecursionDesired)}: ");
        stringWriter.Write(IsRecursionDesired);
        stringWriter.WriteLine();

        stringWriter.Write($"{nameof(IsAuthenticatedData)}: ");
        stringWriter.Write(IsAuthenticatedData);
        stringWriter.WriteLine();

        stringWriter.Write($"{nameof(IsCheckingDisabled)}: ");
        stringWriter.Write(IsCheckingDisabled);
        stringWriter.WriteLine();

        stringWriter.Write($"{nameof(QuestionCount)}: ");
        stringWriter.Write(QuestionCount);
        stringWriter.WriteLine();

        stringWriter.Write($"{nameof(AnswerCount)}: ");
        stringWriter.Write(AnswerCount);
        stringWriter.WriteLine();

        stringWriter.Write($"{nameof(AuthorityCount)}: ");
        stringWriter.Write(AuthorityCount);
        stringWriter.WriteLine();

        stringWriter.Write($"{nameof(AdditionalRecordsCount)}: ");
        stringWriter.Write(AdditionalRecordsCount);
        stringWriter.WriteLine();

        return stringWriter.ToString();
    }
}