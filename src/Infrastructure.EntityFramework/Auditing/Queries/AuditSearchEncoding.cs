// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.Globalization;
using System.Numerics;
using System.Text;
using BridgingIT.DevKit.Application.Auditing;

/// <summary>Produces explicit ordinal text and exact decimal search keys independent of database collation or floating point.</summary>
/// <example><code>var key = AuditSearchEncoding.Text("Customer", true);</code></example>
public static class AuditSearchEncoding
{
    private static readonly BigInteger scale = BigInteger.Pow(10, 28);
    private static readonly BigInteger complement = BigInteger.Pow(10, 58) - 1;

    /// <summary>Encodes ordinal UTF-16 units or case-insensitive Unicode scalars with explicit substring boundaries.</summary>
    /// <example><code>var key = AuditSearchEncoding.Text(value, ignoreCase: false);</code></example>
    public static string Text(string value, bool ignoreCase)
    {
        if (value is null) return null;
        if (ignoreCase) value = Fold(value);
        var buffer = new StringBuilder(value.Length * (ignoreCase ? 7 : 5) + 1);
        buffer.Append('|');
        for (var index = 0; index < value.Length; index++)
        {
            var point = (int)value[index];
            if (ignoreCase && char.IsHighSurrogate(value[index]) && index + 1 < value.Length && Rune.TryCreate(value[index], value[index + 1], out var rune))
            {
                point = rune.Value;
                index++;
            }

            buffer.Append(point.ToString(ignoreCase ? "X6" : "X4", CultureInfo.InvariantCulture)).Append('|');
        }

        return buffer.ToString();
    }

    /// <summary>Folds only BCL ordinal-equivalent rune pairs and preserves isolated UTF-16 code units.</summary>
    /// <remarks>Invariant cultural uppercase also folds some characters that OrdinalIgnoreCase keeps distinct.</remarks>
    /// <example><code>var normalized = AuditSearchEncoding.Fold(value);</code></example>
    public static string Fold(string value)
    {
        var result = new StringBuilder(value.Length);
        Span<char> upper = stackalloc char[2];
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character <= 127) { result.Append(character is >= 'a' and <= 'z' ? (char)(character - 32) : character); continue; }

            if (char.IsHighSurrogate(character) && index + 1 < value.Length && Rune.TryCreate(character, value[index + 1], out var rune))
            {
                var length = Rune.ToUpperInvariant(rune).EncodeToUtf16(upper);
                var original = value.AsSpan(index, 2);
                result.Append(original.Equals(upper[..length], StringComparison.OrdinalIgnoreCase) ? upper[..length] : original);
                index++;
                continue;
            }

            upper[0] = char.ToUpperInvariant(character);
            result.Append(value.AsSpan(index, 1).Equals(upper[..1], StringComparison.OrdinalIgnoreCase) ? upper[0] : character);
        }

        return result.ToString();
    }

    /// <summary>Encodes trusted field bindings into fixed ASCII JSON keys; client input never becomes a JSON path.</summary>
    /// <example><code>var key = AuditSearchEncoding.FieldKey(field.Binding);</code></example>
    public static string FieldKey(string binding) => "F" + Convert.ToHexString(Encoding.UTF8.GetBytes(Fold(binding)));

    /// <summary>Creates a fixed-width exact ordered key for the entire supported decimal range.</summary>
    /// <example><code>var key = AuditSearchEncoding.Number(42.5m);</code></example>
    public static string Number(decimal value)
    {
        var bits = decimal.GetBits(value);
        var coefficient = new BigInteger((uint)bits[0]) | new BigInteger((uint)bits[1]) << 32 | new BigInteger((uint)bits[2]) << 64;
        var magnitude = coefficient * BigInteger.Pow(10, 28 - ((bits[3] >> 16) & 0xff));
        return (value < 0 ? "0" : "1") + (value < 0 ? complement - magnitude : magnitude).ToString("D58", CultureInfo.InvariantCulture);
    }

    /// <summary>Encodes one validated comparable scalar; unavailable and policy-marker values have no comparable key.</summary>
    /// <example><code>var key = AuditSearchEncoding.Scalar(value, field.IgnoreCase);</code></example>
    public static string Scalar(AuditQueryValue value, bool ignoreCase) => value.Kind switch
    {
        "String" or "Guid" => Text(value.Text, ignoreCase),
        "Integer" or "Timestamp" => Number(value.Ticks.GetValueOrDefault()),
        "Decimal" => Number(value.Number.GetValueOrDefault()),
        "Boolean" => value.Boolean == true ? "1" : "0",
        _ => null
    };

    /// <summary>Decodes a selected normalized representative for aggregate/facet responses.</summary>
    /// <example><code>var displayed = AuditSearchEncoding.Decode(key, field.Kind);</code></example>
    public static string Decode(string key, string kind)
    {
        if (key is null) return null;
        if (kind is "String" or "Guid")
        {
            if (key.Length == 0 || key[0] != '|') throw new ArgumentException("AuditSearchProjectionInvalid");
            var result = new StringBuilder();
            for (var index = 1; index < key.Length;)
            {
                var next = key.IndexOf('|', index);
                var length = next - index;
                if (length is not (4 or 6)) throw new ArgumentException("AuditSearchProjectionInvalid");
                var point = int.Parse(key.AsSpan(index, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (point <= char.MaxValue) result.Append((char)point);
                else if (Rune.TryCreate(point, out var rune)) result.Append(rune.ToString());
                else throw new ArgumentException("AuditSearchProjectionInvalid");
                index = next + 1;
            }

            return result.ToString();
        }

        if (kind == "Boolean") return key == "1" ? "True" : "False";
        if (key.Length != 59 || key[0] is not ('0' or '1')) throw new ArgumentException("AuditSearchProjectionInvalid");
        var number = BigInteger.Parse(key.AsSpan(1), CultureInfo.InvariantCulture);
        if (key[0] == '0') number = -(complement - number);
        if (kind is "Integer" or "Timestamp") return (number / scale).ToString(CultureInfo.InvariantCulture);
        var negative = number < 0;
        number = BigInteger.Abs(number);
        var whole = BigInteger.DivRem(number, scale, out var fraction).ToString(CultureInfo.InvariantCulture);
        var decimals = fraction.ToString("D28", CultureInfo.InvariantCulture).TrimEnd('0');
        return (negative ? "-" : "") + whole + (decimals.Length > 0 ? "." + decimals : "");
    }
}
