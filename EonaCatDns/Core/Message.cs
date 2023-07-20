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

using EonaCat.Dns.Core.Helpers;
using EonaCat.Dns.Core.Records;
using EonaCat.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EonaCat.Dns.Core
{
    public class Message : RecordBase
    {
        internal DnsHeader.Bytes Bytes { get; } = new DnsHeader.Bytes();
        public DnsHeader Header { get; set; } = new DnsHeader();
        public List<Question> Questions { get; } = new List<Question>();
        public List<ResourceRecord> Answers { get; set; } = new List<ResourceRecord>();
        public List<ResourceRecord> AuthorityRecords { get; set; } = new List<ResourceRecord>();
        public List<ResourceRecord> AdditionalRecords { get; set; } = new List<ResourceRecord>();
        public bool IsFromCache { get; set; }
        public bool IsBlocked { get; set; }
        public bool IsReceivedViaMultiCast { get; set; }

        public OperationCode OperationCode
        {
            get
            {
                var optRecord = AdditionalRecords.OfType<OptRecord>().FirstOrDefault();
                return (OperationCode)((optRecord?.Opcode8 << 4) | Header.Opcode4 ?? Header.Opcode4);
            }
            set
            {
                var optRecord = AdditionalRecords.OfType<OptRecord>().FirstOrDefault();

                // Is standard opCode?
                var extendedOpCode = (int)value;
                if (extendedOpCode < 4080)
                {
                    Header.Opcode4 = (byte)extendedOpCode;
                    if (optRecord != null) optRecord.Opcode8 = 0;

                    return;
                }

                // Extended opCode, needs an OPT resource record.
                if (optRecord == null)
                {
                    optRecord = new OptRecord();
                    AdditionalRecords.Add(optRecord);
                }

                Header.Opcode4 = (byte)(extendedOpCode & Bytes.MessageTypeByte);
                optRecord.Opcode8 = (byte)((extendedOpCode >> 4) & 255);
            }
        }

        public bool IsDnsSecSupported
        {
            get
            {
                var optRecord = AdditionalRecords.OfType<OptRecord>().FirstOrDefault();
                return optRecord?.Do ?? false;
            }
            set
            {
                var optRecord = AdditionalRecords.OfType<OptRecord>().FirstOrDefault();
                if (optRecord == null)
                {
                    optRecord = new OptRecord();
                    AdditionalRecords.Add(optRecord);
                }

                optRecord.Do = value;
            }
        }

        public bool HasAuthorityRecord => AuthorityRecords.Count > 0;
        public bool HasAdditionalRecords => AdditionalRecords.Count > 0;
        public bool HasAnswerRecords => Answers.Count > 0;
        public bool HasAnswers => HasAnswerRecords || HasAdditionalRecords || HasAuthorityRecord;
        public TimeSpan ResponseTime { get; set; }
        public string ServerUrl { get; set; }
        public ResolveType ResolveType { get; set; }

        public Message CreateResponse()
        {
            var response = new Message
            {
                Header = new DnsHeader
                {
                    Id = Header.Id,
                    MessageType = MessageType.Response
                }
            };
            response.OperationCode = OperationCode;
            response.Questions.AddRange(Questions);
            return response;
        }

        public void Truncate(int length)
        {
            while (Length() > length)
            {
                if (AdditionalRecords.Count > 0)
                {
                    AdditionalRecords.RemoveAt(AdditionalRecords.Count - 1);
                }
                else if (AuthorityRecords.Count > 0)
                {
                    AuthorityRecords.RemoveAt(AuthorityRecords.Count - 1);
                }
                else
                {
                    Header.IsTruncated = true;
                    return;
                }
            }
        }

        public Message UseDnsSecurity()
        {
            IsDnsSecSupported = true;
            return this;
        }

        public override IDns Read(DnsReader reader)
        {
            RetrieveHeader(reader);

            if (Header.QuestionCount == 256)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Corrupted package received:");

                if (IsReceivedViaMultiCast) stringBuilder.AppendLine("Message was received via multiCast");

                stringBuilder.AppendLine(string.Empty);

                stringBuilder.AppendLine("=======");
                stringBuilder.AppendLine("HEADER:");
                stringBuilder.AppendLine("=======");
                stringBuilder.AppendLine(Header.ToString());
                HasPacketError = true;

                if (reader.HasOriginalBytes)
                    MessageHelper.PrintPacketDetails(reader.OriginalBytes.Length, reader.OriginalBytes, "Packet details", true, true);

                Logger.Log(stringBuilder.ToString(), ELogType.ERROR, false);
                return this;
            }

            Questions.Clear();
            for (var i = 0; i < Header.QuestionCount; i++) Questions.Add((Question)new Question().Read(reader));

            Answers.Clear();
            for (var i = 0; i < Header.AnswerCount; i++) Answers.Add((ResourceRecord)new ResourceRecord().Read(reader));

            AuthorityRecords.Clear();
            for (var i = 0; i < Header.AuthorityCount; i++) AuthorityRecords.Add((ResourceRecord)new ResourceRecord().Read(reader));

            AdditionalRecords.Clear();
            for (var i = 0; i < Header.AdditionalRecordsCount; i++) AdditionalRecords.Add((ResourceRecord)new ResourceRecord().Read(reader));

            return this;
        }

        private void RetrieveHeader(DnsReader reader)
        {
            var header = new DnsHeader();

            try
            {
                header.Id = reader.ReadUInt16();
                var flags = reader.ReadUInt16();
                header.QuestionCount = reader.ReadUInt16();
                header.AnswerCount = reader.ReadUInt16();
                header.AuthorityCount = reader.ReadUInt16();
                header.AdditionalRecordsCount = reader.ReadUInt16();

                header.MessageType = (MessageType)(flags >> Bytes.MessageTypeByte);
                header.AuthoritativeAnswer = (AuthoritativeAnswer)((flags >> Bytes.AuthoritativeAnswerByte) & 1);
                header.IsTruncated = ((flags >> Bytes.IsTruncatedByte) & 1) == 1;
                header.IsRecursionDesired = ((flags >> Bytes.IsRecursionDesiredByte) & 1) == 1;
                header.IsRecursionAvailable = ((flags >> Bytes.IsRecursionAvailableByte) & 1) == 1;
                header.Opcode4 = (byte)((flags >> Bytes.OpCodeByte) & Bytes.MessageTypeByte);
                header.Zero = (flags >> Bytes.ZeroByte) & 1;
                header.IsAuthenticatedData = ((flags >> Bytes.IsAuthenticatedDataByte) & 1) == 1;
                header.IsCheckingDisabled = ((flags >> Bytes.IsCheckingDisabledByte) & 1) == 1;
                header.ResponseCode = (ResponseCode)(flags & Bytes.MessageTypeByte);
            }
            catch (Exception exception)
            {
                if (reader.HasOriginalBytes)
                    MessageHelper.PrintPacketDetails(reader.OriginalBytes.Length, reader.OriginalBytes, "Could not retrieve dns header for message", true, true);
                Logger.Log(exception, "Could not retrieve dns header for message", false);
            }
            finally
            {
                Header = header;
            }
        }

        public override void Write(DnsWriter writer)
        {
            writer.WriteUInt16(Header.Id);
            ushort flags = 0;
            flags |= (ushort)((ushort)Header.MessageType << Bytes.MessageTypeByte);
            flags |= (ushort)((ushort)Header.AuthoritativeAnswer << Bytes.AuthoritativeAnswerByte);
            flags |= (ushort)(Convert.ToUInt16(Header.IsTruncated) << Bytes.IsTruncatedByte);
            flags |= (ushort)(Convert.ToUInt16(Header.IsRecursionDesired) << Bytes.IsRecursionDesiredByte);
            flags |= (ushort)(Convert.ToUInt16(Header.IsRecursionAvailable) << Bytes.IsRecursionAvailableByte);
            flags |= (ushort)((Header.Opcode4 & Bytes.MessageTypeByte) << Bytes.OpCodeByte);
            flags |= (ushort)(((ushort)Header.Zero & 1) << Bytes.ZeroByte);
            flags |= (ushort)(Convert.ToUInt16(Header.IsAuthenticatedData) << Bytes.IsAuthenticatedDataByte);
            flags |= (ushort)(Convert.ToUInt16(Header.IsCheckingDisabled) << Bytes.IsCheckingDisabledByte);
            flags |= (ushort)(Convert.ToUInt16(Header.ResponseCode) & Bytes.MessageTypeByte);
            writer.WriteUInt16(flags);
            writer.WriteUInt16((ushort)Questions.Count);
            writer.WriteUInt16((ushort)Answers.Count);
            writer.WriteUInt16((ushort)AuthorityRecords.Count);
            writer.WriteUInt16((ushort)AdditionalRecords.Count);
            Questions.ForEach(q => q.Write(writer));
            Answers.ForEach(a => a.Write(writer));
            AuthorityRecords.ForEach(ar => ar.Write(writer));
            AdditionalRecords.ForEach(ad => ad.Write(writer));
        }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            ToString(stringBuilder, Header);
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("======");
            stringBuilder.AppendLine("Question");
            stringBuilder.AppendLine("======");
            if (Questions.Count == 0)
            {
                stringBuilder.AppendLine("NO QUESTIONS FOUND");
            }
            else
            {
                Questions.ForEach(q => stringBuilder.AppendLine(q.ToString()));
            }

            ToString(stringBuilder, "Answer", Answers.OrderBy(x => x.Type).ToList());
            ToString(stringBuilder, "Authority", AuthorityRecords.OrderBy(x => x.Type).ToList());
            ToString(stringBuilder, "Additional", AdditionalRecords.OrderBy(x => x.Type).ToList());

            return stringBuilder.ToString();
        }

        private void ToString(StringBuilder stringBuilder, DnsHeader header)
        {
            stringBuilder.AppendLine("======");
            stringBuilder.AppendLine("Header");
            stringBuilder.AppendLine("======");

            if (header == null)
            {
                stringBuilder.AppendLine("INVALID MESSAGE - NO HEADER FOUND");
                return;
            }

            stringBuilder.AppendLine(header.ToString());
        }

        private static void ToString(StringBuilder stringBuilder, string title, List<ResourceRecord> records)
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("======");
            stringBuilder.AppendLine(title);
            stringBuilder.AppendLine("======");

            if (records.Count == 0)
            {
                stringBuilder.AppendLine("No records");
            }
            else
            {
                records.ForEach(r => stringBuilder.AppendLine(r.ToString()));
            }

            stringBuilder.AppendLine("======");
        }
    }
}
