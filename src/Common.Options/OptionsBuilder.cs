// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents a delegate definition for building options using a specified builder type.
/// </summary>
/// <typeparam name="TBuilder">The type of the builder.</typeparam>
/// <typeparam name="TOptions">The type of the options to be built.</typeparam>
/// <param name="builder">The builder instance used to configure the options.</param>
/// <returns>The configured builder instance.</returns>
public delegate TBuilder Builder<TBuilder, TOptions>(TBuilder builder)
    where TBuilder : class, IOptionsBuilder<TOptions>, new();

/// <summary>
///     Provides a base implementation for building options of a specific type.
/// </summary>
/// <typeparam name="T">The type of options to be built.</typeparam>
public class OptionsBuilder<T> : IOptionsBuilder<T>
    where T : class, new()
{
    /// <summary>
    ///     Gets the target instance.
    /// </summary>
    /// <value>
    ///     The target instance of type <typeparamref name="T" />.
    /// </value>
    public T Target { get; } = new();

    /// <summary>
    ///     Gets the target options instance.
    /// </summary>
    /// <value>
    ///     The target options.
    /// </value>
    object IOptionsBuilder.Target => this.Target;

    /// <summary>
    ///     Builds this options instance.
    /// </summary>
    /// <returns>
    ///     An instance of the configured options.
    /// </returns>
    public virtual T Build()
    {
        return this.Target;
    }
}