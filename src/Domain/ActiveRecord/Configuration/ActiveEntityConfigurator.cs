// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Threading;

/// <summary>
/// Manages the global service provider for the Active Entity pattern, enabling dependency resolution for providers and behaviors.
/// </summary>
public static class ActiveEntityConfigurator
{
    private static IServiceProvider serviceProvider;
    private static readonly ReaderWriterLockSlim lockObject = new();

    /// <summary>
    /// Gets the global service provider instance.
    /// </summary>
    /// <returns>The current service provider, or null if not set.</returns>
    /// <example>
    /// <code>
    /// var provider = ActiveEntityConfigurator.GetGlobalServiceProvider();
    /// if (provider != null)
    /// {
    ///     var customerProvider = provider.GetService&lt;IActiveEntityEntityProvider&lt;Customer, CustomerId&gt;&gt;();
    /// }
    /// </code>
    /// </example>
    public static IServiceProvider GetGlobalServiceProvider()
    {
        lockObject.EnterReadLock();
        try
        {
            return serviceProvider;
        }
        finally
        {
            lockObject.ExitReadLock();
        }
    }

    /// <summary>
    /// Sets the global service provider for dependency resolution.
    /// </summary>
    /// <param name="provider">The service provider to set.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provider is null.</exception>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// services.AddActiveEntity(cfg => cfg.For&lt;Customer, CustomerId&gt;().UseInMemoryProvider());
    /// ActiveEntityConfigurator.SetGlobalServiceProvider(services.BuildServiceProvider());
    /// </code>
    /// </example>
    public static void SetGlobalServiceProvider(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lockObject.EnterWriteLock();
        try
        {
            serviceProvider = provider;
        }
        finally
        {
            lockObject.ExitWriteLock();
        }
    }
}