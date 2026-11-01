// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using System.Text.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.AspNetCore.Http;
using IResult = Microsoft.AspNetCore.Http.IResult;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Shared bounded binding and safe results used by the optional API and dashboard.</summary>
/// <example><code>var filter = await AuditHttpHandlers.ReadFilterAsync(context);</code></example>
public static class AuditHttpHandlers
{
    /// <summary>Bounds and validates a direct JSON FilterModel before invoking the standard body binder. Parser internals are not echoed.</summary>
    /// <example><code>var filter = await AuditHttpHandlers.ReadFilterAsync(http);</code></example>
    public static async Task<FilterModel> ReadFilterAsync(HttpContext context)
    {
        if (context.Request.ContentLength > 32_768) throw new AuditExportException("AuditFilterTooLarge");
        using var bounded = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false)) != 0)
        {
            if (bounded.Length + read > 32_768) throw new AuditExportException("AuditFilterTooLarge");
            bounded.Write(buffer, 0, read);
        }

        if (bounded.Length == 0) return new();
        var approved = AuditFilterJson.Parse(bounded.GetBuffer().AsMemory(0, (int)bounded.Length));
        if (approved.IsFailure) throw new AuditExportException("AuditFilterInvalid");
        bounded.Position = 0;
        var original = context.Request.Body;
        try
        {
            context.Request.Body = bounded;
            return await context.FromBodyFilterAsync(enableBuffering: true, jsonOptions: AuditFilterJson.CreateOptions()).ConfigureAwait(false) ?? throw new AuditExportException("AuditFilterInvalid");
        }
        finally { context.Request.Body = original; }
    }

    /// <summary>Reads a bounded pure diagnostics probe without binding a business payload.</summary>
    /// <example><code>var probe = await AuditHttpHandlers.ReadProbeAsync(http);</code></example>
    public static async Task<AuditCaptureProbe> ReadProbeAsync(HttpContext context)
    {
        if (context.Request.ContentLength > 4096) throw new AuditExportException("AuditProbeInvalid");
        using var body = new MemoryStream(); var buffer = new byte[512];
        int read;
        while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false)) > 0)
        {
            if (body.Length + read > 4096) throw new AuditExportException("AuditProbeInvalid");
            body.Write(buffer, 0, read);
        }

        var options = AuditExportJson.CreateOptions(); options.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
        return JsonSerializer.Deserialize<AuditCaptureProbe>(body.ToArray(), options) ?? throw new AuditExportException("AuditProbeInvalid");
    }

    /// <summary>Reads a bounded standard query filter.</summary>
    /// <example><code>var filter = AuditHttpHandlers.QueryFilter(http);</code></example>
    public static FilterModel QueryFilter(HttpContext context)
    {
        if (context.Request.QueryString.Value?.Length > 32_768) throw new AuditExportException("AuditFilterTooLarge");
        var json = context.Request.Query["filter"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(json)) return new();
        var approved = AuditFilterJson.Parse(System.Text.Encoding.UTF8.GetBytes(json));
        return approved.IsSuccess ? approved.Value : throw new AuditExportException("AuditFilterInvalid");
    }

    /// <summary>Opens a scoped export and returns a native attachment result with companion metadata.</summary>
    /// <example><code>return await AuditHttpHandlers.ExportAsync(http, filter, runtime);</code></example>
    public static async Task<IResult> ExportAsync(HttpContext context, FilterModel filter, AuditWebRuntime runtime, Guid? exportId = null)
    {
        var principal = AuditExportTokenProtector.PrincipalFingerprint(context.User);
        AuditExportResume resume = null;
        var continuation = context.Request.Query["continuationToken"].ToString(); var checkpoint = context.Request.Query["checkpoint"].ToString();
        if (continuation.Length > 0 && checkpoint.Length > 0) return Error("AuditExportProgressInvalid");
        if (continuation.Length > 0 || checkpoint.Length > 0)
        {
            var opened = runtime.Tokens.Unprotect(continuation.Length > 0 ? continuation : checkpoint, principal);
            if (opened.IsFailure || opened.Value.IsCheckpoint != (checkpoint.Length > 0)) return Error("AuditExportProgressInvalid");
            resume = opened.Value;
        }

        var result = await context.RequestServices.GetRequiredService<IAuditExportService>().OpenAsync(new() { ExportId = exportId, Filter = filter, Format = context.Request.Query["format"].FirstOrDefault() ?? "ndjson", Resume = resume }, context.RequestAborted).ConfigureAwait(false);
        if (result.IsFailure) return Failure(result.Errors);
        var session = result.Value;
        if (!runtime.Attempts.TryStart(session.Metadata, principal)) { await session.DisposeAsync().ConfigureAwait(false); return Error("AuditExportBusy"); }

        return new AuditExportHttpResult(session, runtime, principal);
    }

    /// <summary>Returns only current-principal/current-scope metadata; revalidates original lifecycle before exposing progress.</summary>
    /// <example><code>return await AuditHttpHandlers.StatusAsync(http, id, runtime);</code></example>
    public static async Task<IResult> StatusAsync(HttpContext context, Guid id, AuditWebRuntime runtime)
    {
        context.Response.Headers.CacheControl = "no-store";
        var plan = await context.RequestServices.GetRequiredService<IAuditQueryService>().PrepareAsync(null, context.RequestAborted).ConfigureAwait(false);
        if (plan.IsFailure) return Failure(plan.Errors);
        var completion = runtime.Attempts.Find(id, AuditExportTokenProtector.PrincipalFingerprint(context.User), plan.Value.ScopeFingerprint);
        if (completion is null) return Results.NotFound();
        if (completion.OutputConfirmed && completion.Progress is { } progress)
        {
            var registry = context.RequestServices.GetRequiredService<AuditExportRegistry>();
            var formatter = registry.Find(completion.Metadata.Format);
            var exports = context.RequestServices.GetRequiredService<IAuditExportService>() as AuditExportService;
            if (formatter.IsFailure || exports is null || exports.ExposureFingerprint(formatter.Value) != completion.Metadata.ExposureFingerprint) return Error("AuditExportProgressInvalid");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted); timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var admission = context.RequestServices.GetRequiredService<AuditExportLimiter>().TryAcquire();
            if (admission is null) return Error("AuditExportBusy");
            Task<Result<AuditExtractionBatch>> reading = null;
            Result<AuditExtractionBatch> validation;
            try
            {
                reading = context.RequestServices.GetRequiredService<IAuditingStorageProvider>().ReadExtractionAsync(new()
            {
                StoreId = progress.StoreId, StoreEpoch = progress.StoreEpoch, AfterPosition = progress.UpperPosition, UpperPosition = progress.UpperPosition,
                RequiredAfterPosition = progress.ChunkLowerPosition, AvailabilityBaseline = progress.AvailabilityBaseline, LifecycleGeneration = progress.LifecycleGeneration,
                Selection = plan.Value with { Lifecycle = completion.Metadata.Lifecycle, RequiresStableLifecycle = completion.Metadata.RequiresStableLifecycle }, BatchSize = 1
                }, timeout.Token);
                validation = await reading.WaitAsync(timeout.Token).ConfigureAwait(false);
            }
            finally
            {
                if (reading is { IsCompleted: false }) _ = reading.ContinueWith(task => { _ = task.Exception; admission.Dispose(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                else { _ = reading?.Exception; admission.Dispose(); }
            }

            if (validation.IsFailure) return Error("AuditExportWindowInvalid");
        }

        return Results.Json(completion, AuditExportJson.CreateOptions());
    }

    /// <summary>Maps typed audit failures without exposing exception messages or source values.</summary>
    /// <example><code>return AuditHttpHandlers.Failure(result.Errors);</code></example>
    public static IResult Failure(IEnumerable<IResultError> errors) => Error(errors.OfType<AuditError>().FirstOrDefault()?.Code ?? "AuditReadUnavailable");

    /// <summary>Returns a safe code and appropriate non-success status.</summary>
    /// <example><code>return AuditHttpHandlers.Error("AuditExportBusy");</code></example>
    public static IResult Error(string code) => Results.Json(new { code }, statusCode: code.Contains("NotFound", StringComparison.Ordinal) ? 404 : code.Contains("Scope", StringComparison.Ordinal) ? 403 : code.Contains("Busy", StringComparison.Ordinal) ? 429 : code.Contains("Unavailable", StringComparison.Ordinal) || code.Contains("Capability", StringComparison.Ordinal) ? 503 : 400);

    /// <summary>Converts unexpected request failures to safe responses before output starts.</summary>
    /// <example><code>return await AuditHttpHandlers.GuardAsync(() => handler());</code></example>
    public static async Task<IResult> GuardAsync(Func<Task<IResult>> action)
    {
        try { return await action().ConfigureAwait(false); }
        catch (AuditExportException error) { return Error(error.Code); }
        catch (JsonException) { return Error("AuditFilterInvalid"); }
        catch (OperationCanceledException) { return Error("AuditReadCanceled"); }
        catch (Exception) { return Error("AuditReadUnavailable"); }
    }
}
