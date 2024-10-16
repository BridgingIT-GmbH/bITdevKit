// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Diagnostics;
using Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using Serilog.Parsing;

public class RequestLoggingMiddleware
{
    private const string LogKey = "REQ";
    private const string RequestMethodKey = "RequestMethod";
    private const string RequestPathKey = "RequestPath";
    private const string StatusCodeKey = "StatusCode";
    private const string ElapsedKey = "TimeElapsed";
    private const string ClientIpKey = "ClientIP";
    private const string UserAgentKey = "UserAgent";
    private static readonly LogEventProperty[] NoProperties = [];
    private readonly RequestDelegate next;
    private readonly DiagnosticContext diagnosticContext;
    private readonly MessageTemplate messageTemplateStarted;
    private readonly MessageTemplate messageTemplateFinished;
    private readonly Action<IDiagnosticContext, HttpContext> enrichDiagnosticContext;
    private readonly Func<HttpContext, double, Exception, LogEventLevel> getLevel;
    private readonly RequestLoggingOptions options;
    private readonly ILogger logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        DiagnosticContext diagnosticContext = null,
        RequestLoggingOptions options = null)
    {
        this.options ??= new RequestLoggingOptions();

        this.next = next ?? throw new ArgumentNullException(nameof(next));
        this.diagnosticContext = diagnosticContext; // ?? throw new ArgumentNullException(nameof(diagnosticContext));
        this.logger = options.Logger?.ForContext<RequestLoggingMiddleware>();
        this.getLevel = options.GetLevel;
        this.enrichDiagnosticContext = options.EnrichDiagnosticContext;
        this.messageTemplateStarted = new MessageTemplateParser().Parse(options.MessageTemplateStarted);
        this.messageTemplateFinished = new MessageTemplateParser().Parse(options.MessageTemplateFinished);
    }

    public async Task Invoke(HttpContext httpContext)
    {
        EnsureArg.IsNotNull(httpContext, nameof(httpContext));

        if (GetPath(httpContext).EqualsPatternAny(this.options.PathBlackListPatterns))
        {
            await this.next(httpContext); // continue pipeline
        }
        else
        {
            var start = Stopwatch.GetTimestamp();
            var collector = this.diagnosticContext?.BeginCollection();

            try
            {
                if (collector is not null)
                {
                    this.LogStarted(httpContext, collector);

                    await this.next(httpContext); // continue pipeline

                    this.LogFinished(httpContext, collector, httpContext.Response.StatusCode, GetElapsedMilliseconds(start, Stopwatch.GetTimestamp()), null);
                }
                else
                {
                    await this.next(httpContext); // continue pipeline
                }
            }
            catch (Exception ex)
                // Never caught, because `LogFinished()` returns false. This ensures e.g. the developer exception page is still
                // shown, does also mean we see a duplicate "unhandled exception" event from ASP.NET Core.
#pragma warning disable SA1501
                when (this.LogFinished(httpContext, collector, 500, GetElapsedMilliseconds(start, Stopwatch.GetTimestamp()), ex)) { }
#pragma warning restore SA1501
            finally
            {
                collector?.Dispose();
            }
        }
    }

    private static double GetElapsedMilliseconds(long start, long stop)
    {
        return (stop - start) * 1000 / (double)Stopwatch.Frequency;
    }

    private static string GetPath(HttpContext httpContext, bool includeQuery = false)
    {
        var path = includeQuery
            ? httpContext.Features.Get<IHttpRequestFeature>()?.RawTarget
            : httpContext.Features.Get<IHttpRequestFeature>()?.Path;

        if (string.IsNullOrEmpty(path))
        {
            path = httpContext.Request.Path.ToString();
        }

        return path;
    }

    private void LogStarted(HttpContext httpContext, DiagnosticContextCollector collector)
    {
        var logger = this.logger ?? Log.ForContext<RequestLoggingMiddleware>();
        var level = this.getLevel(httpContext, 0, null);

        if (!logger.IsEnabled(level))
        {
            return;
        }

        if (this.diagnosticContext is not null)
        {
            this.enrichDiagnosticContext?.Invoke(this.diagnosticContext, httpContext);
        }

        if (!collector.TryComplete(out var collectedProperties, out var ex))
        {
            collectedProperties = NoProperties;
        }

        var properties = collectedProperties.Concat(
        [
            new LogEventProperty("LogKey", new ScalarValue(LogKey)),
            new LogEventProperty(RequestMethodKey, new ScalarValue(httpContext.Request.Method)),
            new LogEventProperty(RequestPathKey, new ScalarValue(GetPath(httpContext, this.options.IncludeRequestQuery))),
            new LogEventProperty(ClientIpKey, new ScalarValue(httpContext.Connection.RemoteIpAddress?.ToString())),
            new LogEventProperty(UserAgentKey, new ScalarValue(httpContext.Request.Headers["User-Agent"].FirstOrDefault()))
        ]);

        var @event = new LogEvent(DateTimeOffset.Now, level, ex, this.messageTemplateStarted, properties);
        logger.Write(@event);

        //if (this.includeRequestHeaders)
        //{
        //    @event = new LogEvent(DateTimeOffset.Now, level, null, new MessageTemplate($"request: HTTP headers {string.Join("|", httpContext.Response.Headers.Select(h => $"{h.Key}={h.Value}"))}", Enumerable.Empty<MessageTemplateToken>()), properties);
        //    logger.Write(@event);
        //}
    }

    private bool LogFinished(
        HttpContext httpContext,
        DiagnosticContextCollector collector,
        int statusCode,
        double elapsedMs,
        Exception ex)
    {
        var logger = this.logger ?? Log.ForContext<RequestLoggingMiddleware>();
        var level = this.getLevel(httpContext, elapsedMs, ex);

        if (!logger.IsEnabled(level))
        {
            return false;
        }

        if (this.diagnosticContext is not null)
        {
            this.enrichDiagnosticContext?.Invoke(this.diagnosticContext, httpContext);
        }

        if (!collector.TryComplete(out var collectedProperties, out ex))
        {
            collectedProperties = NoProperties;
        }

        var properties = collectedProperties.Concat(
        [
            new LogEventProperty("LogKey", new ScalarValue(LogKey)),
            new LogEventProperty(RequestMethodKey, new ScalarValue(httpContext.Request.Method)),
            new LogEventProperty(RequestPathKey, new ScalarValue(GetPath(httpContext, this.options.IncludeRequestQuery))),
            new LogEventProperty(StatusCodeKey, new ScalarValue(statusCode)),
            new LogEventProperty(ElapsedKey, new ScalarValue(elapsedMs)),
            new LogEventProperty(ClientIpKey, new ScalarValue(httpContext.Connection.RemoteIpAddress?.ToString())),
            new LogEventProperty(UserAgentKey, new ScalarValue(httpContext.Request.Headers["User-Agent"].FirstOrDefault()))
        ]);

        var @event = new LogEvent(DateTimeOffset.Now, level, ex, this.messageTemplateFinished, properties);
        logger.Write(@event);

        return false;
    }
}