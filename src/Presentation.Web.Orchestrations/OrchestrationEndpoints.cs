namespace BridgingIT.DevKit.Presentation.Web.Orchestrations;

using System.Net;
using System.Text.Json;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Orchestrations.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Exposes operational REST endpoints for inspecting and administering orchestration instances.
/// </summary>
public class OrchestrationEndpoints(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    IOrchestrationService orchestrationService,
    IOrchestrationQueryService orchestrationQueryService,
    IOrchestrationAdministrationService orchestrationAdministrationService,
    IOrchestrationDiagramService orchestrationDiagramService,
    OrchestrationRegistrationStore registrations,
    OrchestrationEndpointsOptions options = null) : EndpointsBase
{
    private const string ValidationProblemType = "/problems/orchestrations/validation";
    private const string NotFoundProblemType = "/problems/orchestrations/not-found";
    private const string InvalidStateProblemType = "/problems/orchestrations/invalid-state";
    private const string ConcurrencyConflictProblemType = "/problems/orchestrations/concurrency-conflict";
    private const string UnsupportedOperationProblemType = "/problems/orchestrations/unsupported-operation";
    private const string UnexpectedProblemType = "/problems/orchestrations/unexpected";

    private readonly ILogger<OrchestrationEndpoints> logger = loggerFactory?.CreateLogger<OrchestrationEndpoints>() ?? NullLogger<OrchestrationEndpoints>.Instance;
    private readonly OrchestrationEndpointsOptions options = options ?? new OrchestrationEndpointsOptions();

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled || !this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options);

        group.MapGet("definitions", this.GetDefinitions)
            .Produces<IEnumerable<string>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetDefinitions")
            .WithSummary("List orchestration definitions")
            .WithDescription("Retrieves registered orchestration definition names.");

        group.MapGet("definitions/{name}/diagram", this.GetDefinitionDiagram)
            .Produces<string>((int)HttpStatusCode.OK, "text/plain")
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetDefinitionDiagram")
            .WithSummary("Get orchestration definition diagram")
            .WithDescription("Retrieves Mermaid-compatible state diagram text for a registered orchestration definition.");

        group.MapGet("definitions/{name}/diagram/svg", this.GetDefinitionDiagramSvg)
            .Produces<string>((int)HttpStatusCode.OK, "image/svg+xml")
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetDefinitionDiagramSvg")
            .WithSummary("Get orchestration definition SVG diagram")
            .WithDescription("Retrieves rendered SVG markup for a registered orchestration definition.");

        group.MapGet(string.Empty, this.GetInstances)
            .Produces<IEnumerable<OrchestrationInstanceModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetInstances")
            .WithSummary("List orchestration instances")
            .WithDescription("Retrieves persisted orchestration instances with optional operational filters.");

        group.MapGet("metrics", this.GetMetrics)
            .Produces<OrchestrationMetricsModel>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetMetrics")
            .WithSummary("Get orchestration metrics")
            .WithDescription("Retrieves persisted orchestration metrics for dashboards and support tooling.");

        group.MapGet("{instanceId:guid}", this.GetInstance)
            .Produces<OrchestrationInstanceModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetInstance")
            .WithSummary("Get orchestration details")
            .WithDescription("Retrieves a single persisted orchestration instance.");

        group.MapGet("{instanceId:guid}/context", this.GetContext)
            .Produces<OrchestrationContextSnapshotModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetContext")
            .WithSummary("Get orchestration context")
            .WithDescription("Retrieves the latest persisted orchestration context snapshot.");

        group.MapGet("{instanceId:guid}/history", this.GetHistory)
            .Produces<IEnumerable<OrchestrationHistoryModel>>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetHistory")
            .WithSummary("Get orchestration history")
            .WithDescription("Retrieves persisted execution history for an orchestration instance.");

        group.MapGet("{instanceId:guid}/signals", this.GetSignals)
            .Produces<IEnumerable<OrchestrationSignalModel>>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetSignals")
            .WithSummary("Get orchestration signals")
            .WithDescription("Retrieves persisted signal records for an orchestration instance.");

        group.MapGet("{instanceId:guid}/timers", this.GetTimers)
            .Produces<IEnumerable<OrchestrationTimerModel>>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetTimers")
            .WithSummary("Get orchestration timers")
            .WithDescription("Retrieves persisted timer records for an orchestration instance.");

        group.MapGet("{instanceId:guid}/diagram", this.GetInstanceDiagram)
            .Produces<string>((int)HttpStatusCode.OK, "text/plain")
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetInstanceDiagram")
            .WithSummary("Get orchestration instance diagram")
            .WithDescription("Retrieves Mermaid-compatible state diagram text for a persisted orchestration instance.");

        group.MapGet("{instanceId:guid}/diagram/svg", this.GetInstanceDiagramSvg)
            .Produces<string>((int)HttpStatusCode.OK, "image/svg+xml")
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.GetInstanceDiagramSvg")
            .WithSummary("Get orchestration instance SVG diagram")
            .WithDescription("Retrieves rendered SVG markup for a persisted orchestration instance.");

        group.MapPost("{instanceId:guid}/signal", this.Signal)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.Signal")
            .WithSummary("Deliver an orchestration signal")
            .WithDescription("Persists and delivers a signal for an orchestration instance.");

        group.MapPost("{instanceId:guid}/pause", this.Pause)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.Pause")
            .WithSummary("Pause an orchestration")
            .WithDescription("Pauses a non-terminal orchestration instance.");

        group.MapPost("{instanceId:guid}/resume", this.Resume)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.Resume")
            .WithSummary("Resume an orchestration")
            .WithDescription("Resumes a previously paused orchestration instance.");

        group.MapPost("{instanceId:guid}/cancel", this.Cancel)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.Cancel")
            .WithSummary("Cancel an orchestration")
            .WithDescription("Cancels a non-terminal orchestration instance.");

        group.MapPost("{instanceId:guid}/terminate", this.Terminate)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.Terminate")
            .WithSummary("Terminate an orchestration")
            .WithDescription("Terminates a non-terminal orchestration instance.");

        group.MapPost("{instanceId:guid}/archive", this.Archive)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.Archive")
            .WithSummary("Archive an orchestration")
            .WithDescription("Archives a terminal orchestration instance.");

        group.MapPost("{instanceId:guid}/repair/release-lease", this.ReleaseLease)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.ReleaseLease")
            .WithSummary("Release an orchestration lease")
            .WithDescription("Releases an active lease for a stuck orchestration instance.");

        group.MapPost("{instanceId:guid}/repair/requeue-timers", this.RequeueTimers)
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.RequeueTimers")
            .WithSummary("Requeue orchestration timers")
            .WithDescription("Requeues persisted timers for a stuck orchestration instance.");

        group.MapDelete(string.Empty, this.Purge)
            .Produces<string>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Orchestrations.Purge")
            .WithSummary("Purge orchestration data")
            .WithDescription("Purges retained orchestration data by age and optional status filters.");

        this.IsRegistered = true;
    }

    private async Task<HttpResult> GetInstances(OrchestrationInstancesQueryModel request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Fetching orchestration instances (OrchestrationName={OrchestrationName}, StatusCount={StatusCount}, StateCount={StateCount}, Skip={Skip}, Take={Take})", request.OrchestrationName, request.Statuses.Count, request.States.Count, request.Skip, request.Take);

        var result = await orchestrationQueryService.QueryAsync(new OrchestrationQueryRequest
        {
            OrchestrationName = request.OrchestrationName,
            Statuses = request.Statuses.ToArray(),
            States = request.States.ToArray(),
            CorrelationId = request.CorrelationId,
            ConcurrencyKey = request.ConcurrencyKey,
            StartedFrom = request.StartedFrom,
            StartedTo = request.StartedTo,
            CompletedFrom = request.CompletedFrom,
            CompletedTo = request.CompletedTo,
            Skip = request.Skip,
            Take = request.Take,
            SortBy = request.SortBy,
            SortDescending = request.SortDescending,
        }, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(result.Value?.ToList() ?? [])
            : this.ToProblem(result.Errors, "Invalid orchestration query.");
    }

    private HttpResult GetDefinitions(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = serviceProvider.CreateScope();
        var names = registrations.GetRegisteredTypes()
            .Select(type => new
            {
                Type = type,
                Name = type.GetProperty("Name")?.GetValue(scope.ServiceProvider.GetRequiredService(type)) as string,
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item =>
            {
                registrations.RegisterName(item.Name, item.Type);
                return item.Name;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Results.Ok(names);
    }

    private async Task<HttpResult> GetDefinitionDiagram(string name, CancellationToken cancellationToken)
    {
        var result = await orchestrationDiagramService.GetDefinitionDiagramAsync(name, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Text(result.Value, "text/plain")
            : this.ToNamedOperationError(result.Errors, name, "Failed to fetch orchestration definition diagram.");
    }

    private async Task<HttpResult> GetDefinitionDiagramSvg(string name, CancellationToken cancellationToken)
    {
        var result = await orchestrationDiagramService.GetDefinitionDiagramAsync(name, DiagramRenderFormat.Svg, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Text(result.Value.GetText(), result.Value.ContentType)
            : this.ToNamedOperationError(result.Errors, name, "Failed to fetch orchestration definition SVG diagram.");
    }

    private async Task<HttpResult> GetMetrics(OrchestrationMetricsQueryModel request, CancellationToken cancellationToken)
    {
        var result = await orchestrationQueryService.GetMetricsAsync(new OrchestrationMetricsRequest
        {
            OrchestrationName = request.OrchestrationName,
            Statuses = request.Statuses.ToArray(),
            States = request.States.ToArray(),
            StartedFrom = request.StartedFrom,
            StartedTo = request.StartedTo,
            CompletedFrom = request.CompletedFrom,
            CompletedTo = request.CompletedTo,
        }, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : this.ToProblem(result.Errors, "Invalid orchestration metrics query.");
    }

    private async Task<HttpResult> GetInstance(Guid instanceId, CancellationToken cancellationToken)
    {
        var result = await orchestrationQueryService.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return this.ToSingleResult(result, instanceId, "Failed to fetch orchestration instance.");
    }

    private async Task<HttpResult> GetContext(Guid instanceId, CancellationToken cancellationToken)
    {
        var result = await orchestrationQueryService.GetContextAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return this.ToSingleResult(result, instanceId, "Failed to fetch orchestration context.");
    }

    private async Task<HttpResult> GetHistory(Guid instanceId, CancellationToken cancellationToken)
    {
        var result = await orchestrationQueryService.GetHistoryAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return this.ToSingleResult(result, instanceId, "Failed to fetch orchestration history.");
    }

    private async Task<HttpResult> GetSignals(Guid instanceId, CancellationToken cancellationToken)
    {
        var result = await orchestrationQueryService.GetSignalsAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return this.ToSingleResult(result, instanceId, "Failed to fetch orchestration signals.");
    }

    private async Task<HttpResult> GetTimers(Guid instanceId, CancellationToken cancellationToken)
    {
        var result = await orchestrationQueryService.GetTimersAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return this.ToSingleResult(result, instanceId, "Failed to fetch orchestration timers.");
    }

    private async Task<HttpResult> GetInstanceDiagram(Guid instanceId, CancellationToken cancellationToken)
    {
        var result = await orchestrationDiagramService.GetInstanceDiagramAsync(instanceId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Text(result.Value, "text/plain")
            : this.ToInstanceOperationError(result.Errors, instanceId, "Failed to fetch orchestration instance diagram.");
    }

    private async Task<HttpResult> GetInstanceDiagramSvg(Guid instanceId, CancellationToken cancellationToken)
    {
        var result = await orchestrationDiagramService.GetInstanceDiagramAsync(instanceId, DiagramRenderFormat.Svg, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Text(result.Value.GetText(), result.Value.ContentType)
            : this.ToInstanceOperationError(result.Errors, instanceId, "Failed to fetch orchestration instance SVG diagram.");
    }

    private async Task<HttpResult> Signal(Guid instanceId, [FromBody] OrchestrationSignalRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return this.ToProblem([new Error("Signal request body is required.")], "Invalid orchestration signal request.");
        }

        if (string.IsNullOrWhiteSpace(request.SignalName))
        {
            return this.ToProblem([new Error("SignalName is required.")], "Invalid orchestration signal request.");
        }

        var result = await orchestrationService.SignalAsync(instanceId, request.SignalName, request.Payload, request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return this.ToOperationResult(result, instanceId, $"Signal '{request.SignalName}' was accepted for orchestration instance '{instanceId}'.");
    }

    private async Task<HttpResult> Pause(Guid instanceId, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await orchestrationService.PauseAsync(instanceId, request?.Reason, cancellationToken).ConfigureAwait(false);
        return this.ToOperationResult(result, instanceId, $"Orchestration instance '{instanceId}' was paused.");
    }

    private async Task<HttpResult> Resume(Guid instanceId, CancellationToken cancellationToken)
    {
        var result = await orchestrationService.ResumeAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return this.ToOperationResult(result, instanceId, $"Orchestration instance '{instanceId}' was resumed.");
    }

    private async Task<HttpResult> Cancel(Guid instanceId, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await orchestrationService.CancelAsync(instanceId, request?.Reason, cancellationToken).ConfigureAwait(false);
        return this.ToOperationResult(result, instanceId, $"Orchestration instance '{instanceId}' was cancelled.");
    }

    private async Task<HttpResult> Terminate(Guid instanceId, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await orchestrationService.TerminateAsync(instanceId, request?.Reason, cancellationToken).ConfigureAwait(false);
        return this.ToOperationResult(result, instanceId, $"Orchestration instance '{instanceId}' was terminated.");
    }

    private async Task<HttpResult> Archive(Guid instanceId, CancellationToken cancellationToken)
    {
        var result = await orchestrationAdministrationService.ArchiveAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : this.ToInstanceOperationError(result.Errors, instanceId, "Failed to archive orchestration instance.");
    }

    private async Task<HttpResult> ReleaseLease(Guid instanceId, CancellationToken cancellationToken)
    {
        var result = await orchestrationAdministrationService.ReleaseLeaseAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : this.ToInstanceOperationError(result.Errors, instanceId, "Failed to release orchestration lease.");
    }

    private async Task<HttpResult> RequeueTimers(Guid instanceId, CancellationToken cancellationToken)
    {
        var result = await orchestrationAdministrationService.RequeueTimersAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : this.ToInstanceOperationError(result.Errors, instanceId, "Failed to requeue orchestration timers.");
    }

    private async Task<HttpResult> Purge(OrchestrationPurgeModel request, CancellationToken cancellationToken)
    {
        var result = await orchestrationAdministrationService.PurgeAsync(new OrchestrationPurgeRequest
        {
            OlderThan = request.OlderThan,
            Statuses = request.Statuses.ToArray(),
            IsArchived = request.IsArchived,
        }, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return this.ToProblem(result.Errors, "Invalid orchestration purge request.");
        }

        return Results.Ok($"Purged {result.Value.PurgedInstanceCount} orchestration instance(s), {result.Value.PurgedHistoryCount} history record(s), {result.Value.PurgedSignalCount} signal(s), and {result.Value.PurgedTimerCount} timer(s).");
    }

    private HttpResult ToInstanceOperationError(IReadOnlyList<IResultError> errors, Guid instanceId, string fallbackTitle)
    {
        var message = FirstError(errors) ?? fallbackTitle;
        return IsNotFound(message)
            ? Results.NotFound($"Orchestration instance '{instanceId}' was not found.")
            : this.ToProblem(errors, fallbackTitle);
    }

    private HttpResult ToNamedOperationError(IReadOnlyList<IResultError> errors, string name, string fallbackTitle)
    {
        var message = FirstError(errors) ?? fallbackTitle;
        return IsNotFound(message)
            ? Results.NotFound($"Orchestration definition '{name}' was not found.")
            : this.ToProblem(errors, fallbackTitle);
    }

    private HttpResult ToOperationResult(Result result, Guid instanceId, string successMessage)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(successMessage);
        }

        return this.ToInstanceOperationError(result.Errors, instanceId, "Failed to execute orchestration operation.");
    }

    private HttpResult ToProblem(IReadOnlyList<IResultError> errors, string fallbackTitle)
    {
        var message = FirstError(errors) ?? fallbackTitle;
        var (statusCode, title, type) = ClassifyProblem(message, fallbackTitle);
        return Results.Problem(message, statusCode: statusCode, title: title, type: type);
    }

    private HttpResult ToSingleResult<T>(Result<T> result, Guid instanceId, string fallbackTitle)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return this.ToInstanceOperationError(result.Errors, instanceId, fallbackTitle);
    }

    private static (int statusCode, string title, string type) ClassifyProblem(string message, string fallbackTitle)
    {
        if (IsValidation(message))
        {
            return ((int)HttpStatusCode.BadRequest, fallbackTitle, ValidationProblemType);
        }

        if (IsConcurrency(message))
        {
            return ((int)HttpStatusCode.Conflict, fallbackTitle, ConcurrencyConflictProblemType);
        }

        if (IsUnsupported(message))
        {
            return ((int)HttpStatusCode.Conflict, fallbackTitle, UnsupportedOperationProblemType);
        }

        if (IsInvalidState(message))
        {
            return ((int)HttpStatusCode.Conflict, fallbackTitle, InvalidStateProblemType);
        }

        return ((int)HttpStatusCode.InternalServerError, fallbackTitle, UnexpectedProblemType);
    }

    private static string FirstError(IReadOnlyList<IResultError> errors)
    {
        return errors.SafeNull().FirstOrDefault()?.Message;
    }

    private static bool IsConcurrency(string message)
    {
        return message?.Contains("lease", StringComparison.OrdinalIgnoreCase) == true &&
            (message.Contains("already", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("authoritative", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("lost", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInvalidState(string message)
    {
        return message?.Contains("already terminal", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("already paused", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("not paused", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("not archivable", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("does not have an active lease", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("does not have requeueable timers", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsNotFound(string message)
    {
        return message?.Contains("was not found", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsUnsupported(string message)
    {
        return message?.Contains("unsupported", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsValidation(string message)
    {
        return message?.Contains("Unknown orchestration status", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("Unsupported orchestration sort field", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("Skip must", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("Take must", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("SignalName is required", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("request body is required", StringComparison.OrdinalIgnoreCase) == true;
    }
}