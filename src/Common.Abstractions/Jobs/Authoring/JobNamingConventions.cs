// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Derives normalized, dash-separated names for code-first job definitions.
/// </summary>
public static class JobNamingConventions
{
    /// <summary>
    /// Creates a lowercase display name from the job type and optionally prefixes it with the module name.
    /// </summary>
    /// <param name="jobType">The job type whose type name is converted to dash-separated lowercase text.</param>
    /// <param name="module">The optional module prefix, also converted to dash-separated lowercase text.</param>
    /// <returns>The normalized job name, prefixed with the normalized module and a hyphen when a module is supplied.</returns>
    /// <exception cref="NullReferenceException">Thrown when <paramref name="jobType"/> is <see langword="null"/>.</exception>
    public static string ResolveDisplayName(Type jobType, string module)
    {
        var jobName = Dashify(jobType.Name);
        return string.IsNullOrWhiteSpace(module)
            ? jobName
            : $"{Dashify(module)}-{jobName}";
    }

    private static string Dashify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (char.IsUpper(character) && i > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
