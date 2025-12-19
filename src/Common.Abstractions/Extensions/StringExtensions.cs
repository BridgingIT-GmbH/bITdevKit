// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Linq;

public static class StringExtensions
{
    public static string Distinct(this string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var words = source.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var distinctWords = words.Distinct();

        return string.Join(" ", distinctWords);
    }
}
