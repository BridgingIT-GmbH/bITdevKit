// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;

/// <summary>
/// Provides ambient access to the current <see cref="TimeProvider"/> instance.
/// This enables domain entities and other non-DI-aware code to access time
/// without requiring constructor injection or method parameters.
/// </summary>
/// <remarks>
/// The current <see cref="TimeProvider"/> is stored in an <see cref="AsyncLocal{T}"/>
/// to ensure correct flow across asynchronous operations.
/// In production, it defaults to <see cref="TimeProvider.System"/>.
/// In tests, it can be overridden using <c>TimeProviderAccessor.Current = fake</c>.
/// </remarks>
public static class TimeProviderAccessor
{
    private static readonly AsyncLocal<TimeProvider> current = new();

    /// <summary>
    /// Gets or sets the current <see cref="TimeProvider"/>.
    /// When no value is explicitly set, returns <see cref="TimeProvider.System"/>.
    /// </summary>
    /// <value>
    /// The current <see cref="TimeProvider"/> instance.
    /// </value>
    public static TimeProvider Current
    {
        get => current.Value ?? TimeProvider.System;
        set => current.Value = value;
    }

    /// <summary>
    /// Resets the current <see cref="TimeProvider"/> to <c>null</c>,
    /// causing <see cref="Current"/> to fall back to <see cref="TimeProvider.System"/>.
    /// </summary>
    /// <remarks>
    /// Useful in test teardown to avoid cross-test pollution.
    /// </remarks>
    public static void Reset() => current.Value = null;

    /// <summary>
    /// Sets the current <see cref="TimeProvider"/> instance.
    /// Intended for internal use by <see cref="ServiceCollectionExtensions.AddTimeProvider(IServiceCollection, TimeProvider)"/>.
    /// </summary>
    /// <param name="provider">The <see cref="TimeProvider"/> to set as current.</param>
    public static void SetCurrent(TimeProvider provider) => Current = provider;
}
