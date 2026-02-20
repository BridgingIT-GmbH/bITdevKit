// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler for development environments that provides detailed
///     debugging information including full stack traces, inner exceptions,
///     and exception properties.
/// </summary>
/// <remarks>
///     This handler should only be registered in development environments
///     to avoid exposing sensitive information in production.
///     It catches all exceptions and provides comprehensive debugging details.
/// </remarks>
public class DebugExceptionHandler(
    ILogger<DebugExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<Exception>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status500InternalServerError;

    /// <inheritdoc />
    protected override string Title => "Debug Information";

    /// <inheritdoc />
    protected override string GetDetail(Exception exception)
    {
        return exception.Message;
    }

    /// <inheritdoc />
    protected override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        Exception exception)
    {
        var problemDetails = base.CreateProblemDetails(httpContext, exception);

        // Add comprehensive debugging information
        problemDetails.Extensions["debugInfo"] = new
        {
            ExceptionType = exception.GetType().FullName,
            StackTrace = exception.StackTrace?.Split(Environment.NewLine),
            InnerExceptions = this.BuildInnerExceptionChain(exception),
            ExceptionProperties = this.ExtractExceptionProperties(exception),
            Timestamp = DateTimeOffset.UtcNow,
            RequestInfo = new
            {
                Method = httpContext.Request.Method,
                Path = httpContext.Request.Path.Value,
                QueryString = httpContext.Request.QueryString.Value,
                ContentType = httpContext.Request.ContentType,
                Headers = this.SanitizeHeaders(httpContext.Request.Headers)
            },
            ResponseInfo = new
            {
                StatusCode = httpContext.Response.StatusCode,
                Headers = this.SanitizeHeaders(httpContext.Response.Headers)
            }
        };

        return problemDetails;
    }

    /// <inheritdoc />
    protected override void LogException(Exception exception)
    {
        this.Logger?.LogError(
            exception,
            "DEBUG: Unhandled exception of type {ExceptionType}: {ExceptionMessage}\n" +
            "Stack Trace: {StackTrace}\n" +
            "Inner Exception: {InnerException}",
            exception.GetType().FullName,
            exception.Message,
            exception.StackTrace,
            exception.InnerException?.Message ?? "None");
    }

    /// <summary>
    ///     Builds a chain of inner exceptions for detailed debugging.
    /// </summary>
    private object[] BuildInnerExceptionChain(Exception exception)
    {
        var chain = new List<object>();
        var current = exception.InnerException;
        var depth = 0;
        const int maxDepth = 5; // Prevent infinite loops

        while (current is not null && depth < maxDepth)
        {
            chain.Add(new
            {
                Depth = depth + 1,
                ExceptionType = current.GetType().FullName,
                Message = current.Message,
                StackTrace = current.StackTrace?.Split(Environment.NewLine),
                Properties = this.ExtractExceptionProperties(current)
            });

            current = current.InnerException;
            depth++;
        }

        return chain.ToArray();
    }

    /// <summary>
    ///     Extracts custom properties from the exception for debugging.
    /// </summary>
    private Dictionary<string, object> ExtractExceptionProperties(Exception exception)
    {
        var properties = new Dictionary<string, object>();

        var exceptionType = exception.GetType();
        var publicProperties = exceptionType.GetProperties(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        foreach (var prop in publicProperties)
        {
            // Skip standard exception properties (already included elsewhere)
            if (prop.Name is "Message" or "StackTrace" or "InnerException" or "Source" or "TargetSite" or "Data" or "HelpLink")
            {
                continue;
            }

            try
            {
                var value = prop.GetValue(exception);

                // Only include serializable values
                if (value is null or string or int or long or double or bool or decimal or DateTime or DateTimeOffset or Enum)
                {
                    properties[prop.Name] = value ?? "null";
                }
                else if (value is System.Collections.IEnumerable enumerable && prop.Name != "Message")
                {
                    properties[prop.Name] = string.Join(", ", enumerable.Cast<object>().Take(10));
                }
                else
                {
                    properties[prop.Name] = value?.ToString() ?? "null";
                }
            }
            catch
            {
                // Skip properties that throw on access
                properties[prop.Name] = "[Error accessing property]";
            }
        }

        return properties;
    }

    /// <summary>
    ///     Sanitizes sensitive headers from the response for security.
    /// </summary>
    private Dictionary<string, object> SanitizeHeaders(IHeaderDictionary headers)
    {
        var sensitiveHeaders = new[]
        {
            "Authorization",
            "X-API-Key",
            "X-Token",
            "Cookie",
            "Set-Cookie",
            "X-CSRF-Token",
            "X-Client-Secret"
        };

        return headers
            .Where(h => !sensitiveHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(
                h => h.Key,
                h => (object)string.Join("; ", h.Value),
                StringComparer.OrdinalIgnoreCase);
    }
}