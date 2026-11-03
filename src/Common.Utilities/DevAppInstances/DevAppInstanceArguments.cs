// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;

/// <summary>
/// Reserved command-line arguments that start a host in a one-shot development instance mode.
/// </summary>
/// <remarks>
/// <c>bdk dev reset</c> and <c>bdk dev prune --confirm</c> start the application project with these arguments.
/// The host runs the declared adapters instead of starting the listener, then exits with <see cref="ExitCodes"/>.
/// </remarks>
/// <example>
/// <code>
/// var args = new[] { DevAppInstanceArguments.FormatReset("forecasthub", 2) };
/// // args[0] == "--bdk-dev-reset=forecasthub:w02"
/// </code>
/// </example>
public static class DevAppInstanceArguments
{
    /// <summary>The reset argument name. Its value is the confirmation token <c>&lt;appId&gt;:w&lt;slot:00&gt;</c>.</summary>
    public const string Reset = "--bdk-dev-reset";

    /// <summary>The orphan-reset argument name. Its value is the orphaned worktree ID.</summary>
    public const string OrphanReset = "--bdk-dev-orphan-reset";

    /// <summary>
    /// Formats the instance confirmation token, for example <c>forecasthub:w02</c>.
    /// </summary>
    /// <param name="appId">The application ID.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The token.</returns>
    public static string FormatInstanceToken(string appId, int slot) =>
        $"{appId}:w{slot.ToString("00", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Formats the reset argument for an instance.
    /// </summary>
    /// <param name="appId">The application ID.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The argument.</returns>
    public static string FormatReset(string appId, int slot) => $"{Reset}={FormatInstanceToken(appId, slot)}";

    /// <summary>
    /// Formats the orphan-reset argument for a worktree ID.
    /// </summary>
    /// <param name="worktreeId">The orphaned worktree ID.</param>
    /// <returns>The argument.</returns>
    public static string FormatOrphanReset(Guid worktreeId) => $"{OrphanReset}={worktreeId:D}";

    /// <summary>
    /// Finds the value of a reserved argument in <c>--name=value</c> or <c>--name value</c> form.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The value, an empty string when the argument has no value, or <c>null</c> when absent.</returns>
    public static string Find(IReadOnlyList<string> args, string name)
    {
        if (args is null)
        {
            return null;
        }

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, name, StringComparison.Ordinal))
            {
                return i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[i + 1] : string.Empty;
            }

            if (arg?.StartsWith(name + "=", StringComparison.Ordinal) == true)
            {
                return arg[(name.Length + 1)..];
            }
        }

        return null;
    }

    /// <summary>
    /// The process exit codes of the one-shot modes.
    /// </summary>
    public static class ExitCodes
    {
        /// <summary>Every target was removed.</summary>
        public const int Success = 0;

        /// <summary>Validation failed and no resource changed.</summary>
        public const int Refused = 2;

        /// <summary>Some targets were removed and some remain (<c>reset_incomplete</c>).</summary>
        public const int Incomplete = 3;
    }
}
