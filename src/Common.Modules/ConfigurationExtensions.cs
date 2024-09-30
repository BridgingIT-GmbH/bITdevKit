// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Configuration;

/// <summary>
///     Provides a set of extension methods for the <see cref="IConfiguration" /> interface
///     to facilitate configuration retrieval and section handling.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    ///     Retrieves an instance of type <typeparamref name="T" /> from the configuration section defined
    ///     by the specified module. If the configuration section is not found, a new instance is created using the factory.
    /// </summary>
    /// <typeparam name="T">The type of the object to retrieve or create.</typeparam>
    /// <param name="source">The configuration source to retrieve the section from.</param>
    /// <param name="module">The module defining the section in the configuration.</param>
    /// <returns>An instance of type <typeparamref name="T" />.</returns>
    public static T Get<T>(this IConfiguration source, IModule module)
        where T : class
    {
        return source.GetModuleSection(module)?.Get<T>() ?? Factory<T>.Create();
    }

    /// <summary>
    ///     Retrieves a configuration section based on the module's name.
    /// </summary>
    /// <param name="source">The configuration source.</param>
    /// <param name="module">The module from which the section name is derived.</param>
    /// <param name="skipPlaceholders">Indicates whether to skip placeholder substitution.</param>
    /// <returns>The configuration section corresponding to the specified module.</returns>
    public static IConfiguration GetModuleSection(
        this IConfiguration source,
        IModule module,
        bool skipPlaceholders = false)
    {
        return source.GetSection($"Modules:{module?.Name}", skipPlaceholders);
    }
}