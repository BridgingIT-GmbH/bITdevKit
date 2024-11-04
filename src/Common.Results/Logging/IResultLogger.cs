// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>
/// Defines methods for logging results with various levels and contexts.
/// </summary>
public interface IResultLogger
{
    /// <summary>
    /// Logs the given context, content, and result using the specified log level.
    /// </summary>
    /// <param name="context">The context in which the log is being made.</param>
    /// <param name="content">The content of the log message.</param>
    /// <param name="result">The result object containing information about the operation.</param>
    /// <param name="logLevel">The level of the log (e.g., Information, Warning, Error).</param>
    void Log(string context, string content, Result result, LogLevel logLevel);

    /// <summary>
    /// Logs a provided message along with the result and a specified log level.
    /// </summary>
    /// <typeparam name="TContext">The context type in which the log operation occurs.</typeparam>
    /// <param name="content">The message content to be logged.</param>
    /// <param name="result">The result object containing additional error or success information.</param>
    /// <param name="logLevel">The level of the log (e.g., Information, Warning, Error).</param>
    void Log<TContext>(string content, Result result, LogLevel logLevel);
}