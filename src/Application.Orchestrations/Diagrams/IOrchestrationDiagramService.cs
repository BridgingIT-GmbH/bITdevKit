// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using BridgingIT.DevKit.Common;

namespace BridgingIT.DevKit.Application.Orchestrations;
/// <summary>
/// Provides orchestration-specific diagram export operations.
/// </summary>
public interface IOrchestrationDiagramService
{
    /// <summary>
    /// Renders the registered orchestration definition as Mermaid-compatible state diagram text.
    /// </summary>
    /// <param name="orchestrationName">The orchestration definition name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rendered diagram text.</returns>
    /// <example>
    /// <code>
    /// var result = await service.GetDefinitionDiagramAsync("OrderApprovalOrchestration", cancellationToken);
    /// </code>
    /// </example>
    Task<Result<string>> GetDefinitionDiagramAsync(
        string orchestrationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders the registered orchestration definition using the requested diagram format.
    /// </summary>
    /// <param name="orchestrationName">The orchestration definition name.</param>
    /// <param name="format">The requested diagram format.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rendered diagram payload.</returns>
    /// <example>
    /// <code>
    /// var result = await service.GetDefinitionDiagramAsync(
    ///     "OrderApprovalOrchestration",
    ///     DiagramRenderFormat.Svg,
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DiagramRenderResult>> GetDefinitionDiagramAsync(
        string orchestrationName,
        DiagramRenderFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders the persisted orchestration instance as Mermaid-compatible state diagram text.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="options">The diagram options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rendered diagram text.</returns>
    /// <example>
    /// <code>
    /// var result = await service.GetInstanceDiagramAsync(instanceId, new OrchestrationDiagramOptions
    /// {
    ///     HighlightCurrentState = true,
    /// }, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<string>> GetInstanceDiagramAsync(
        Guid instanceId,
        OrchestrationDiagramOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders the persisted orchestration instance using the requested diagram format.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="format">The requested diagram format.</param>
    /// <param name="options">The diagram options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rendered diagram payload.</returns>
    /// <example>
    /// <code>
    /// var result = await service.GetInstanceDiagramAsync(
    ///     instanceId,
    ///     DiagramRenderFormat.Svg,
    ///     new OrchestrationDiagramOptions
    ///     {
    ///         HighlightCurrentState = true,
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DiagramRenderResult>> GetInstanceDiagramAsync(
        Guid instanceId,
        DiagramRenderFormat format,
        OrchestrationDiagramOptions options = null,
        CancellationToken cancellationToken = default);
}