// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides convenience helpers for calculating binary byte sizes.
/// </summary>
/// <example>
/// <code>
/// var maxValueSize = ByteSize.Megabytes(1);
/// var chunkSize = ByteSize.Megabytes(4);
/// </code>
/// </example>
public static class ByteSize
{
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
