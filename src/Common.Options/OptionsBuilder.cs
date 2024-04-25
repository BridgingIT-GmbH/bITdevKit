// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public delegate TBuilder Builder<TBuilder, TOptions>(TBuilder builder)
    where TBuilder : class, IOptionsBuilder<TOptions>, new();

public class OptionsBuilder<T> : IOptionsBuilder<T>
    where T : class, new()
{
    /// <summary>
    /// Gets the target.
    /// </summary>
    /// <value>
    /// The target.
    /// </value>
    public T Target { get; } = new T();

    /// <summary>
    /// Gets the target.
    /// </summary>
    /// <value>
    /// The target.
    /// </value>
    object IOptionsBuilder.Target => this.Target;

    /// <summary>
    /// Builds this options instance.
    /// </summary>
    public virtual T Build()
    {
        return this.Target;
    }
}