// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Marks the end of configuration composition on a DevKit application builder.
/// </summary>
/// <remarks>
/// Features that rewrite configuration, such as linked-worktree development isolation, register an action under
/// <see cref="DevKitBuilderProperties.ConfigurationCompletion"/>. Extensions that read configuration while registering,
/// such as logging and module registration, call <see cref="CompleteConfiguration{TBuilder}"/> first.
/// </remarks>
/// <example>
/// <code>
/// public static TBuilder AddReporting&lt;TBuilder&gt;(this TBuilder builder)
///     where TBuilder : IDevKitApplicationBuilder
/// {
///     builder.CompleteConfiguration();
///     var connectionString = builder.Configuration.GetConnectionString("Reporting");
///     return builder;
/// }
/// </code>
/// </example>
public static class DevKitApplicationBuilderConfigurationExtensions
{
    /// <summary>
    /// Runs the registered configuration completion action once. Later calls do nothing.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <param name="builder">The DevKit application builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.CompleteConfiguration();
    /// </code>
    /// </example>
    public static TBuilder CompleteConfiguration<TBuilder>(this TBuilder builder)
        where TBuilder : IDevKitApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Properties.TryGetValue(DevKitBuilderProperties.ConfigurationCompletion, out var value))
        {
            builder.Properties.Remove(DevKitBuilderProperties.ConfigurationCompletion);
            (value as Action)?.Invoke();
        }

        return builder;
    }
}
