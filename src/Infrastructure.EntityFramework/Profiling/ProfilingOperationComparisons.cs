// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Common;

internal static class ProfilingOperationComparisons
{
    // Big-endian UTF-16 preserves .NET ordinal code-unit ordering on every database engine.
    internal static byte[] Exact(string value) => value is null ? null : Encoding.BigEndianUnicode.GetBytes(value);
    internal static byte[] Canonical(string value) => Exact(ProfilingKeyComparer.Canonicalize(value));
    internal static byte[] Hash(byte[] value) => value is null ? null : SHA256.HashData(value);
    internal static byte[] Part(string value) => Exact(value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value);
    internal static byte[] ValuePart(ProfilingValue value) => Part(value is null ? "missing" : "present:" + value.Type + ":" + value.Scalar);
    internal static byte[] MethodPart(string method) => Part(method is null ? "missing" : "present:" + ProfilingKeyComparer.Canonicalize(method));
    internal static byte[] BaseGroup(string kind, string key) => Part(ProfilingKeyComparer.Canonicalize(kind)).Concat(Part(ProfilingKeyComparer.Canonicalize(key))).ToArray();
    internal static Guid SummaryId(Guid operationId, byte[] path)
    {
        var input = operationId.ToByteArray().Concat(path).ToArray();
        return new Guid(SHA256.HashData(input).AsSpan(0, 16));
    }
}
