// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

public abstract class OptionsBuilderBase<TOption, TBuilder> : OptionsBuilder<TOption>
    where TOption : OptionsBase, new()
    where TBuilder : OptionsBuilderBase<TOption, TBuilder>
{
    /// <summary>
    /// Sets the logger factory.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    public TBuilder LoggerFactory(ILoggerFactory loggerFactory)
    {
        this.Target.LoggerFactory = loggerFactory;
        return (TBuilder)this;
    }
}