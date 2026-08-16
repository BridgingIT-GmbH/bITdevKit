// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

/// <summary>
///     Provides activity-source selection from registered application sources.
/// </summary>
public static class ActivitySourceExtensions
{
    /// <summary>
    ///     Finds a source by name, then the source named <c>default</c>, then the current activity's source.
    /// </summary>
    /// <param name="source">The registered activity sources.</param>
    /// <param name="name">The preferred source name, compared without regard to case.</param>
    /// <returns>The selected activity source, or <see langword="null"/> when none is available.</returns>
    [DebuggerStepThrough]
    public static ActivitySource Find(this IEnumerable<ActivitySource> source, string name)
    {
        if (source.IsNullOrEmpty())
        {
            return Activity.Current?.Source;
        }

        // get activitysource for name or default
        var result = source.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? source.FirstOrDefault(a => a.Name.Equals("default", StringComparison.OrdinalIgnoreCase));

        return result ?? Activity.Current?.Source;
    }
}
