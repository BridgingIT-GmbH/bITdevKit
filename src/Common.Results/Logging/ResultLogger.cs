// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>
/// Provides functionality to log results with a specified logging provider.
/// </summary>
public class ResultLogger(ILogger<ResultLogger> logger) : IResultLogger
{
    /// <summary>
    /// An instance of the ILogger interface specifically designated for logging results in the application.
    /// This logger is utilized to record the outcome of operations, the context in which they occur,
    /// and associated log levels.
    /// </summary>
    private readonly ILogger<ResultLogger> logger = logger;

    /// <summary>
    /// Logs the result of an operation with a specified context, content, and log level.
    /// </summary>
    /// <param name="context">The context in which the operation occurred.</param>
    /// <param name="message">The message or main information of the result.</param>
    /// <param name="result">The result object which contains success status and error messages.</param>
    /// <param name="logLevel">The severity level of the log entry.</param>
    public void Log(string context, string message, Result result, LogLevel logLevel = LogLevel.Trace)
    {
        //var errors = result.Errors.Select(e => $"[{e.GetType().Namespace}] {e.Message}");
        context = context.IsNullOrEmpty() ? message : $"<{context}> {message}".Trim();

        result.Log(this.logger, context, logLevel);
        //this.logger.Log(logLevel,
        //    "{LogKey} result: {ResultSuccess} | {ResultMessages} | {ResultContent} <{ResultContext}>",
        //    "RES",
        //    result.IsSuccess ? "Success" : "Failure",
        //    $"{result.Messages.Concat(errors).ToString(", ")}",
        //    content,
        //    context);
    }

    /// <summary>
    /// Logs a result message with associated content and logging level.
    /// </summary>
    /// <typeparam name="TContext">The type of the context in which the log entry is created.</typeparam>
    /// <param name="content">The content to be logged.</param>
    /// <param name="result">The result object containing status and messages.</param>
    /// <param name="logLevel">The severity level of the log entry.</param>
    public void Log<TContext>(string content, Result result, LogLevel logLevel = LogLevel.Trace)
    {
        //var errors = result.Errors.Select(e => $"[{e.GetType().Namespace}] {e.Message}");

        result.Log(this.logger, $"<{typeof(TContext).Name}> {content}".Trim(), logLevel);
        //result.Log(this.logger, $"<{typeof(TContext).Name}> {content}".Trim());

        //this.logger.Log(logLevel,
        //    "{LogKey} result: {ResultSuccess} | {ResultMessages} | {ResultContent} <{ResultContext}>",
        //    "RES",
        //    result.IsSuccess ? "Success" : "Failure",
        //    $"{result.Messages.Concat(errors).ToString(", ")}",
        //    content,
        //    typeof(TContext).Name);
    }
}