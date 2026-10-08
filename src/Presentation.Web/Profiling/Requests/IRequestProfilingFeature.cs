// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>Preserves one request-entry decision and explicit recording handle across re-execution and DI scopes.</summary>
/// <example><code>httpContext.Features.Get&lt;IRequestProfilingFeature&gt;()?.ReportException(exception);</code></example>
public interface IRequestProfilingFeature
{
    /// <summary>Gets the selected occurrence ID, including capture admission rejection.</summary>
    /// <example><code>var id = context.Features.Get&lt;IRequestProfilingFeature&gt;()?.Id;</code></example>
    Guid Id { get; }
    /// <summary>Gets whether the entry sampling policy selected this request.</summary>
    /// <example><code>var selected = feature.Selected;</code></example>
    bool Selected { get; }
    /// <summary>Gets the bounded default key chosen at entry, unaffected by controller overrides.</summary>
    /// <example><code>var initial = feature.InitialKey;</code></example>
    string InitialKey { get; }
    /// <summary>Gets whether nested instrumentation is suppressed by a blacklist or sampling skip.</summary>
    /// <example><code>var suppressed = feature.Suppressed;</code></example>
    bool Suppressed { get; }
    /// <summary>Gets the explicit root handle, independent from the ambient execution or DI scope.</summary>
    /// <example><code>feature.Operation?.SetKey("Products.List");</code></example>
    IProfilingOperationScope Operation { get; }
    /// <summary>Safely observes an exception before a consuming handler or filter replaces its response.</summary>
    /// <example><code>feature?.ReportException(exception);</code></example>
    void ReportException(Exception exception);
    /// <summary>Captures the original selected route once, including an unmatched null route.</summary>
    /// <example><code>feature?.CaptureOriginalRoute();</code></example>
    void CaptureOriginalRoute();
}

/// <summary>Coordinates HTTP observations and exactly-once finalization without retaining application payloads.</summary>
/// <example>Installed by RequestProfilingMiddleware, reused by exception observers and consuming filters.</example>
public sealed class RequestProfilingFeature : IRequestProfilingFeature
{
    private readonly object sync = new();
    private HttpContext context;
    private readonly RequestProfilingRuntime runtime;
    private HttpRequestProfilingMetadata metadata;
    private bool routeObserved;
    private bool unwound;
    private bool aborted;
    private bool applicationCanceled;
    private bool finalized;
    private bool responsePartial;
    private bool requestPartial;
    private long responseBytes;
    private long requestBytes;
    private CancellationTokenRegistration abortRegistration;
    private ProfilingResponseBodyFeature responseObserver;
    private ProfilingRequestBodyObserver requestObserver;

    /// <summary>Creates the selected or suppressed request feature from immutable entry metadata.</summary>
    /// <example>Created once at HTTP middleware entry.</example>
    public RequestProfilingFeature(HttpContext context, IProfilingOperationScope operation, RequestProfilingRuntime runtime,
        HttpRequestProfilingMetadata metadata, bool selected, string initialKey)
    {
        this.context = context;
        this.Operation = operation;
        this.runtime = runtime;
        this.metadata = metadata;
        this.Selected = selected;
        this.InitialKey = initialKey;
        this.Publish();
    }

    /// <inheritdoc />
    public Guid Id => this.Operation?.Id ?? Guid.Empty;
    /// <inheritdoc />
    public bool Selected { get; }
    /// <inheritdoc />
    public string InitialKey { get; }
    /// <inheritdoc />
    public bool Suppressed => !this.Selected;
    /// <inheritdoc />
    public IProfilingOperationScope Operation { get; }

    /// <summary>Attaches observers and an abort notification without changing transport ownership.</summary>
    /// <example>Called once after installing the response and optional request adapters.</example>
    public void Attach(ProfilingResponseBodyFeature response, ProfilingRequestBodyObserver request)
    {
        this.responseObserver = response;
        this.requestObserver = request;
        this.abortRegistration = this.context.RequestAborted.Register(this.MarkAborted);
    }

    /// <inheritdoc />
    public void CaptureOriginalRoute()
    {
        lock (this.sync)
        {
            if (this.finalized || this.routeObserved) { return; }

            this.routeObserved = true;
            this.metadata = this.metadata with { Route = (this.context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText };
            this.Publish();
        }
    }

    /// <inheritdoc />
    public void ReportException(Exception exception)
    {
        try
        {
            lock (this.sync)
            {
                if (this.finalized) { return; }

                this.CaptureOriginalRoute();
                if (exception is OperationCanceledException canceled && canceled.CancellationToken.IsCancellationRequested)
                {
                    this.applicationCanceled = true;
                    this.Operation?.Cancel();
                }
                else
                {
                    this.Operation?.Fail(exception);
                    this.metadata = this.metadata with { HandledException = true };
                }

                this.Publish();
            }
        }
        catch (Exception)
        {
            // Observation cannot replace the exception or response selected by the application.
        }
    }

    /// <summary>Records a successful response write or uncertain partial delivery; false detaches expired observation.</summary>
    /// <example>Used by Stream, BodyWriter and send-file adapters after successful forwarding.</example>
    public bool ObserveResponse(long bytes, bool partial) => this.ObserveBytes(bytes, partial, false);
    /// <summary>Records ordinarily consumed request bytes; false detaches expired observation.</summary>
    /// <example>Used by the optional request Stream and PipeReader adapters.</example>
    public bool ObserveRequest(long bytes, bool partial) => this.ObserveBytes(bytes, partial, true);

    private bool ObserveBytes(long bytes, bool partial, bool request)
    {
        try
        {
            lock (this.sync)
            {
                if (this.finalized || this.Operation?.IsRecording != true) { return false; }

                if (request) { this.requestBytes = checked(this.requestBytes + bytes); this.requestPartial |= partial; }
                else { this.responseBytes = checked(this.responseBytes + bytes); this.responsePartial |= partial; }

                this.Publish();
                return true;
            }
        }
        catch (Exception) { return false; }
    }

    /// <summary>Restores pipeline ownership externally, retaining only the explicit handle for completion.</summary>
    /// <example>Called in the outer middleware finally block after downstream unwinds.</example>
    public void PipelineUnwound(bool escaped)
    {
        lock (this.sync)
        {
            this.unwound = true;
            this.CaptureOriginalRoute();
            if (escaped || this.aborted) { this.Finish(false); }
        }
    }

    /// <summary>Finalizes normal response processing once, including final status and quality.</summary>
    /// <example>Registered with HttpResponse.OnCompleted by the outer middleware.</example>
    public Task CompleteAsync()
    {
        lock (this.sync) { this.Finish(true); }

        return Task.CompletedTask;
    }

    /// <summary>Updates header-time metadata before the transport begins sending the selected response.</summary>
    /// <example>Called by the outer OnStarting callback after inner cache callbacks.</example>
    public void ResponseStarting()
    {
        lock (this.sync)
        {
            if (this.finalized) { return; }

            this.CaptureOriginalRoute();
            this.metadata = this.metadata with { StatusCode = this.context.Response.StatusCode, DeclaredResponseBytes = this.context.Response.ContentLength };
            this.Publish();
        }
    }

    private void MarkAborted()
    {
        lock (this.sync)
        {
            if (this.finalized) { return; }

            this.aborted = true;
            Safe(() => this.Operation?.Abort());
            this.metadata = this.metadata with { TransportAborted = true };
            this.Publish();
            if (this.unwound) { this.Finish(false); }
        }
    }

    private static void Safe(Action action)
    {
        try { action(); }
        catch (Exception) { /* Instrumentation never replaces application or transport behavior. */ }
    }

    private void Publish()
    {
        Safe(() => this.Operation?.SetHttpMetadata(this.metadata with
        {
            ResponseBytes = this.Selected ? this.responseBytes : null,
            ResponseBytesQuality = this.Selected ? ProfilingObservationQuality.Partial : ProfilingObservationQuality.Unavailable,
            RequestBytes = this.runtime.Options.ObserveRequestBodyBytes && this.Selected ? this.requestBytes : null,
            RequestBytesQuality = this.runtime.Options.ObserveRequestBodyBytes && this.Selected ? ProfilingObservationQuality.Partial : ProfilingObservationQuality.Unavailable,
        }));
    }

    private void Finish(bool normal)
    {
        if (this.finalized) { return; }

        this.finalized = true;
        var http = this.context;
        try
        {
            var coveredResponse = this.responseObserver?.IsInstalled(http) == true;
            var coveredRequest = this.requestObserver?.IsInstalled(http) == true;
            Safe(() => this.Operation?.SetHttpMetadata(this.metadata with
            {
                StatusCode = normal || http.Response.HasStarted ? http.Response.StatusCode : this.metadata.StatusCode,
                DeclaredResponseBytes = http.Response.ContentLength,
                TransportAborted = this.aborted,
                ResponseBytes = this.Selected ? this.responseBytes : null,
                ResponseBytesQuality = normal && !this.aborted && !this.responsePartial && coveredResponse ? ProfilingObservationQuality.Complete : ProfilingObservationQuality.Partial,
                RequestBytes = this.runtime.Options.ObserveRequestBodyBytes ? this.requestBytes : null,
                RequestBytesQuality = !this.runtime.Options.ObserveRequestBodyBytes ? ProfilingObservationQuality.Unavailable
                    : normal && !this.aborted && !this.requestPartial && coveredRequest ? ProfilingObservationQuality.Complete : ProfilingObservationQuality.Partial,
            }));
            if (this.aborted) { Safe(() => this.Operation?.Abort()); }
            else if (http.Response.StatusCode >= 500 && !this.applicationCanceled) { Safe(() => this.Operation?.Fail(new ProfilingFailureDescriptor { Source = "Http", Code = "ServerError" })); }
            else if (normal) { Safe(() => this.Operation?.Complete()); }
        }
        finally
        {
            Safe(() => this.Operation?.Dispose());
            // Release diagnostic callbacks, never dispose the live transport.
            this.responseObserver?.Detach();
            this.requestObserver?.Detach();
            this.responseObserver = null;
            this.requestObserver = null;
            this.context = null;
            this.abortRegistration.Unregister();
            if (this.Selected) { this.runtime.LeaveSelected(); }
        }
    }
}
