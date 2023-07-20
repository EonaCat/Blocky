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
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace EonaCat.Dns.Extensions;

public static class IpAddressExtension
{
    public static IPAddress Parse(BinaryReader binaryReader)
    {
        switch (binaryReader.ReadByte())
        {
            case 1:
                return new IPAddress(binaryReader.ReadBytes(4));

            case 2:
                return new IPAddress(binaryReader.ReadBytes(16));

            default:
                throw new NotSupportedException("EonaCatDns: " + "EonaCatDns: AddressFamily not supported.");
        }
    }

    public static void WriteTo(this IPAddress address, BinaryWriter binaryWriter)
    {
        switch (address.AddressFamily)
        {
            case AddressFamily.InterNetwork:
                binaryWriter.Write((byte)1);
                break;

            case AddressFamily.InterNetworkV6:
                binaryWriter.Write((byte)2);
                break;

            default:
                throw new NotSupportedException("EonaCatDns: " + "EonaCatDns: AddressFamily not supported.");
        }

        binaryWriter.Write(address.GetAddressBytes());
    }
}