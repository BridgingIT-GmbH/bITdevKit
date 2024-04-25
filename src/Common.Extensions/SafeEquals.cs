// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>
    /// Safeky compares the source to the value string.
    /// </summary>
    /// <param name="source">the source string.</param>
    /// <param name="value">the value string to compare to.</param>
    /// <param name="comparisonType">the comparison type.</param>
    /// <returns>true if equal, otherwhise false.</returns>
    [DebuggerStepThrough]
    public static bool SafeEquals(
        this string source,
        string value,
        StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
    {
        if (source is null && value is null)
        {
            return true;
        }

        if (source is null && value is not null)
        {
            return false;
        }

        return source.Equals(value, comparisonType);
    }
}