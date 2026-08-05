// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Cryptography;

/// <summary>
///     Provides methods for generating random keys.
/// </summary>
/// <example><code>var key = KeyGenerator.CreateLowercase(12);</code></example>
public static class KeyGenerator
{
    private static readonly char[] Chars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
    private static readonly char[] LowercaseChars =
        "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
    private static readonly char[] UppercaseChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

    /// <summary>
    ///     Generates a random string key of the specified size.
    /// </summary>
    /// <param name="size">The length of the generated key.</param>
    /// <returns>A randomly generated string key of the specified size.</returns>
    /// <example><code>var key = KeyGenerator.Create(32);</code></example>
    public static string Create(int size) => Create(size, Chars);

    /// <summary>
    ///     Generates a cryptographically random lowercase alphanumeric key of the specified size.
    /// </summary>
    /// <param name="size">The length of the generated key.</param>
    /// <returns>A random key containing only lowercase ASCII letters and digits.</returns>
    /// <example><code>var identifier = KeyGenerator.CreateLowercase(12);</code></example>
    public static string CreateLowercase(int size) => Create(size, LowercaseChars);

    /// <summary>
    ///     Generates a cryptographically random uppercase alphanumeric key of the specified size.
    /// </summary>
    /// <param name="size">The length of the generated key.</param>
    /// <returns>A random key containing only uppercase ASCII letters and digits.</returns>
    /// <example><code>var identifier = KeyGenerator.CreateUppercase(12);</code></example>
    public static string CreateUppercase(int size) => Create(size, UppercaseChars);

    private static string Create(int size, char[] characters)
    {
        var data = new byte[4 * size];
        RandomNumberGenerator.Fill(data);

        var result = new StringBuilder(size);
        for (var i = 0; i < size; i++)
        {
            var rnd = BitConverter.ToUInt32(data, i * 4);
            var idx = rnd % characters.Length;

            result.Append(characters[idx]);
        }

        return result.ToString();
    }
}
