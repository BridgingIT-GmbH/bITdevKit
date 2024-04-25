// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using BridgingIT.DevKit.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public abstract class OptionsBase : ILoggerOptions
{
    protected OptionsBase(ILoggerFactory loggerFactory)
    {
        this.LoggerFactory = loggerFactory;
    }

    protected OptionsBase()
    {
    }

    /// <summary>
    /// Gets or sets the logger factory.
    /// </summary>
    /// <value>
    /// The logger factory.
    /// </value>
    public ILoggerFactory LoggerFactory { get; set; }

    /// <summary>
    /// Creates the logger.
    /// </summary>
    /// <param name="categoryName">Name of the category.</param>
    public ILogger CreateLogger(string categoryName) => this.LoggerFactory is null
        ? NullLogger.Instance
        : this.LoggerFactory.CreateLogger(categoryName);

    /// <summary>
    /// Creates the typed logger.
    /// </summary>
    public ILogger<T> CreateLogger<T>() =>
        this.LoggerFactory is null ? new NullLogger<T>() : this.LoggerFactory.CreateLogger<T>();
}