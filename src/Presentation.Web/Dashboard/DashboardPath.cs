// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>
/// Provides dashboard path helpers.
/// </summary>
/// <example>
/// <code>
/// var path = DashboardPath.Combine("/_bdk/dashboard", "/metrics");
/// </code>
/// </example>
public static class DashboardPath
{
    /// <summary>
    /// Combines dashboard path segments into one absolute path.
    /// </summary>
    /// <param name="segments">The path segments to combine.</param>
    /// <returns>An absolute path with one slash between non-empty segments.</returns>
    public static string Combine(params string[] segments)
    {
        var path = string.Join(
            "/",
            (segments ?? [])
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .Select(segment => segment.Trim('/'))
                .Where(segment => !string.IsNullOrWhiteSpace(segment)));

        return $"/{path}".TrimEnd('/');
    }
}
