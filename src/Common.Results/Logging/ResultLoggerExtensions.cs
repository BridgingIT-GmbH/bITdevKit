// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>
/// Provides extension methods for logging the results of operations.
/// </summary>
public static class ResultLoggerExtensions
{
    /// <summary>
    /// Logs a result with optional context and content at the specified log level.
    /// </summary>
    /// <param name="result">The result to be logged.</param>
    /// <param name="content">Optional content to include in the log. Default is null.</param>
    /// <param name="logLevel">The level at which to log the result. Default is LogLevel.Debug.</param>
    /// <returns>The same result that was passed in, allowing for method chaining.</returns>
    public static Result Log(this Result result, string content = null, LogLevel logLevel = LogLevel.Debug)
    {
        return result.Log(null, content, logLevel);
    }

    /// <summary>
    /// Logs the result with the given context, content, and log level.
    /// </summary>
    /// <param name="result">The result to be logged.</param>
    /// <param name="context">A string providing context for the log. Can be null.</param>
    /// <param name="content">Log content or message. Can be null.</param>
    /// <param name="logLevel">The level of the log entry (e.g., Debug, Information, Warning, Error).</param>
    /// <returns>Returns the original result after logging.</returns>
    public static Result Log(this Result result, string context, string content = null, LogLevel logLevel = LogLevel.Debug)
    {
        Result.Settings.Logger?.Log(context, content, result, logLevel);

        return result;
    }

    /// <summary>
    /// Logs the result if it is successful.
    /// </summary>
    /// <param name="result">The Result object to be checked and logged if successful.</param>
    /// <param name="content">Optional content to include in the log message. Default is null.</param>
    /// <param name="logLevel">The logging level to use. Default is LogLevel.Debug.</param>
    /// <returns>The original Result object to allow for method chaining.</returns>
    public static Result LogIfSuccess(this Result result, string content = null, LogLevel logLevel = LogLevel.Debug)
    {
        return result.LogIfSuccess(null, content, logLevel);
    }

    /// <summary>
    /// Logs a message if the result indicates a success.
    /// </summary>
    /// <param name="result">The result to check and potentially log.</param>
    /// <param name="context">The context of the log message.</param>
    /// <param name="content">The content of the log message.</param>
    /// <param name="logLevel">The log level to use. Default is LogLevel.Debug.</param>
    /// <returns>The original result, allowing for method chaining.</returns>
    public static Result LogIfSuccess(this Result result, string context, string content, LogLevel logLevel = LogLevel.Debug)
    {
        if (result.IsSuccess)
        {
            result.Log(context, content, logLevel);
        }

        return result;
    }

    /// <summary>
    /// Logs the result if it indicates a failure.
    /// </summary>
    /// <param name="result">The result to be checked and potentially logged.</param>
    /// <param name="content">Optional content to be included in the log entry.</param>
    /// <param name="logLevel">The log level to be used. Defaults to <see cref="LogLevel.Debug"/>.</param>
    /// <return>The original result for method chaining.</return>
    public static Result LogIfFailure(this Result result, string content = null, LogLevel logLevel = LogLevel.Debug)
    {
        return result.LogIfFailure(null, content, logLevel);
    }

    /// <summary>
    /// Logs the specified content if the result represents a failure.
    /// </summary>
    /// <param name="result">The result to check for failure.</param>
    /// <param name="context">Optional context information to include in the log.</param>
    /// <param name="content">Optional content to be logged.</param>
    /// <param name="logLevel">The log level to use for the failure log entry. Default is Debug.</param>
    /// <returns>The original result, unmodified.</returns>
    public static Result LogIfFailure(this Result result, string context, string content = null, LogLevel logLevel = LogLevel.Debug)
    {
        if (result.IsFailure)
        {
            result.Log(context, content, logLevel);
        }

        return result;
    }

    /// <summary>
    /// Logs the result using the provided context type.
    /// </summary>
    /// <typeparam name="TContext">The type of the context to be used for logging.</typeparam>
    /// <param name="result">The result to be logged.</param>
    /// <param name="logLevel">The log level to be used. Defaults to <see cref="LogLevel.Debug"/>.</param>
    /// <returns>The original result, potentially allowing for further method chaining.</returns>
    public static Result Log<TContext>(this Result result, LogLevel logLevel = LogLevel.Debug)
    {
        return result.Log<TContext>(null, logLevel);
    }

    /// <summary>
    /// Logs the result with specified log level.
    /// </summary>
    /// <typeparam name="TContext">The context type used for logging.</typeparam>
    /// <param name="result">The result to be logged.</param>
    /// <param name="logLevel">The severity level of the log. Default is <see cref="LogLevel.Debug"/>.</param>
    /// <returns>The original result after logging.</returns>
    public static Result Log<TContext>(this Result result, string content, LogLevel logLevel = LogLevel.Debug)
    {
        Result.Settings.Logger?.Log<TContext>(content, result, logLevel);

        return result;
    }
}