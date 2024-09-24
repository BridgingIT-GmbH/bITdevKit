// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Cryptography;

/// <summary>
///     Provides methods for generating random keys.
/// </summary>
public static class KeyGenerator
{
    private static readonly char[] Chars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();

    /// <summary>
    ///     Generates a random string key of the specified size.
    /// </summary>
    /// <param name="size">The length of the generated key.</param>
    /// <return>A randomly generated string key of the specified size.</return>
    public static string Create(int size)
    {
        var data = new byte[4 * size];
        using (var crypto = RandomNumberGenerator.Create())
        {
            crypto.GetBytes(data);
        }

        var result = new StringBuilder(size);
        for (var i = 0; i < size; i++)
        {
            var rnd = BitConverter.ToUInt32(data, i * 4);
            var idx = rnd % Chars.Length;

            result.Append(Chars[idx]);
        }

        return result.ToString();
    }
}