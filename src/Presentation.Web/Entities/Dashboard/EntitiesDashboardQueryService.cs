// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>One batch of masked rows.</summary>
/// <param name="Rows">The projected rows (masked and hidden members already applied) keyed by property name.</param>
/// <param name="Batch">The 1-based batch number that was loaded.</param>
/// <param name="BatchSize">The batch size that was used.</param>
/// <param name="HasMore">True when the batch is full, so another batch may exist (no total count is queried).</param>
/// <param name="StatusCode">The status code: 200, 400 for an invalid query or 500.</param>
/// <param name="Error">The user-facing error, or null.</param>
/// <param name="CorrelationId">The correlation id of a server failure.</param>
/// <param name="DeletedFlags">The soft-deleted flag of each row (same order as <paramref name="Rows"/>), or null when unknown.</param>
internal sealed record EntitiesDashboardRowsResult(
    IReadOnlyList<IDictionary<string, object>> Rows,
    int Batch,
    int BatchSize,
    bool HasMore,
    int StatusCode,
    string Error,
    string CorrelationId,
    IReadOnlyList<bool> DeletedFlags = null)
{
    /// <summary>Gets the next batch number.</summary>
    public int NextBatch => this.Batch + 1;

    /// <summary>Gets a value indicating whether the batch was loaded.</summary>
    public bool Success => this.StatusCode == StatusCodes.Status200OK;
}

/// <summary>The detail of one record.</summary>
/// <param name="Row">The projected masked values keyed by property name.</param>
/// <param name="Id">The id text.</param>
/// <param name="ConcurrencyVersion">The stored concurrency version text, or null.</param>
/// <param name="IsDeleted">True when the record is soft-deleted.</param>
/// <param name="ChangeHistoryUrl">The change-history link, or null when no provider returns one.</param>
/// <param name="StatusCode">The status code: 200, 400, 404 or 500.</param>
/// <param name="Error">The user-facing error, or null.</param>
/// <param name="CorrelationId">The correlation id of a server failure.</param>
internal sealed record EntitiesDashboardDetailResult(
    IDictionary<string, object> Row,
    string Id,
    string ConcurrencyVersion,
    bool IsDeleted,
    string ChangeHistoryUrl,
    int StatusCode,
    string Error,
    string CorrelationId)
{
    /// <summary>Gets a value indicating whether the record was loaded.</summary>
    public bool Success => this.StatusCode == StatusCodes.Status200OK;
}

/// <summary>Reads entity rows for the Entities dashboard. Every client-supplied field is validated against the descriptors.</summary>
/// <param name="loggerFactory">The optional logger factory.</param>
internal sealed class EntitiesDashboardQueryService(ILoggerFactory loggerFactory = null)
{
    private readonly ILogger logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<EntitiesDashboardQueryService>();

    /// <summary>Resolves a source of an entity by name.</summary>
    /// <param name="entity">The entity.</param>
    /// <param name="name">The source name, or null for the default source.</param>
    /// <returns>The source, or null when the name is unknown.</returns>
    public static IEntitiesDashboardSource ResolveSource(EntitiesDashboardEntity entity, string name) =>
        string.IsNullOrWhiteSpace(name)
            ? entity.DefaultSource
            : entity.Sources.FirstOrDefault(s => string.Equals(s.SourceName, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Loads one batch of rows.</summary>
    /// <param name="http">The HTTP context.</param>
    /// <param name="entity">The entity.</param>
    /// <param name="query">The parsed query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows or an error.</returns>
    public async Task<EntitiesDashboardRowsResult> GetRowsAsync(
        HttpContext http,
        EntitiesDashboardEntity entity,
        EntitiesDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var options = EntitiesDashboardOptionsAccessor.Get(http.RequestServices);
        var batchSize = options.ResolveBatchSize(query.PageSize);
        var source = ResolveSource(entity, query.Source);
        if (source == null)
        {
            return RowsError(query.Batch, batchSize, "The source is not available for this entity.");
        }

        var filter = EntitiesDashboardFilterBuilder.Build(entity, query, batchSize);
        if (filter.IsFailure)
        {
            return RowsError(query.Batch, batchSize, filter.Messages.FirstOrDefault() ?? "The filter is invalid.");
        }

        try
        {
            var result = await source.FindAllAsync(http.RequestServices, filter.Value, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                var correlationId = EntitiesDashboardOutcome.GetCorrelationId(http);
                EntitiesDashboardLog.FailedResult(this.logger, entity.Name, "query", correlationId, result.Errors.FirstOrDefault()?.GetType().Name, result.Errors.FirstOrDefault()?.Message);

                return RowsError(query.Batch, batchSize, $"The rows could not be loaded. Correlation id: {correlationId}", StatusCodes.Status500InternalServerError, correlationId);
            }

            var instances = (result.Value ?? []).ToList();
            var rows = instances.Select(r => EntitiesDashboardMasking.Project(entity, r)).ToList();
            var deleted = instances.Select(EntitiesDashboardWriteService.IsSoftDeleted).ToList();

            return new EntitiesDashboardRowsResult(rows, query.Batch, batchSize, rows.Count == batchSize, StatusCodes.Status200OK, null, null, deleted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // expression building can fail for unusual types; the client only learns that the query was not accepted
            var correlationId = EntitiesDashboardOutcome.GetCorrelationId(http);
            EntitiesDashboardLog.Failed(this.logger, ex, entity.Name, "query", correlationId);

            return RowsError(
                query.Batch,
                batchSize,
                $"The query could not be executed. Check the filter conditions. Correlation id: {correlationId}",
                query.Filters.Count > 0 || query.QuickSearch != null ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError,
                correlationId);
        }
    }

    /// <summary>Loads one record, with the masked projection and the change-history link.</summary>
    /// <param name="http">The HTTP context.</param>
    /// <param name="entity">The entity.</param>
    /// <param name="sourceName">The source name, or null for the default source.</param>
    /// <param name="id">The id text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The detail or an error.</returns>
    public async Task<EntitiesDashboardDetailResult> GetDetailAsync(
        HttpContext http,
        EntitiesDashboardEntity entity,
        string sourceName,
        string id,
        CancellationToken cancellationToken)
    {
        var source = ResolveSource(entity, sourceName);
        if (source == null)
        {
            return DetailError(StatusCodes.Status400BadRequest, "The source is not available for this entity.");
        }

        var parsed = EntitiesDashboardIdConverter.Parse(entity.IdType, id);
        if (parsed.IsFailure)
        {
            return DetailError(StatusCodes.Status400BadRequest, "The id is not valid.");
        }

        try
        {
            var result = await source.FindOneAsync(http.RequestServices, parsed.Value, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure || result.Value == null)
            {
                var outcome = EntitiesDashboardOutcome.FromErrors(
                    result.IsFailure ? result.Errors.ToList() : [new NotFoundError()],
                    EntitiesDashboardOutcome.GetCorrelationId(http));

                return DetailError(outcome.StatusCode, outcome.Message, outcome.CorrelationId);
            }

            var instance = result.Value;
            var row = EntitiesDashboardMasking.Project(entity, instance);
            var concurrency = entity.Properties.FirstOrDefault(p => p.IsConcurrency);
            var version = concurrency?.Getter?.Invoke(instance);

            return new EntitiesDashboardDetailResult(
                row,
                EntitiesDashboardIdConverter.Format(entity.IdProperty?.Getter?.Invoke(instance) ?? parsed.Value),
                version == null ? null : EntitiesDashboardValueConverter.Format(version),
                EntitiesDashboardWriteService.IsSoftDeleted(instance),
                GetChangeHistoryUrl(http, entity, id),
                StatusCodes.Status200OK,
                null,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var correlationId = EntitiesDashboardOutcome.GetCorrelationId(http);
            EntitiesDashboardLog.Failed(this.logger, ex, entity.Name, "detail", correlationId);

            return DetailError(StatusCodes.Status500InternalServerError, $"The record could not be loaded. Correlation id: {correlationId}", correlationId);
        }
    }

    private static string GetChangeHistoryUrl(HttpContext http, EntitiesDashboardEntity entity, string id)
    {
        foreach (var provider in http.RequestServices.GetServices<IEntitiesDashboardChangeHistoryLinkProvider>())
        {
            var link = provider.GetLink(entity.EntityType, id);
            if (!string.IsNullOrWhiteSpace(link))
            {
                return link;
            }
        }

        return null;
    }

    private static EntitiesDashboardRowsResult RowsError(int batch, int batchSize, string error, int statusCode = StatusCodes.Status400BadRequest, string correlationId = null) =>
        new([], batch, batchSize, false, statusCode, error, correlationId);

    private static EntitiesDashboardDetailResult DetailError(int statusCode, string error, string correlationId = null) =>
        new(null, null, null, false, null, statusCode, error, correlationId);
}
