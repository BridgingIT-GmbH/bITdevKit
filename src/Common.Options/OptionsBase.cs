// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Options;

/// <summary>
///     Base class for defining options that include logging capabilities.
/// </summary>
public abstract class OptionsBase : ILoggerOptions
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OptionsBase" /> class.
    ///     Base class for logger options, providing common functionality
    ///     for creating loggers.
    /// </summary>
    protected OptionsBase() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OptionsBase" /> class.
    ///     Provides a base class for logger options, allowing for the creation of loggers.
    /// </summary>
    protected OptionsBase(ILoggerFactory loggerFactory)
    {
        this.LoggerFactory = loggerFactory;
    }

    /// <summary>
    ///     Gets or sets the logger factory.
    /// </summary>
    /// <value>
    ///     A factory for creating logger instances.
    /// </value>
    public ILoggerFactory LoggerFactory { get; set; }

    /// <summary>
    ///     Creates the logger.
    /// </summary>
    /// <param name="categoryName">Name of the category.</param>
    /// <returns>A logger instance for the specified category name.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return this.LoggerFactory is null ? NullLogger.Instance : this.LoggerFactory.CreateLogger(categoryName);
    }

    /// <summary>
    ///     Creates the typed logger.
    /// </summary>
    /// <returns>
    ///     A logger instance for the given type.
    /// </returns>
    public ILogger<T> CreateLogger<T>()
    {
        return this.LoggerFactory is null ? new NullLogger<T>() : this.LoggerFactory.CreateLogger<T>();
    }
}