// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.AspNetCore.Http.Json;
using System.Text.Json;

/// <summary>
/// Provides a uniform configuration for ASP.NET Core <see cref="JsonOptions"/> by
/// copying settings from the canonical <c>Common.DefaultJsonSerializerOptions.Create()</c>.
/// Use together with <see cref="ServiceCollectionExtensions.ConfigureJson"/> to apply
/// these settings application-wide.
/// </summary>
public static class DefaultJsonSerializerOptions
{
    /// <summary>
    /// Configures ASP.NET Core <see cref="JsonOptions"/> to match the canonical JSON
    /// settings defined in <c>BridgingIT.DevKit.Common.DefaultJsonSerializerOptions.Create()</c>.
    /// This method copies relevant serializer settings and converters to the framework's
    /// <see cref="JsonOptions.SerializerOptions"/> instance.
    /// </summary>
    /// <param name="options">
    /// The ASP.NET Core <see cref="JsonOptions"/> instance to configure.
    /// </param>
    public static void Configure(JsonOptions options)
    {
        CopyJsonOptions(
            BridgingIT.DevKit.Common.DefaultJsonSerializerOptions.Create(),
            options.SerializerOptions);
    }

    /// <summary>
    /// Copies JSON serializer settings from one <see cref="JsonSerializerOptions"/> instance
    /// to another, ensuring consistent serialization behavior across the application.
    /// Converters are replaced to avoid duplication.
    /// </summary>
    /// <param name="from">The source options to copy from.</param>
    /// <param name="to">The target options to copy into.</param>
    internal static void CopyJsonOptions(JsonSerializerOptions from, JsonSerializerOptions to)
    {
        // Replace converters to prevent duplicates if Configure is called multiple times.
        to.Converters.Clear();
        foreach (var c in from.Converters)
        {
            to.Converters.Add(c);
        }

        // Copy common serialization settings.
        to.WriteIndented = from.WriteIndented;
        to.PropertyNameCaseInsensitive = from.PropertyNameCaseInsensitive;
        to.PropertyNamingPolicy = from.PropertyNamingPolicy;
        to.DefaultIgnoreCondition = from.DefaultIgnoreCondition;
        to.TypeInfoResolver = from.TypeInfoResolver;
        to.ReferenceHandler = from.ReferenceHandler;
        to.Encoder = from.Encoder;
        to.NumberHandling = from.NumberHandling;
    }
}
