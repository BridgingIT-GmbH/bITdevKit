// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Inspired by
///     <see
///         href="https://github.com/aspnet/KestrelHttpServer/blob/6fde01a825cffc09998d3f8a49464f7fbe40f9c4/src/Kestrel.Core/Internal/Infrastructure/CorrelationIdGenerator.cs" />
///     ,
///     this class generates an efficient 20-bytes ID which is the concatenation of a <c>base36</c> encoded
///     machine name and <c>base32</c> encoded <see cref="long" /> using the alphabet <c>0-9</c> and <c>A-V</c>.
/// </summary>
public static class IdGenerator
{
    // origin: http://www.nimaara.com/2018/10/10/generating-ids-in-csharp/
    private const string Characters = "0123456789ABCDEFGHIJKLMNOPQRSTUV";
    private static readonly char[] Prefix = new char[6];

    private static readonly ThreadLocal<char[]> CharBufferThreadLocal = new(() =>
    {
        var buffer = new char[20];
        buffer[0] = Prefix[0];
        buffer[1] = Prefix[1];
        buffer[2] = Prefix[2];
        buffer[3] = Prefix[3];
        buffer[4] = Prefix[4];
        buffer[5] = Prefix[5];
        buffer[6] = '0';
        return buffer;
    });

    private static long lastId = DateTime.UtcNow.Ticks;

    static IdGenerator()
    {
        PopulatePrefix();
    }

    /// <summary>
    ///     Returns a sequential string like <c>X9IQ0R00HMFOG3UVQ8G4</c>.
    /// </summary>
    public static string Create()
    {
        return Generate(Interlocked.Increment(ref lastId));
    }

    private static string Generate(long id)
    {
        var buffer = CharBufferThreadLocal.Value;
        buffer[7] = Characters[(int)(id >> 60) & 31];
        buffer[8] = Characters[(int)(id >> 55) & 31];
        buffer[9] = Characters[(int)(id >> 50) & 31];
        buffer[10] = Characters[(int)(id >> 45) & 31];
        buffer[11] = Characters[(int)(id >> 40) & 31];
        buffer[12] = Characters[(int)(id >> 35) & 31];
        buffer[13] = Characters[(int)(id >> 30) & 31];
        buffer[14] = Characters[(int)(id >> 25) & 31];
        buffer[15] = Characters[(int)(id >> 20) & 31];
        buffer[16] = Characters[(int)(id >> 15) & 31];
        buffer[17] = Characters[(int)(id >> 10) & 31];
        buffer[18] = Characters[(int)(id >> 5) & 31];
        buffer[19] = Characters[(int)id & 31];

        return new string(buffer, 0, buffer.Length);
    }

    private static void PopulatePrefix()
    {
        var machine = Base36.Encode(Math.Abs(Environment.MachineName.GetHashCode()));
        var i = Prefix.Length - 1;
        var j = 0;

        while (i >= 0)
        {
            if (j < machine.Length)
            {
                Prefix[i] = machine[j];
                j++;
            }
            else
            {
                Prefix[i] = '0';
            }

            i--;
        }
    }
}

/// <summary>
///     Provides methods to encode and decode numbers in Base36.
/// </summary>
public static class Base36
{
    private const string Base36Characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    ///     Encode the given number into a <see cref="Base36" />string.
    /// </summary>
    /// <param name="input">The number to encode.</param>
    /// <returns>Encoded <paramref name="input" /> as string.</returns>
    public static string Encode(long input)
    {
        EnsureArg.IsTrue(input >= 0, nameof(input));

        var arr = Base36Characters.ToCharArray();
        var result = new Stack<char>();
        while (input != 0)
        {
            result.Push(arr[input % 36]);
            input /= 36;
        }

        return new string(result.ToArray());
    }

    /// <summary>
    ///     Decode the <see cref="Base36" /> encoded string into a long.
    /// </summary>
    /// <param name="input">The number to decode.</param>
    /// <returns>Decoded <paramref name="input" /> as long.</returns>
    public static long Decode(string input)
    {
        EnsureArg.IsNotNull(input, nameof(input));

        var reversed = input.ToLowerInvariant().Reverse();
        long result = 0;
        var pos = 0;
        foreach (var c in reversed)
        {
            result += Base36Characters.IndexOf(c) * (long)Math.Pow(36, pos);
            pos++;
        }

        return result;
    }
}