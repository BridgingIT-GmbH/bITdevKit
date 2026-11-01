// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns selected HTTP executions outside downstream exception handling and authorization.</summary>
/// <example><code>app.UseRouting(); app.UseAuthentication(); app.UseAuditing(); app.UseExceptionHandler(); app.UseAuthorization();</code></example>
public sealed class AuditHttpMiddleware(RequestDelegate next, AuditHttpOptions options, AuditRuntime runtime)
{
    /// <summary>Observes one request, suppressing excluded paths throughout nested work.</summary>
    /// <example><code>await middleware.InvokeAsync(httpContext, audit);</code></example>
    public async Task InvokeAsync(HttpContext context, IAuditContext audit)
    {
        if (context.Features.Get<AuditHttpFeature>() is not null || !audit.IsEnabled)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var path = RequestPathDefaults.GetPath(context);
        if (context.GetEndpoint()?.Metadata.GetMetadata<AuditSuppressionMetadata>() is not null || path.MatchAny(options.PathBlackListPatterns))
        {
            using var suppressed = audit.Suppress();
            await next(context).ConfigureAwait(false);
            return;
        }

        var selected = false;
        try { selected = !options.IsRead(context) || options.SelectBusinessRead?.Invoke(context) == true; }
        catch (Exception) { runtime.Report("AuditHttpSelectionFailed"); }

        if (!selected)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        using var boundary = runtime.DetachHttpExecution();
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        var sanitizedPath = route ?? "/[unmatched]";
        var key = "http:" + context.Request.Method + ":" + (route ?? "unmatched");
        var scope = audit.BeginScope(builder => builder.Key(key.Length <= 512 ? key : "http:request")
            .DisplayName(AuditCollector.Text(context.GetEndpoint()?.DisplayName ?? context.Request.Method + " " + sanitizedPath, 1_024)).Kind("Http"));
        var feature = new AuditHttpFeature(scope);
        context.Features.Set(feature);
        context.Features.Set<IAuditHttpErrorFeature>(feature);
        try
        {
            (scope as IAuditSourceScope)?.SetSource(new() { Feature = "Http", RequestId = context.TraceIdentifier });
            if (scope is AuditScope recording) recording.SetHttp(new() { Method = context.Request.Method, Path = sanitizedPath, Route = route, Endpoint = context.GetEndpoint()?.DisplayName });
            if (options.IncludeAuditIdHeader && scope.IsRecording && !context.Response.HasStarted)
            {
                context.Response.OnStarting(() =>
                {
                    if (scope.AuditId != Guid.Empty) context.Response.Headers["X-Audit-Id"] = scope.AuditId.ToString("D");
                    return Task.CompletedTask;
                });
            }
        }
        catch (Exception) { runtime.Report("AuditHttpMetadataFailed"); }

        try { await next(context).ConfigureAwait(false); }
        catch (Exception error)
        {
            feature.Observe(error);
            if (error is OperationCanceledException && !context.RequestAborted.IsCancellationRequested) scope.SetDetail("http.failureReason", AuditValue.From("UnattributedCancellation"));
            throw;
        }
        finally
        {
            try
            {
                var status = context.Response.StatusCode;
                if (scope is AuditScope finalizing)
                {
                    finalizing.SetHttp(new() { Method = context.Request.Method, Path = sanitizedPath, Route = route, Endpoint = context.GetEndpoint()?.DisplayName, Status = status });
                    finalizing.RefreshActor(context.RequestServices.GetService<ICurrentUserAccessor>());
                }

                scope.Complete(context.RequestAborted.IsCancellationRequested ? AuditOutcome.Canceled
                    : status >= 500 ? AuditOutcome.Failed
                    : feature.KnownRejection || status is >= 400 and < 500 ? AuditOutcome.Rejected
                    : feature.ExceptionObserved ? AuditOutcome.Failed : AuditOutcome.Succeeded);
            }
            catch (Exception) { runtime.Report("AuditHttpFinalizationFailed"); }
            finally
            {
                context.Features.Set<IAuditHttpErrorFeature>(null);
                context.Features.Set<AuditHttpFeature>(null);
                try { await scope.DisposeAsync().ConfigureAwait(false); }
                catch (Exception) { runtime.Report("AuditHttpFinalizationFailed"); }
            }
        }
    }
}
