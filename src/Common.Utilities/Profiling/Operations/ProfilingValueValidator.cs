// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal static class ProfilingValueValidator
{
    public static bool IsKey(string value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && IsUnicode(value);

    public static bool TryValue(object value, OperationProfilingOptions options, out ProfilingValue scalar)
    {
        if (!ProfilingValue.TryCreate(value, out scalar))
        {
            return false;
        }

        return scalar.Type != ProfilingValueType.String
            || scalar.Scalar.Length <= options.MaxStringLength && IsUnicode(scalar.Scalar);
    }

    public static bool TryNumber(object value, OperationProfilingOptions options, out ProfilingValue scalar) =>
        TryValue(value, options, out scalar)
        && scalar.Type is ProfilingValueType.Int64 or ProfilingValueType.Decimal or ProfilingValueType.Double;

    public static long Charge(string value) => value is null ? 0 : 32L + value.Length * 2L;
    public static long Charge(ProfilingValue value) => value is null ? 0 : 64L + Charge(value.Scalar);

    public static string Clip(string value, int maximum, out bool truncated)
    {
        truncated = value?.Length > maximum;
        if (!truncated)
        {
            return IsUnicode(value) ? value : null;
        }

        var length = maximum;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }

        var clipped = value[..length];
        return IsUnicode(clipped) ? clipped : null;
    }

    internal static bool IsUnicode(string value)
    {
        if (value is null)
        {
            return true;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index]))
                {
                    return false;
                }
            }
            else if (char.IsLowSurrogate(value[index]))
            {
                return false;
            }
        }

        return true;
    }
}
