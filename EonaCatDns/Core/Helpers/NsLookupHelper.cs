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
using System.Threading.Tasks;

namespace EonaCat.Dns.Core.Helpers;

internal class NsLookupHelper
{
    internal static async Task ResolveAsync(string[] args, bool resolveOverDoh, bool appExitAfterCommand,
        bool onlyExitAfterKeyPress)
    {
        if (args.Length > 0)
        {
            if (args[0].ToLower().StartsWith("--ns"))
            {
                try
                {
                    var question = args[1];
                    string recordType = null;

                    if (args.Length > 2)
                    {
                        recordType = args[2];
                    }

                    var message = new Message();
                    if (!Enum.TryParse(recordType, true, out RecordType currentRecordType))
                    {
                        currentRecordType = RecordType.Any;
                    }

                    var currentQuestion = new Question
                    {
                        Class = RecordClass.Internet,
                        TimeCreated = DateTime.Now,
                        Name = question,
                        Type = currentRecordType
                    };
                    message.Questions.Add(currentQuestion);

                    var response = await ResolveAsync(message, resolveOverDoh).ConfigureAwait(false);
                    var stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(response.ToString());
                    Console.WriteLine(stringBuilder.ToString());
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.Message);
                }

                ToolHelper.ExitAfterTool(appExitAfterCommand, onlyExitAfterKeyPress);
            }
        }
    }

    private static async Task<Message> ResolveAsync(Message message, bool resolveOverDoh = false)
    {
        if (resolveOverDoh)
        {
            return await ResolveHelper.ResolveOverDohAsync(message, true).ConfigureAwait(false);
        }

        return await ResolveHelper.ResolveOverDnsAsync(message, true).ConfigureAwait(false);
    }
}