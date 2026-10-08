// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Owns one full downstream HTTP observation outside the host's error handler and response processors.</summary>
/// <example><code>app.UseRequestProfiling(); app.UseExceptionHandler(); app.UseRequestProfilingExceptionObserver(); app.UseRouting();</code></example>
public sealed class RequestProfilingMiddleware(RequestDelegate next, RequestProfilingRuntime runtime = null,
    IOperationProfiler profiler = null, IProfilingNodeIdentityProvider nodes = null, TimeProvider timeProvider = null)
{
    /// <summary>Gets the opaque full-GUID response header used for exact retained-record lookup.</summary>
    /// <example><code>var id = response.Headers[RequestProfilingMiddleware.HeaderName];</code></example>
    public const string HeaderName = "X-Request-Profiling-Id";

    /// <summary>Observes one request without buffering, retrying, canceling or modifying business work.</summary>
    /// <example>Invoked by UseRequestProfiling in the ASP.NET Core pipeline.</example>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Features.Get<IRequestProfilingFeature>() is not null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var clock = timeProvider ?? TimeProvider.System;
        var entry = clock.GetTimestamp();
        var utc = clock.GetUtcNow();
        RequestProfilingFeature feature = null;
        context.Response.OnStarting(() =>
        {
            feature?.ResponseStarting();
            if (feature?.Selected == true && feature.Id != Guid.Empty) { context.Response.Headers[HeaderName] = feature.Id.ToString("D"); }
            else { context.Response.Headers.Remove(HeaderName); }

            return Task.CompletedTask;
        });
        if (runtime?.Enabled != true || profiler is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var path = context.Request.PathBase.Add(context.Request.Path).Value;
        path = string.IsNullOrEmpty(path) ? "/" : path;
        var blacklisted = runtime.Matcher.IsMatch(path);
        var (key, shortened) = blacklisted ? ((string)null, false) : RequestProfilingPathMatcher.CreateKey(path, runtime.Options.StripPathPrefix, runtime.MaximumKeyLength);
        var decision = blacklisted ? new RequestProfilingSamplingDecision(false, "Blacklisted")
            : runtime.Decide(new(context.Request.Method, path, key, nodes.GetNode(), utc));
        if (blacklisted) { runtime.Exclude(); }

        var selected = decision.Capture;
        using var boundary = profiler.BeginExecutionBoundary();
        using var suppression = selected ? null : profiler.Suppress();
        var activeSelected = selected ? runtime.EnterSelected() : (int?)null;
        var operation = selected ? profiler.BeginOperation(new OperationProfilingStartRequest
        {
            Key = key, Kind = OperationProfilingKind.HttpRequest.ToString(), CorrelationId = context.TraceIdentifier,
            EntryTimestamp = entry, EntryUtc = utc,
        }) : null;
        feature = new(context, operation, runtime, new HttpRequestProfilingMetadata
        {
            ApplicationRequestId = context.TraceIdentifier, Method = context.Request.Method, Path = path,
            DeclaredRequestBytes = context.Request.ContentLength, PathKeyShortened = shortened,
            ActiveSelectedRequestsAtEntry = activeSelected,
            SamplingStrategyKey = runtime.Options.StrategyKey, SamplingConfigurationKey = runtime.Options.ConfigurationKey,
            SamplingInclusionProbability = decision.InclusionProbability,
        }, selected, key);
        // Preserve the exact sampling decision metadata, without a second policy invocation.
        context.Features.Set<IRequestProfilingFeature>(feature);
        if (selected)
        {
            var response = new ProfilingResponseBodyFeature(context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>(), feature.ObserveResponse);
            context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>(response);
            var request = runtime.Options.ObserveRequestBodyBytes ? new ProfilingRequestBodyObserver(context, feature.ObserveRequest) : null;
            feature.Attach(response, request);
        }

        context.Response.OnCompleted(feature.CompleteAsync);
        var escaped = true;
        try
        {
            await next(context).ConfigureAwait(false);
            escaped = false;
        }
        catch (Exception exception)
        {
            feature.ReportException(exception);
            throw;
        }
        finally
        {
            feature.PipelineUnwound(escaped);
        }
    }
}
