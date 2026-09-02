// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Provides naming, priority, enablement, and compatibility registration metadata for application modules.
/// </summary>
/// <example>
/// <code>
/// public sealed class CustomerModule : ModuleBase
/// {
///     // Implement Register and Use for the customer feature.
/// }
/// </code>
/// </example>
public abstract class ModuleBase : IModule
{
    /// <summary>
    ///     Initializes a module with a name derived from its type and the default priority.
    /// </summary>
    protected ModuleBase()
        : this(null) { }

    /// <summary>
    ///     Initializes a module with an explicit name and priority.
    /// </summary>
    /// <param name="name">The module name, or <see langword="null"/> to derive it from the type name.</param>
    /// <param name="priority">The module processing priority.</param>
    protected ModuleBase(string name, int priority = 99)
    {
        this.Name = name ??
            this.GetType().Name.Replace("Module", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        this.Priority = priority;
    }

    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public int Priority { get; } = 99;

    /// <inheritdoc/>
    public bool IsRegistered { get; set; }

    /// <inheritdoc/>
    public abstract IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null);

    /// <inheritdoc/>
    public abstract IApplicationBuilder Use(
        IApplicationBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null);
}
