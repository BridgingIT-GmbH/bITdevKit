// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

public static class ConfigurationExtensions
{
    public static T Get<T>(this IConfiguration source, IModule module)
        where T : class
    {
        return source.GetSection(module)?.Get<T>() ?? Factory<T>.Create();
    }

    public static IConfiguration GetSection(this IConfiguration source, IModule module, bool skipPlaceholders = false)
    {
        return source.GetSection($"Modules:{module?.Name}", skipPlaceholders);
    }

    public static IConfiguration GetSection(this IConfiguration source, string key, bool skipPlaceholders)
    {
        var section = source?.GetSection(key);
        if(skipPlaceholders)
        {
            return section;
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
    /// Replaces placeholders in the given value with values from the configuration.
    /// </summary>
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
        return regex.Replace(value, match =>
        {
            var placeholder = match.Groups[1].Value;
            return configuration[placeholder] ?? match.Value;
        });
    }
}