// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

public static class HashHelper
{
    public static string Compute(byte[] input)
    {
        if (input is null)
        {
            return string.Empty;
        }

        return BitConverter.ToString(
            MD5.HashData(input)).Replace("-", string.Empty).ToLowerInvariant();
    }

    public static string Compute(Stream stream)
    {
        if (stream is null)
        {
            return string.Empty;
        }

        using var ms = new MemoryStream();
        stream.Position = 0;
        stream.CopyTo(ms);

        return Compute(ms.ToArray());
    }

    public static string Compute(string input)
    {
        if (input is null)
        {
            return string.Empty;
        }

        return Compute(Encoding.UTF8.GetBytes(input));
    }

    public static string Compute(object input, ISerializer serializer = null)
    {
        if (input is null)
        {
            return string.Empty;
        }

        return Compute(SerializeToBytes(input));
    }

    private static byte[] SerializeToBytes(object input)
    {
        if (input is null)
        {
            return null;
        }

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input));
    }
}