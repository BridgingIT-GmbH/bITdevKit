// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;

internal static class ProfilingValueCodec
{
    public static ProfilingValue Add(ProfilingValue left, ProfilingValue right) => left.Type switch
    {
        ProfilingValueType.Int64 => Encode(checked(Int64(left) + Int64(right))),
        ProfilingValueType.Decimal => Encode(checked(Decimal(left) + Decimal(right))),
        ProfilingValueType.Double => Encode(Double(left) + Double(right)),
        _ => throw new ArgumentException("A compatible numeric scalar is required."),
    };

    public static int Compare(ProfilingValue left, ProfilingValue right) => left.Type switch
    {
        ProfilingValueType.Int64 => Int64(left).CompareTo(Int64(right)),
        ProfilingValueType.Decimal => Decimal(left).CompareTo(Decimal(right)),
        ProfilingValueType.Double => Double(left).CompareTo(Double(right)),
        _ => throw new ArgumentException("A compatible numeric scalar is required."),
    };

    public static ProfilingValue Divide(ProfilingValue value, long count) => value.Type switch
    {
        ProfilingValueType.Int64 => Encode((decimal)Int64(value) / count),
        ProfilingValueType.Decimal => Encode(Decimal(value) / count),
        ProfilingValueType.Double => Encode(Double(value) / count),
        _ => throw new ArgumentException("A numeric scalar is required."),
    };

    private static long Int64(ProfilingValue value) => long.Parse(value.Scalar, CultureInfo.InvariantCulture);
    private static decimal Decimal(ProfilingValue value) => decimal.Parse(value.Scalar, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static double Double(ProfilingValue value) => double.Parse(value.Scalar, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static ProfilingValue Encode(object value) => ProfilingValue.TryCreate(value, out var scalar)
        ? scalar : throw new OverflowException("The numeric aggregate is unavailable.");
}
