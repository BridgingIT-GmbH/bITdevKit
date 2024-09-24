// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static class ActivitySourceExtensions
{
    [DebuggerStepThrough]
    public static ActivitySource Find(this IEnumerable<ActivitySource> source, string name)
    {
        if (source.IsNullOrEmpty())
        {
            return Activity.Current?.Source;
        }

        var result = source.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (result is null) // get fallback activitysource
        {
            result = source.FirstOrDefault(a => a.Name.Equals("default", StringComparison.OrdinalIgnoreCase));
        }

        return result;
    }
}