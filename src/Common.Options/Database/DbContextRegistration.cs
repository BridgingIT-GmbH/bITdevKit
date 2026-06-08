// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes one registered Entity Framework database context that can be sampled by operational dashboards.
/// </summary>
/// <example>
/// <code>
/// var registration = new DbContextRegistration(typeof(AppDbContext), "SqlServer", "AppDbContext");
/// </code>
/// </example>
public sealed class DbContextRegistration(
    Type contextType,
    string provider,
    string name = null)
{
    /// <summary>
    /// Gets the registered Entity Framework DbContext type.
    /// </summary>
    /// <example>
    /// <code>
    /// var type = registration.ContextType;
    /// </code>
    /// </example>
    public Type ContextType { get; } = contextType ?? throw new ArgumentNullException(nameof(contextType));

    /// <summary>
    /// Gets the database provider name.
    /// </summary>
    /// <example>
    /// <code>
    /// var provider = registration.Provider;
    /// </code>
    /// </example>
    public string Provider { get; } = string.IsNullOrWhiteSpace(provider) ? "Unknown" : provider;

    /// <summary>
    /// Gets the display name for the database context.
    /// </summary>
    /// <example>
    /// <code>
    /// var name = registration.Name;
    /// </code>
    /// </example>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? contextType?.Name : name;
}
