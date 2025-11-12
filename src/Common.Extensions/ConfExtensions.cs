// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

public static class ConfExtensions // name deliberately chosen to avoid conflict with ConfigurationExtensions
{
    /// <summary>
    ///     Retrieves a configuration section by the given key.
    /// </summary>
    /// <param name="source">The source configuration.</param>
    /// <param name="key">The key of the configuration section to retrieve.</param>
    /// <param name="skipPlaceholders">If true, placeholders in the section values will not be replaced.</param>
    /// <returns>The configuration section corresponding to the specified key.</returns>
    public static IConfiguration GetSection(this IConfiguration source, string key, bool skipPlaceholders)
    {
        var section = source?.GetSection(key);
        if (skipPlaceholders)
        {
            return section;
        }

        if (section == null)
        {
            return null;
        }

        // Replace optional placeholders in the child section
        foreach (var childSectionKey in section.AsEnumerable()
                     .Select(kvp => kvp.Key.Replace(key + ":", string.Empty)))
        {
            var childSection = section.GetSection(childSectionKey);
            childSection.Value = ReplacePlaceholders(childSection.Value, source);
        }

        return section;
    }

    /// <summary>
    ///     Replaces placeholders in the given value with values from the configuration.
    /// </summary>
    /// <param name="value">The string containing placeholders to be replaced.</param>
    /// <param name="configuration">The configuration source used to replace the placeholders.</param>
    /// <returns>The string with placeholders replaced by their corresponding configuration values.</returns>
    private static string ReplacePlaceholders(string value, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        const string placeholderStart = "{{";
        const string placeholderEnd = "}}";

        if (!value.Contains(placeholderStart) || !value.Contains(placeholderEnd))
        {
            return value;
        }

        var regex = new Regex($@"{Regex.Escape(placeholderStart)}(.*?){Regex.Escape(placeholderEnd)}");

        return regex.Replace(value,
            match =>
            {
                var placeholder = match.Groups[1].Value;

                return configuration[placeholder] ?? match.Value;
            });
    }
}