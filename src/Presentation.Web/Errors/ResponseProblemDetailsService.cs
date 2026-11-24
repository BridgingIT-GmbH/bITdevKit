// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Errors;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using NativeOptions = Microsoft.AspNetCore.Http.ProblemDetailsOptions;
using Problem = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>Prepares DevKit problems and writes them through the first applicable registered writer.</summary>
public sealed class ResponseProblemDetailsService(
    IEnumerable<IProblemDetailsWriter> writers,
    IOptions<NativeOptions> nativeOptions,
    IOptions<HttpJsonOptions> jsonOptions,
    IOptions<ProblemDetailsOptions> options,
    ILogger<ResponseProblemDetailsService> logger) : IProblemDetailsService
{
    private static readonly ConditionalWeakTable<Problem, ResultLog> Results = new();
    private readonly IProblemDetailsWriter[] writers = writers.ToArray();
    private readonly IOptions<NativeOptions> nativeOptions = nativeOptions;
    private readonly IOptions<HttpJsonOptions> jsonOptions = jsonOptions;
    private readonly IOptions<ProblemDetailsOptions> options = options;
    private readonly ILogger<ResponseProblemDetailsService> logger = logger;

    /// <inheritdoc />
    public async ValueTask WriteAsync(ProblemDetailsContext context)
    {
        if (!await this.TryWriteAsync(context))
        {
            throw new InvalidOperationException("No registered IProblemDetailsWriter can write this response.");
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ProblemDetails);
        ArgumentNullException.ThrowIfNull(context.HttpContext);

        var http = context.HttpContext;
        if (HasFailedWrite(http))
        {
            return false;
        }

        var problem = context.ProblemDetails;
        var managed = Preparation.Applies(context);
        try
        {
            foreach (var writer in this.writers)
            {
                // MVC's stock API writer performs a second negotiation and can return without writing.
                // Shared problems use HTTP JSON negotiation. MVC's regular ObjectResult path is independent.
                if ((managed && writer.GetType().Assembly == typeof(ProblemDetailsFactory).Assembly) || !writer.CanWrite(context))
                {
                    continue;
                }

                if (managed)
                {
                    await this.WritePreparedAsync(context, writer);
                }
                else
                {
                    await writer.WriteAsync(context);
                }

                return true;
            }
        }
        catch when (managed)
        {
            http.Features.Set(new FailedWrite());
            throw;
        }

        if (managed)
        {
            this.LogProblemResult(http, problem, problem);
        }

        return false;
    }

    private async ValueTask WritePreparedAsync(ProblemDetailsContext context, IProblemDetailsWriter writer)
    {
        var http = context.HttpContext;
        var problem = context.ProblemDetails;
        var previous = http.Features.Get<PreparedWrite>();
        var selectedStatus = problem.Status ?? http.Response.StatusCode;
        var traceKey = this.jsonOptions.Value.SerializerOptions.PropertyNamingPolicy?.ConvertName("traceId") ?? "traceId";
        var current = new PreparedWrite(context, problem, traceKey);
        http.Features.Set(current);
        try
        {
            context.ProblemDetails = ProblemDetailsPayload.Copy(problem);
            ProblemDetailsPayload.SetTrace(context.ProblemDetails, http, traceKey);
            if (traceKey != "traceId")
            {
                ProblemDetailsPayload.SetTrace(context.ProblemDetails, http);
            }

            this.nativeOptions.Value.CustomizeProblemDetails?.Invoke(context);
            context.ProblemDetails.Status = selectedStatus;
            http.Response.StatusCode = selectedStatus;
            current.Complete();
            this.LogProblemResult(http, problem, context.ProblemDetails);
            await writer.WriteAsync(context);
        }
        finally
        {
            http.Features.Set(previous);
            context.ProblemDetails = problem;
        }
    }

    /// <summary>Associates a server Result problem with metadata for execution-time logging.</summary>
    /// <param name="problem">The problem produced for this Result mapping.</param>
    /// <param name="result">The mapped failure containing the Result error types.</param>
    /// <param name="logger">The explicit mapper logger, or null to allow the shared logging policy.</param>
    public static void TrackResult(Problem problem, Result result, ILogger logger)
    {
        if (problem.Status is >= 500 && logger is null)
        {
            Results.TryAdd(problem, new ResultLog(GetErrorTypes(result)));
        }
    }

    /// <summary>Logs a mapped custom response after it has selected its status.</summary>
    /// <param name="http">The request whose response was executed.</param>
    /// <param name="result">The failed Result that produced the custom response.</param>
    public static void LogCustomResult(HttpContext http, Result result)
    {
        var status = http.Response.StatusCode;
        if (status is >= 500 and <= 599 && http.Features.Get<LoggedResults>() is null && ShouldLogResult(http))
        {
            http.Features.Set(new LoggedResults());
            WriteResultLog(http.RequestServices.GetRequiredService<ILogger<ResponseProblemDetailsService>>(),
                status, GetErrorTypes(result), ProblemDetailsPayload.GetResultCorrelation(http));
        }
    }

    private void LogProblemResult(HttpContext http, Problem original, Problem response)
    {
        if (response.Status is >= 500 and <= 599 && Results.TryGetValue(original, out var log) &&
            ShouldLogResult(http, this.options) && log.TryBegin(http))
        {
            WriteResultLog(this.logger, response.Status.Value, log.ErrorTypes, ProblemDetailsPayload.GetResultCorrelation(http, response));
        }
    }

    private static bool ShouldLogResult(HttpContext http, IOptions<ProblemDetailsOptions> options = null) =>
        http.RequestServices?.GetService<CombinedProblemDetailsRegistration>() is not null &&
        (options ?? http.RequestServices.GetRequiredService<IOptions<ProblemDetailsOptions>>()).Value.ShouldLogResultErrors;

    private static string[] GetErrorTypes(Result result) =>
        result.Errors.Select(error => error.GetType().FullName).Distinct().ToArray();

    private static void WriteResultLog(ILogger logger, int status, string[] errorTypes, object correlationId) =>
        logger.LogError("result - HTTP {StatusCode} failure ({ErrorTypes}, correlationId={CorrelationId})",
            status, errorTypes, correlationId);

    /// <summary>Reports whether writer selection, writing or shared preparation already failed in this request.</summary>
    /// <param name="context">The current HTTP request.</param>
    /// <returns>Whether the request already failed while preparing or writing a problem.</returns>
    public static bool HasFailedWrite(HttpContext context) => context.Features.Get<FailedWrite>() is not null;

    private sealed class FailedWrite;

    private sealed class ResultLog(string[] errorTypes)
    {
        public string[] ErrorTypes { get; } = errorTypes;

        public bool TryBegin(HttpContext context)
        {
            var logged = context.Features.Get<LoggedResults>();
            if (logged is null)
            {
                logged = new LoggedResults();
                context.Features.Set(logged);
            }

            return logged.Entries.Add(this);
        }
    }

    private sealed class LoggedResults
    {
        public HashSet<ResultLog> Entries { get; } = [];
    }

    private sealed class PreparedWrite(ProblemDetailsContext context, Problem original, string traceKey)
    {
        private bool hadTrace;
        private object trace;

        public ProblemDetailsContext Context { get; } = context;

        public Problem Original { get; } = original;

        public bool Completed { get; private set; }

        public void Complete()
        {
            this.hadTrace = this.Context.ProblemDetails.Extensions.TryGetValue(traceKey, out this.trace);
            this.Completed = true;
        }

        public void RestoreTrace()
        {
            if (this.hadTrace)
            {
                this.Context.ProblemDetails.Extensions[traceKey] = this.trace;
            }
            else
            {
                this.Context.ProblemDetails.Extensions.Remove(traceKey);
            }
        }
    }

    /// <summary>Composes native Problem Details customization with DevKit response preparation.</summary>
    /// <param name="configure">The host's native options configuration.</param>
    /// <param name="postConfigure">The host's native options post-configuration.</param>
    /// <param name="validators">The host's native options validators.</param>
    public sealed class Preparation(
        IEnumerable<IConfigureOptions<NativeOptions>> configure,
        IEnumerable<IPostConfigureOptions<NativeOptions>> postConfigure,
        IEnumerable<IValidateOptions<NativeOptions>> validators) : IOptionsFactory<NativeOptions>
    {
        private readonly OptionsFactory<NativeOptions> factory = new(configure, postConfigure, validators);

        /// <inheritdoc />
        public NativeOptions Create(string name)
        {
            var options = this.factory.Create(name);
            if (name == Options.DefaultName)
            {
                WrapCustomization(options);
            }

            return options;
        }

        private static void WrapCustomization(NativeOptions options)
        {
            // Native writers and MVC factories resolve the same callback after all host configuration has run.
            var customize = options.CustomizeProblemDetails;
            options.CustomizeProblemDetails = context =>
            {
                var http = context.HttpContext;
                var prepared = http.Features.Get<PreparedWrite>();
                if (!ReferenceEquals(prepared?.Context, context))
                {
                    prepared = null;
                }
                else if (prepared.Completed)
                {
                    prepared.RestoreTrace();
                    return;
                }

                if (!Applies(context))
                {
                    customize?.Invoke(context);
                    return;
                }

                try
                {
                    Customize(context, prepared?.Original ?? context.ProblemDetails, customize);
                }
                catch when (prepared is null)
                {
                    // The MVC factory can call customization without going through the writer service.
                    http.Features.Set(new FailedWrite());
                    throw;
                }
            };
        }

        private static void Customize(ProblemDetailsContext context, Problem original, Action<ProblemDetailsContext> customize)
        {
            var http = context.HttpContext;
            var beforeCustomization = context.ProblemDetails;
            var status = original.Status ?? http.Response.StatusCode;
            var responseStatus = http.Response.StatusCode;
            customize?.Invoke(context);
            var current = context.ProblemDetails;
            if (!ReferenceEquals(current, beforeCustomization))
            {
                current = ProblemDetailsPayload.Copy(current);
                context.ProblemDetails = current;
            }

            var combined = http.RequestServices.GetService<CombinedProblemDetailsRegistration>() is not null;
            var devKit = combined ? http.RequestServices.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value : null;
            ProblemDetailsPayload.SetCorrelation(current, http, automatic: context.Exception is not null);
            if (devKit?.IncludeExceptionDetails is true ||
                (!combined && http.RequestServices.GetService<IOptions<ResultHttpProblemDetailsOptions>>()?.Value.IncludeExceptionDetails is true))
            {
                if (original is ResultHttpProblemDetails result)
                {
                    result.AddDiagnostics(current);
                }
                else if (combined && context.Exception is { } exception)
                {
                    current.Extensions.TryAdd("exceptionDetails", new[] { ProblemDetailsPayload.BuildDiagnostic(exception) });
                }
            }

            devKit?.OnBeforeWriteDetails?.Invoke(http, current);
            current.Status = status;
            http.Response.StatusCode = responseStatus;
        }

        /// <summary>Returns whether the context participates in shared problem preparation.</summary>
        public static bool Applies(ProblemDetailsContext context)
        {
            if ((context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode) < 400)
            {
                return false;
            }

            return context.Exception is null ||
                !ReferenceEquals(context.Exception,
                    context.HttpContext.Features.Get<ResponseProblemDetailsExceptionHandler.DeclinedException>()?.Exception);
        }
    }
}

/// <summary>Identifies hosts that registered combined DevKit Problem Details.</summary>
public sealed class CombinedProblemDetailsRegistration;
/// <summary>Identifies hosts that registered the standalone DevKit exception chain.</summary>
public sealed class StandaloneExceptionHandlerRegistration;
