// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Provides the modules selected for one application host.
/// </summary>
/// <remarks>
///     The registry is registered as a singleton in the host's service collection. Its module view is ordered by
///     <see cref="IModule.Priority" /> and then by <see cref="IModule.Name" /> using ordinal-ignore-case comparison.
/// </remarks>
/// <example>
/// <code>
/// var registry = app.Services.GetRequiredService&lt;IModuleRegistry&gt;();
/// foreach (var module in registry.Modules)
/// {
///     Console.WriteLine(module.Name);
/// }
/// </code>
/// </example>
public interface IModuleRegistry
{
    /// <summary>
    ///     Gets the host's selected modules in deterministic lifecycle order.
    /// </summary>
    /// <example>
    /// <code>
    /// var modules = app.Services.GetRequiredService&lt;IModuleRegistry&gt;().Modules;
    /// </code>
    /// </example>
    IReadOnlyList<IModule> Modules { get; }
}
