// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Exposes the mutable target configured by an options builder.</summary>
public interface IOptionsBuilder
{
    /// <summary>
    ///     Gets the target.
    /// </summary>
    /// <value>
    ///     The target.
    /// </value>
    object Target { get; }
}

/// <summary>Defines an options builder that produces a configured options instance.</summary>
/// <typeparam name="T">The options type produced by the builder.</typeparam>
public interface IOptionsBuilder<out T> : IOptionsBuilder
{
    /// <summary>Builds the options from the builder's current target state.</summary>
    /// <returns>The configured options instance.</returns>
    T Build();
}
