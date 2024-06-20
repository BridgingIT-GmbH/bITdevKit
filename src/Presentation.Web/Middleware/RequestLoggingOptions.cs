// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;

/// <summary>
/// Options for the <see cref="RequestLoggingMiddleware"/>.
/// </summary>
public class RequestLoggingOptions
{
    private const string DefaultRequestStartedMessageTemplate =
        "{LogKey} starting HTTP {RequestMethod} {RequestPath}";

    private const string DefaultRequestFinishedMessageTemplate =
        "{LogKey} finished HTTP {RequestMethod} {RequestPath} [{StatusCode}] -> took {TimeElapsed:0.0000} ms";

    public RequestLoggingOptions()
    {
        this.GetLevel = DefaultGetLevel;
        this.MessageTemplateStarted = DefaultRequestStartedMessageTemplate;
        this.MessageTemplateFinished = DefaultRequestFinishedMessageTemplate;
    }

    /// <summary>
    /// Gets or sets the message template.
    /// </summary>
    public string MessageTemplateStarted { get; set; }

    /// <summary>
    /// Gets or sets the message template.
    /// </summary>
    public string MessageTemplateFinished { get; set; }

    /// <summary>
    /// A function returning the <see cref="LogEventLevel"/> based on the <see cref="HttpContext"/>
    /// </summary>
    public Func<HttpContext, double, Exception, LogEventLevel> GetLevel { get; set; }

    /// <summary>
    /// Callback that can be used to set additional properties on the request completion event.
    /// </summary>
    public Action<IDiagnosticContext, HttpContext> EnrichDiagnosticContext { get; set; }

    /// <summary>
    /// The logger through which request completion events will be logged.
    /// </summary>
    public ILogger Logger { get; set; }

    /// <summary>
    /// Include the full URL query string in the <c>RequestPath</c> property
    /// that is attached to request log events.
    /// </summary>
    public bool IncludeRequestQuery { get; set; }

    public bool IncludeRequestHeaders { get; set; }

    /// <summary>
    /// The path patterns to ignore.
    /// </summary>
    public string[] PathBlackListPatterns { get; set; } =
        ["/*.js", "/*.css", "/*.map", "/*.html", "/swagger*", "/favicon.ico", "/_framework*", "/_vs*", "/health*", "/notificationhub*", "/_content*", "/signalrhub*"];

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
    private static LogEventLevel DefaultGetLevel(HttpContext ctx, double _, Exception ex) =>
    ex is not null
            ? LogEventLevel.Error
            : ctx.Response.StatusCode > 499
                ? LogEventLevel.Error
                : LogEventLevel.Information;
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
}

/// <summary>
/// Options for the <see cref="IExceptionHandler"/>.
/// </summary>
public class GlobalExceptionHandlerOptions : Microsoft.AspNetCore.Builder.ExceptionHandlerOptions
{
    /// <summary>
    /// Gets or sets if exception details are included.
    /// </summary>
    public bool IncludeExceptionDetails { get; set; } = true;

    /// <summary>
    /// Gets or sets if exception should be logged
    /// </summary>
    public bool EnableLogging { get; set; } = false;
}
