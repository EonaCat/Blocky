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

using System.Net;
using System.Text;
using System.Threading.Tasks;
using EonaCat.Logger;

namespace EonaCat.Dns.Core.Helpers;

public static class MessageHelper
{
    public static bool ShowPacketDetails => false;
    private static bool OnlyTestDeviceIsAllowed => false;

    public static async Task PrintPacketDetails(int messageLength, byte[] bytes, string message = null,
        bool showAsString = false, bool forcePacketDetails = false, bool writeToConsole = false)
    {
        if (!ShowPacketDetails && !forcePacketDetails)
        {
            return;
        }

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"DETAILS FOR: {message}");
        stringBuilder.AppendLine("===========");

        for (var i = 0; i < messageLength; i++)
        {
            stringBuilder.Append($"{bytes[i]:X2} ");
            if (i % 16 == 15)
            {
                stringBuilder.AppendLine();
            }
        }

        if (showAsString)
        {
            stringBuilder.AppendLine("Representation as string: ");
            stringBuilder.AppendLine(Encoding.Default.GetString(bytes));
        }

        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"TOTAL BYTES: {messageLength}");
        stringBuilder.AppendLine("===========");
        await Logger.LogAsync(stringBuilder.ToString(), ELogType.DEBUG, writeToConsole).ConfigureAwait(false);
    }

    public static bool TestDeviceOnly(IPAddress ipAddress)
    {
        return OnlyTestDeviceIsAllowed && ipAddress.ToString() != "100.100.100.168";
    }

    public static async Task<Message> GetMessageFromBytes(byte[] bytes)
    {
        var message = new Message();
        await message.Read(bytes, 0, bytes.Length).ConfigureAwait(false);
        return message;
    }
}