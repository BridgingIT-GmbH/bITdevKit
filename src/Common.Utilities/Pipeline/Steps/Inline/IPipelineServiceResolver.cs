// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides scoped dependency resolution to advanced inline pipeline steps.
/// </summary>
public interface IPipelineServiceResolver
{
    /// <summary>
    /// Resolves the required service of type <typeparamref name="T"/>.
    /// </summary>
    T GetRequiredService<T>();

    /// <summary>
    /// Resolves the required service by its type.
    /// </summary>
    object GetRequiredService(Type serviceType);

    /// <summary>
    /// Resolves all registered services of type <typeparamref name="T"/>.
    /// </summary>
    IEnumerable<T> GetServices<T>();

    /// <summary>
    /// Resolves all registered services for the specified type.
    /// </summary>
    IEnumerable<object> GetServices(Type serviceType);

    /// <summary>
    /// Resolves an optional service by its type.
    /// </summary>
    object GetService(Type serviceType);
}
