// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;

/// <summary>
/// Factory interface for creating and configuring file storage providers.
/// Supports registration, creation, and behavior decoration of storage providers.
/// </summary>
public interface IFileStorageProviderFactory
{
    /// <summary>
    /// Creates a file storage provider with the specified name.
    /// </summary>
    /// <param name="name">The registered name of the provider to create.</param>
    /// <returns>An instance of the file storage provider.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no provider is registered with the specified name.</exception>
    IFileStorageProvider CreateProvider(string name);

    /// <summary>
    /// Creates a file storage provider of the specified implementation type.
    /// </summary>
    /// <typeparam name="TImplementation">The implementation type of the provider to create.</typeparam>
    /// <returns>An instance of the file storage provider matching the specified type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no provider of the specified type is registered or when multiple providers match the type.</exception>
    IFileStorageProvider CreateProvider<TImplementation>() where TImplementation : IFileStorageProvider;

    /// <summary>
    /// Registers a new file storage provider with the specified name and configuration.
    /// </summary>
    /// <param name="name">The unique name to register the provider with.</param>
    /// <param name="configure">A delegate to configure the provider using the builder.</param>
    /// <returns>The factory instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is null/empty or a provider with the same name is already registered.</exception>
    IFileStorageProviderFactory RegisterProvider(string name, Action<FileStorageProviderFactory.FileStorageBuilder> configure);

    /// <summary>
    /// Adds a behavior to a specific provider or to all registered providers.
    /// </summary>
    /// <param name="providerName">The name of the provider to add the behavior to, or null to apply to all providers.</param>
    /// <param name="behaviorFactory">A factory function to create the behavior wrapper.</param>
    /// <returns>The factory instance for method chaining.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the specified provider name is not registered.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the behavior factory is null.</exception>
    IFileStorageProviderFactory WithBehavior(string providerName, Func<IFileStorageProvider, IServiceProvider, IFileStorageBehavior> behaviorFactory);
}