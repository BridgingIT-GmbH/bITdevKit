// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Reflection;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

namespace BridgingIT.DevKit.Application.Orchestrations;
/// <summary>
/// Builds orchestration definition and instance diagrams using the reusable diagram infrastructure.
/// </summary>
public class OrchestrationDiagramService(
    IServiceProvider serviceProvider,
    OrchestrationRegistrationStore registrations,
    IOrchestrationQueryService queryService,
    OrchestrationDefinitionDiagramProjector definitionProjector,
    OrchestrationInstanceDiagramProjector instanceProjector,
    IDiagramRendererFactory rendererFactory) : IOrchestrationDiagramService
{
    /// <inheritdoc />
    public async Task<Result<string>> GetDefinitionDiagramAsync(string orchestrationName, CancellationToken cancellationToken = default)
    {
        var result = await this.GetDefinitionDiagramAsync(orchestrationName, DiagramRenderFormat.Mermaid, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<string>.Success(result.Value.GetText())
            : Result<string>.Failure().WithErrors(result.Errors);
    }

    /// <inheritdoc />
    public async Task<Result<DiagramRenderResult>> GetDefinitionDiagramAsync(string orchestrationName, DiagramRenderFormat format, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolution = await this.ResolveDefinitionAsync(orchestrationName, cancellationToken).ConfigureAwait(false);
            if (resolution.IsFailure)
            {
                return Result<DiagramRenderResult>.Failure().WithErrors(resolution.Errors);
            }

            var document = this.ProjectDefinitionDocument(resolution.Value.OrchestrationType, resolution.Value.Definition);
            return Result<DiagramRenderResult>.Success(rendererFactory.Render(document, format));
        }
        catch (OperationCanceledException)
        {
            return Result<DiagramRenderResult>.Failure().WithError(new Error("Orchestration definition diagram generation was canceled."));
        }
        catch (Exception exception)
        {
            return Result<DiagramRenderResult>.Failure().WithError(new Error(exception.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> GetInstanceDiagramAsync(Guid instanceId, OrchestrationDiagramOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await this.GetInstanceDiagramAsync(instanceId, DiagramRenderFormat.Mermaid, options, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<string>.Success(result.Value.GetText())
            : Result<string>.Failure().WithErrors(result.Errors);
    }

    /// <inheritdoc />
    public async Task<Result<DiagramRenderResult>> GetInstanceDiagramAsync(Guid instanceId, DiagramRenderFormat format, OrchestrationDiagramOptions options = null, CancellationToken cancellationToken = default)
    {
        options ??= new OrchestrationDiagramOptions();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var instanceResult = await queryService.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (instanceResult.IsFailure)
            {
                return Result<DiagramRenderResult>.Failure().WithErrors(instanceResult.Errors);
            }

            var resolution = await this.ResolveDefinitionAsync(instanceResult.Value.OrchestrationName, cancellationToken).ConfigureAwait(false);
            if (resolution.IsFailure)
            {
                return Result<DiagramRenderResult>.Failure().WithErrors(resolution.Errors);
            }

            var definitionDocument = this.ProjectDefinitionDocument(resolution.Value.OrchestrationType, resolution.Value.Definition);
            var historyResult = options.IncludeHistory
                ? await queryService.GetHistoryAsync(instanceId, cancellationToken).ConfigureAwait(false)
                : Result<IReadOnlyList<OrchestrationHistoryModel>>.Success([]);
            if (historyResult.IsFailure)
            {
                return Result<DiagramRenderResult>.Failure().WithErrors(historyResult.Errors);
            }

            var signalResult = options.IncludeSignals
                ? await queryService.GetSignalsAsync(instanceId, cancellationToken).ConfigureAwait(false)
                : Result<IReadOnlyList<OrchestrationSignalModel>>.Success([]);
            if (signalResult.IsFailure)
            {
                return Result<DiagramRenderResult>.Failure().WithErrors(signalResult.Errors);
            }

            var timerResult = options.IncludeTimers
                ? await queryService.GetTimersAsync(instanceId, cancellationToken).ConfigureAwait(false)
                : Result<IReadOnlyList<OrchestrationTimerModel>>.Success([]);
            if (timerResult.IsFailure)
            {
                return Result<DiagramRenderResult>.Failure().WithErrors(timerResult.Errors);
            }

            var document = instanceProjector.Project(
                definitionDocument,
                instanceResult.Value,
                historyResult.Value,
                signalResult.Value,
                timerResult.Value,
                options);

            return Result<DiagramRenderResult>.Success(rendererFactory.Render(document, format));
        }
        catch (OperationCanceledException)
        {
            return Result<DiagramRenderResult>.Failure().WithError(new Error("Orchestration instance diagram generation was canceled."));
        }
        catch (Exception exception)
        {
            return Result<DiagramRenderResult>.Failure().WithError(new Error(exception.Message));
        }
    }

    private DiagramDocument ProjectDefinitionDocument(Type orchestrationType, object definition)
    {
        var dataType = registrations.GetDataType(orchestrationType);
        var method = GetType()
            .GetMethod(nameof(ProjectDefinitionDocumentCore), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(dataType);
        return (DiagramDocument)method.Invoke(this, [definition])!;
    }

    private DiagramDocument ProjectDefinitionDocumentCore<TData>(object definition)
        where TData : class, IOrchestrationData
    {
        return definitionProjector.Project((OrchestrationDefinition<TData>)definition);
    }

    private async Task<Result<ResolvedOrchestrationDefinition>> ResolveDefinitionAsync(string orchestrationName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orchestrationName);

        await using var scope = serviceProvider.CreateAsyncScope();
        var orchestrationType = await this.ResolveOrchestrationTypeAsync(scope.ServiceProvider, orchestrationName, cancellationToken).ConfigureAwait(false);
        if (orchestrationType is null)
        {
            return Result<ResolvedOrchestrationDefinition>.Failure().WithError(new Error($"Orchestration definition '{orchestrationName}' was not found."));
        }

        var orchestration = scope.ServiceProvider.GetRequiredService(orchestrationType);
        var actualName = orchestrationType.GetProperty(nameof(IOrchestration<IOrchestrationData>.Name))?.GetValue(orchestration) as string;
        if (!string.IsNullOrWhiteSpace(actualName))
        {
            registrations.RegisterName(actualName, orchestrationType);
        }

        var definition = orchestrationType.GetMethod("GetDefinition", BindingFlags.Instance | BindingFlags.Public)?.Invoke(orchestration, null);
        return definition is null
            ? Result<ResolvedOrchestrationDefinition>.Failure().WithError(new Error($"Orchestration definition '{orchestrationName}' could not be resolved."))
            : Result<ResolvedOrchestrationDefinition>.Success(new ResolvedOrchestrationDefinition(orchestrationType, definition));
    }

    private async Task<Type> ResolveOrchestrationTypeAsync(IServiceProvider scopedProvider, string orchestrationName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (registrations.TryGetByName(orchestrationName, out var orchestrationType))
        {
            return orchestrationType;
        }

        foreach (var registeredType in registrations.GetRegisteredTypes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var orchestration = scopedProvider.GetRequiredService(registeredType);
            var name = registeredType.GetProperty(nameof(IOrchestration<IOrchestrationData>.Name))?.GetValue(orchestration) as string;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            registrations.RegisterName(name, registeredType);
            if (string.Equals(name, orchestrationName, StringComparison.OrdinalIgnoreCase))
            {
                return registeredType;
            }
        }

        return null;
    }

    private sealed record ResolvedOrchestrationDefinition(Type OrchestrationType, object Definition);
}