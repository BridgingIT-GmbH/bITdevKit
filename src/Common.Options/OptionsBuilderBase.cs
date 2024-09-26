// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>
///     Provides a base class for building specific types of options with support for logging.
/// </summary>
/// <typeparam name="TOption">The type of options to be built, derived from OptionsBase.</typeparam>
/// <typeparam name="TBuilder">
///     The type of the builder itself, allowing for a fluent interface by returning the concrete builder type from
///     methods.
/// </typeparam>
public abstract class OptionsBuilderBase<TOption, TBuilder> : OptionsBuilder<TOption>
    where TOption : OptionsBase, new()
    where TBuilder : OptionsBuilderBase<TOption, TBuilder>
{
    /// <summary>
    ///     Sets the logger factory.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The configuration builder.</returns>
    public TBuilder LoggerFactory(ILoggerFactory loggerFactory)
    {
        this.Target.LoggerFactory = loggerFactory;

        return (TBuilder)this;
    }
}