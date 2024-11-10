// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>
/// An implementation of the <see cref="IResultLogger"/> interface that performs no logging operations.
/// </summary>
/// <remarks>
/// This class is used as a default no-op logger when no other logger is provided.
/// Methods in this class do not perform any actions and are intended to be used as placeholders.
/// </remarks>
public class ResultNullLogger : IResultLogger
{
    /// <summary>
    /// Logs a message with context, content, result, and log level.
    /// </summary>
    /// <param name="context">The context in which the log is being recorded.</param>
    /// <param name="content">The content of the log message.</param>
    /// <param name="result">The result associated with the log entry.</param>
    /// <param name="logLevel">The severity level of the log entry.</param>
    public void Log(string context, string content, Result result, LogLevel logLevel) { }

    /// <summary>
    /// Logs the specified content, result, and log level within the context.
    /// </summary>
    /// <typeparam name="TContext">The type of the context for logging.</typeparam>
    /// <param name="content">The content to log.</param>
    /// <param name="result">The result associated with the log entry.</param.
    /// <param name="logLevel">The level of the log entry.</param>
    public void Log<TContext>(string content, Result result, LogLevel logLevel) { }
}