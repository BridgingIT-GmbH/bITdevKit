// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;

/// <summary>
/// Provides helpers for calculating and displaying binary byte sizes.
/// </summary>
/// <remarks>
/// Size calculation helpers return Int64 byte counts suitable for configuration properties and fluent size limits.
/// Kilobytes, megabytes, gigabytes, and terabytes use powers of 1,024. Negative values are rejected,
/// and conversions throw on Int64 overflow. Use a checked cast when an API requires an Int32 byte count.
/// Display formatting accepts signed byte counts and uses invariant culture.
/// </remarks>
/// <example>
/// <code>
/// var maxPayloadBytes = ByteSize.Megabytes(2);
/// var cacheCapacity = ByteSize.Megabytes(8);
/// var bufferSize = checked((int)ByteSize.Kilobytes(64));
/// </code>
/// </example>
public static class ByteSize
{
    private static readonly string[] BinaryUnits = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];

    /// <summary>
    /// The number of bytes in one binary kilobyte.
    /// </summary>
    /// <example>
    /// <code>
    /// var size = ByteSize.BytesPerKilobyte;
    /// </code>
    /// </example>
    public const long BytesPerKilobyte = 1024L;

    /// <summary>
    /// The number of bytes in one binary megabyte.
    /// </summary>
    /// <example>
    /// <code>
    /// var size = ByteSize.BytesPerMegabyte;
    /// </code>
    /// </example>
    public const long BytesPerMegabyte = BytesPerKilobyte * 1024L;

    /// <summary>
    /// The number of bytes in one binary gigabyte.
    /// </summary>
    /// <example>
    /// <code>
    /// var size = ByteSize.BytesPerGigabyte;
    /// </code>
    /// </example>
    public const long BytesPerGigabyte = BytesPerMegabyte * 1024L;

    /// <summary>
    /// The number of bytes in one binary terabyte.
    /// </summary>
    /// <example>
    /// <code>
    /// var size = ByteSize.BytesPerTerabyte;
    /// </code>
    /// </example>
    public const long BytesPerTerabyte = BytesPerGigabyte * 1024L;

    /// <summary>
    /// Validates a raw byte count and returns it unchanged.
    /// </summary>
    /// <param name="value">The raw byte count.</param>
    /// <returns>The raw byte count.</returns>
    /// <example>
    /// <code>
    /// var size = ByteSize.Bytes(512);
    /// </code>
    /// </example>
    public static long Bytes(long value) => EnsureNonNegative(value);

    /// <summary>
    /// Converts binary kilobytes to bytes.
    /// </summary>
    /// <param name="value">The number of kilobytes.</param>
    /// <returns>The calculated byte count.</returns>
    /// <example>
    /// <code>
    /// var size = ByteSize.Kilobytes(64);
    /// </code>
    /// </example>
    public static long Kilobytes(long value) => Multiply(value, BytesPerKilobyte);

    /// <summary>
    /// Converts binary megabytes to bytes.
    /// </summary>
    /// <param name="value">The number of megabytes.</param>
    /// <returns>The calculated byte count.</returns>
    /// <example>
    /// <code>
    /// var size = ByteSize.Megabytes(1);
    /// </code>
    /// </example>
    public static long Megabytes(long value) => Multiply(value, BytesPerMegabyte);

    /// <summary>
    /// Converts binary gigabytes to bytes.
    /// </summary>
    /// <param name="value">The number of gigabytes.</param>
    /// <returns>The calculated byte count.</returns>
    /// <example>
    /// <code>
    /// var size = ByteSize.Gigabytes(2);
    /// </code>
    /// </example>
    public static long Gigabytes(long value) => Multiply(value, BytesPerGigabyte);

    /// <summary>
    /// Converts binary terabytes to bytes.
    /// </summary>
    /// <param name="value">The number of terabytes.</param>
    /// <returns>The calculated byte count.</returns>
    /// <example>
    /// <code>
    /// var size = ByteSize.Terabytes(1);
    /// </code>
    /// </example>
    public static long Terabytes(long value) => Multiply(value, BytesPerTerabyte);

    /// <summary>
    /// Converts a byte count to binary megabytes.
    /// </summary>
    /// <param name="bytes">The byte count.</param>
    /// <returns>The calculated megabyte value.</returns>
    /// <example>
    /// <code>
    /// var megabytes = ByteSize.ToMegabytes(bytes);
    /// </code>
    /// </example>
    public static double ToMegabytes(long bytes) => EnsureNonNegative(bytes) / (double)BytesPerMegabyte;

    /// <summary>
    /// Formats a signed byte count using binary units and at most two fractional digits.
    /// </summary>
    /// <param name="bytes">The byte count, including negative counts used for size differences.</param>
    /// <returns>An invariant-culture label without digit grouping, using B, KiB, MiB, GiB, TiB, PiB, or EiB.</returns>
    /// <remarks>Unit selection uses the absolute count and powers of 1,024. Midpoints round away from zero.</remarks>
    /// <example>
    /// <code>
    /// var payloadSize = ByteSize.FormatBinary(904512); // "883.31 KiB"
    /// var sizeChange = ByteSize.FormatBinary(-1536); // "-1.5 KiB"
    /// </code>
    /// </example>
    public static string FormatBinary(long bytes)
    {
        var value = (double)bytes;
        var unit = 0;
        while (Math.Abs(value) >= BytesPerKilobyte && unit < BinaryUnits.Length - 1)
        {
            value /= BytesPerKilobyte;
            unit++;
        }

        return Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture) + " " + BinaryUnits[unit];
    }

    private static long Multiply(long value, long factor)
    {
        EnsureNonNegative(value);

        return checked(value * factor);
    }

    private static long EnsureNonNegative(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Byte size values must be greater than or equal to zero.");
        }

        return value;
    }
}