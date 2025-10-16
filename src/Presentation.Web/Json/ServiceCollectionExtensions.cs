// MIT-License
// Copyright ...
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.AspNetCore.Http.Json;
using System.Text.Json;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to apply JSON serializer
/// configuration across the application and wire the same options into FilterModel.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures ASP.NET Core <see cref="JsonOptions"/> using the canonical defaults
    /// (from <c>BridgingIT.DevKit.Common.DefaultJsonSerializerOptions.Create()</c>) and
    /// also configures default JSON-aware components to use the same effective options.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    /// <remarks>
    /// Default component configurators include:
    /// - <c>BridgingIT.DevKit.Common.FilterModel.ConfigureJson</c>
    /// </remarks>
    public static IServiceCollection ConfigureJson(this IServiceCollection services)
    {
        services.Configure<JsonOptions>(o =>
        {
            // Apply canonical defaults to the framework's live options instance.
            DefaultJsonSerializerOptions.Configure(o);

            // Invoke default component configurators with the same live instance.
            BridgingIT.DevKit.Common.FilterModel.ConfigureJson(o.SerializerOptions);
        });

        return services;
    }

    /// <summary>
    /// Configures ASP.NET Core <see cref="JsonOptions"/> using the canonical defaults
    /// and then invokes optional component configurators with the same effective options.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configureComponents">
    /// Optional component configurators that receive the live <see cref="JsonSerializerOptions"/>
    /// instance to align their behavior (e.g., <c>Common.FilterModel.ConfigureJson</c>).
    /// If none are provided, a sensible default is applied (FilterModel).
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection ConfigureJson(
        this IServiceCollection services,
        params Action<JsonSerializerOptions>[] configureComponents)
    {
        services.Configure<JsonOptions>(o =>
        {
            // Apply canonical defaults.
            DefaultJsonSerializerOptions.Configure(o);

            // Always include FilterModel as a default configurator.
            BridgingIT.DevKit.Common.FilterModel.ConfigureJson(o.SerializerOptions);

            // Invoke any additional component configurators.
            if (configureComponents != null)
            {
                foreach (var comp in configureComponents)
                {
                    comp?.Invoke(o.SerializerOptions);
                }
            }
        });

        return services;
    }

    /// <summary>
    /// Configures ASP.NET Core <see cref="JsonOptions"/> using only the caller-provided
    /// configuration delegate (bypasses canonical defaults) and invokes optional component
    /// configurators with the same effective options.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configure">
    /// A delegate that mutates the framework's live <see cref="JsonSerializerOptions"/> directly.
    /// </param>
    /// <param name="configureComponents">
    /// Optional component configurators that receive the live <see cref="JsonSerializerOptions"/>
    /// instance to align their behavior.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection ConfigureJson(
        this IServiceCollection services,
        Action<JsonSerializerOptions> configure,
        params Action<JsonSerializerOptions>[] configureComponents)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure<JsonOptions>(o =>
        {
            // Let the caller fully configure the live JsonOptions.
            configure(o.SerializerOptions);

            // Always include FilterModel as a default configurator.
            BridgingIT.DevKit.Common.FilterModel.ConfigureJson(o.SerializerOptions);

            // Invoke any additional component configurators.
            if (configureComponents != null)
            {
                foreach (var comp in configureComponents)
                {
                    comp?.Invoke(o.SerializerOptions);
                }
            }
        });

        return services;
    }

    /// <summary>
    /// Configures ASP.NET Core <see cref="JsonOptions"/> by copying settings from the provided
    /// <see cref="JsonSerializerOptions"/> instance (bypasses canonical defaults) and invokes
    /// optional component configurators with the same effective options.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="options">
    /// The source <see cref="JsonSerializerOptions"/> to copy from into ASP.NET Core's live options.
    /// </param>
    /// <param name="configureComponents">
    /// Optional component configurators that receive the live <see cref="JsonSerializerOptions"/>
    /// instance to align their behavior.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection ConfigureJson(
        this IServiceCollection services,
        JsonSerializerOptions options,
        params Action<JsonSerializerOptions>[] configureComponents)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.Configure<JsonOptions>(o =>
        {
            // Copy caller-provided options into the framework's live instance.
            DefaultJsonSerializerOptions.CopyJsonOptions(options, o.SerializerOptions);

            // Always include FilterModel as a default configurator.
            BridgingIT.DevKit.Common.FilterModel.ConfigureJson(o.SerializerOptions);

            // Invoke any additional component configurators.
            if (configureComponents != null)
            {
                foreach (var comp in configureComponents)
                {
                    comp?.Invoke(o.SerializerOptions);
                }
            }
        });

        return services;
    }
}