// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>
/// An implementation of the <see cref="IRuleLogger"/> interface that performs no logging operations.
/// </summary>
/// <remarks>
/// This class is used as a default no-op logger when no other logger is provided.
/// Methods in this class do not perform any actions and are intended to be used as placeholders.
/// </remarks>
public class RuleNullLogger : IRuleLogger
{
    /// <summary>
    /// Logs the given context, content, and result using the specified log level.
    /// </summary>
    /// <param name="context">The context in which the log is being made.</param>
    /// <param name="content">The content of the log message.</param>
    /// <param name="rule">The rule object containing information about the operation.</param>
    /// <param name="result"></param>
    /// <param name="logLevel">The level of the log (e.g., Information, Warning, Error).</param>
    public void Log(string context, string content, IRule rule, IResult result, LogLevel logLevel) { }

    /// <summary>
    /// Logs a provided message along with the rule and a specified log level.
    /// </summary>
    /// <typeparam name="TContext">The context type in which the log operation occurs.</typeparam>
    /// <param name="content">The message content to be logged.</param>
    /// <param name="rule">The rule object containing additional error or success information.</param>
    /// <param name="result"></param>
    /// <param name="logLevel">The level of the log (e.g., Information, Warning, Error).</param>
    public void Log<TContext>(string content, IRule rule, IResult result, LogLevel logLevel) { }
}