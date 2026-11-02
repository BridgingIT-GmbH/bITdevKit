// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Writes entity rows for the Entities dashboard (create, update, delete, bulk delete). The caller must run the
/// <see cref="EntitiesDashboardWriteGuard"/> first. Every operation returns an <see cref="EntitiesDashboardOutcome"/>
/// and never leaks exception messages.
/// </summary>
/// <param name="loggerFactory">The optional logger factory.</param>
internal sealed class EntitiesDashboardWriteService(ILoggerFactory loggerFactory = null)
{
    private readonly ILogger logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<EntitiesDashboardWriteService>();

    /// <summary>Determines whether an instance is soft-deleted.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>True when the instance is marked as deleted.</returns>
    public static bool IsSoftDeleted(object instance) =>
        instance is ISoftDeletable { Deleted: true } || (instance is IAuditable { AuditState: not null } auditable && auditable.AuditState.IsDeleted());

    /// <summary>Creates a record from the submitted values.</summary>
    /// <param name="http">The HTTP context.</param>
    /// <param name="body">The JSON body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome; <c>payload.id</c> holds the new id (status 201).</returns>
    public async Task<EntitiesDashboardOutcome> CreateAsync(HttpContext http, JsonElement body, CancellationToken cancellationToken)
    {
        var context = Resolve(http, body, out var outcome);
        if (outcome != null)
        {
            return outcome;
        }

        var (entity, source, request) = context.Value;
        if (!entity.CanCreate)
        {
            return EntitiesDashboardOutcome.Failed(entity.CapabilityNote ?? "Create is not available for this entity.");
        }

        try
        {
            // ActiveEntity upserts treat a pre-set id as an existing row (UPDATE), so the id is left to the store
            var created = EntitiesDashboardEntityMaterializer.Create(entity, request, source is not ActiveEntityEntitiesDashboardSource);
            if (created.IsFailure)
            {
                return this.Map(http, entity, "create", created.Errors);
            }

            var inserted = await source.InsertAsync(http.RequestServices, created.Value, cancellationToken).ConfigureAwait(false);
            if (inserted.IsFailure)
            {
                return this.Map(http, entity, "create", inserted.Errors);
            }

            var id = EntitiesDashboardIdConverter.Format(entity.IdProperty?.Getter?.Invoke(inserted.Value ?? created.Value));
            EntitiesDashboardLog.Created(this.logger, entity.Name, id, GetUserName(http));

            return EntitiesDashboardOutcome.Ok("The record was created.", new { id }, StatusCodes.Status201Created);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return this.Unexpected(http, entity, "create", ex);
        }
    }

    /// <summary>Updates a record, or returns the diff only when <paramref name="preview"/> is set.</summary>
    /// <param name="http">The HTTP context.</param>
    /// <param name="body">The JSON body.</param>
    /// <param name="preview">True to compute the diff without writing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome; <c>payload</c> holds <c>id</c>, <c>changed</c> and the <c>diff</c> entries.</returns>
    public async Task<EntitiesDashboardOutcome> UpdateAsync(HttpContext http, JsonElement body, bool preview, CancellationToken cancellationToken)
    {
        var context = Resolve(http, body, out var outcome);
        if (outcome != null)
        {
            return outcome;
        }

        var (entity, source, request) = context.Value;
        if (!entity.CanUpdate)
        {
            return EntitiesDashboardOutcome.Failed(entity.CapabilityNote ?? "Update is not available for this entity.");
        }

        var id = EntitiesDashboardIdConverter.Parse(entity.IdType, request.Id);
        if (id.IsFailure)
        {
            return EntitiesDashboardOutcome.Failed("The id is not valid.");
        }

        try
        {
            var found = await source.FindOneAsync(http.RequestServices, id.Value, cancellationToken).ConfigureAwait(false);
            if (found.IsFailure || found.Value == null)
            {
                return found.IsFailure ? this.Map(http, entity, "update", found.Errors) : EntitiesDashboardOutcome.NotFound();
            }

            var instance = found.Value;
            if (IsSoftDeleted(instance))
            {
                return EntitiesDashboardOutcome.Failed("The record is deleted and cannot be changed.", StatusCodes.Status409Conflict);
            }

            if (entity.IsConcurrency)
            {
                if (string.IsNullOrWhiteSpace(request.ConcurrencyVersion))
                {
                    return EntitiesDashboardOutcome.Failed("The concurrency version of the loaded record is required.");
                }

                var stored = entity.Properties.First(p => p.IsConcurrency).Getter(instance);
                if (!string.Equals(EntitiesDashboardValueConverter.Format(stored), request.ConcurrencyVersion.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return EntitiesDashboardOutcome.Conflict();
                }
            }

            var changes = EntitiesDashboardEntityMaterializer.Update(entity, instance, request);
            if (changes.IsFailure)
            {
                return this.Map(http, entity, "update", changes.Errors);
            }

            var diff = EntitiesDashboardDiff.Compute(changes.Value);
            if (changes.Value.Count == 0)
            {
                return EntitiesDashboardOutcome.Ok(
                    "There are no changes to save.",
                    new EntitiesDashboardUpdatePayload(request.Id, false, diff.Entries));
            }

            if (preview)
            {
                return EntitiesDashboardOutcome.Ok(
                    "Review the changes before saving.",
                    new EntitiesDashboardUpdatePayload(request.Id, true, diff.Entries));
            }

            // let the store enforce the version the user loaded, closing the window between the pre-check and the save
            if (entity.IsConcurrency && instance is IConcurrency versioned && Guid.TryParse(request.ConcurrencyVersion?.Trim(), out var expectedVersion))
            {
                versioned.ConcurrencyVersion = expectedVersion;
            }

            var updated = await source.UpdateAsync(http.RequestServices, instance, cancellationToken).ConfigureAwait(false);
            if (updated.IsFailure)
            {
                return this.Map(http, entity, "update", updated.Errors);
            }

            EntitiesDashboardLog.Updated(this.logger, entity.Name, request.Id, GetUserName(http));

            return EntitiesDashboardOutcome.Ok(
                "The record was saved.",
                new EntitiesDashboardUpdatePayload(request.Id, true, diff.Entries));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return this.Unexpected(http, entity, "update", ex);
        }
    }

    /// <summary>Deletes one record.</summary>
    /// <param name="http">The HTTP context.</param>
    /// <param name="body">The JSON body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<EntitiesDashboardOutcome> DeleteAsync(HttpContext http, JsonElement body, CancellationToken cancellationToken)
    {
        var context = Resolve(http, body, out var outcome);
        if (outcome != null)
        {
            return outcome;
        }

        var (entity, source, request) = context.Value;
        if (!entity.CanDelete)
        {
            return EntitiesDashboardOutcome.Failed(entity.CapabilityNote ?? "Delete is not available for this entity.");
        }

        var id = EntitiesDashboardIdConverter.Parse(entity.IdType, request.Id);
        if (id.IsFailure)
        {
            return EntitiesDashboardOutcome.Failed("The id is not valid.");
        }

        try
        {
            var deleted = await source.DeleteAsync(http.RequestServices, id.Value, cancellationToken).ConfigureAwait(false);
            if (deleted.IsFailure)
            {
                return this.Map(http, entity, "delete", deleted.Errors);
            }

            EntitiesDashboardLog.Deleted(this.logger, entity.Name, request.Id, GetUserName(http));

            return EntitiesDashboardOutcome.Ok("The record was deleted.", new { id = request.Id });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return this.Unexpected(http, entity, "delete", ex);
        }
    }

    /// <summary>Deletes several records one after the other without a transaction.</summary>
    /// <param name="http">The HTTP context.</param>
    /// <param name="body">The JSON body (<c>entity</c>, optional <c>source</c>, <c>ids</c>).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome; <c>payload</c> holds <c>deleted</c> (ids) and <c>failed</c> (<c>id</c>, <c>message</c>).</returns>
    public async Task<EntitiesDashboardOutcome> BulkDeleteAsync(HttpContext http, JsonElement body, CancellationToken cancellationToken)
    {
        var context = Resolve(http, body, out var outcome);
        if (outcome != null)
        {
            return outcome;
        }

        var (entity, source, _) = context.Value;
        if (!entity.CanDelete)
        {
            return EntitiesDashboardOutcome.Failed(entity.CapabilityNote ?? "Delete is not available for this entity.");
        }

        var ids = new List<string>();
        if (body.TryGetProperty("ids", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            ids.AddRange(list.EnumerateArray()
                .Select(EntitiesDashboardWriteRequest.AsText)
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Distinct(StringComparer.Ordinal));
        }

        var options = EntitiesDashboardOptionsAccessor.Get(http.RequestServices);
        if (ids.Count == 0)
        {
            return EntitiesDashboardOutcome.Failed("Select at least one record.");
        }

        if (ids.Count > options.MaxBulkSelection)
        {
            return EntitiesDashboardOutcome.Failed($"At most {options.MaxBulkSelection} records can be deleted at once.");
        }

        var parsed = ids.Select(i => (Text: i, Id: EntitiesDashboardIdConverter.Parse(entity.IdType, i))).ToList();
        var deleted = new List<string>();
        var failed = new List<object>();

        foreach (var (text, id) in parsed)
        {
            if (id.IsFailure)
            {
                failed.Add(new { id = text, message = "The id is not valid." });

                continue;
            }

            try
            {
                var result = await source.DeleteAsync(http.RequestServices, id.Value, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    deleted.Add(text);
                }
                else
                {
                    var mapped = this.Map(http, entity, "bulk-delete", result.Errors);
                    failed.Add(new { id = text, message = mapped.Message });
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var mapped = this.Unexpected(http, entity, "bulk-delete", ex);
                failed.Add(new { id = text, message = mapped.Message });
            }
        }

        EntitiesDashboardLog.BulkDeleted(this.logger, entity.Name, deleted.Count, failed.Count, GetUserName(http));

        return EntitiesDashboardOutcome.Ok(
            failed.Count == 0
                ? $"Deleted {deleted.Count} record(s)."
                : $"Deleted {deleted.Count} of {ids.Count} record(s); {failed.Count} failed.",
            new { deleted, failed });
    }

    private static (EntitiesDashboardEntity, IEntitiesDashboardSource, EntitiesDashboardWriteRequest)? Resolve(
        HttpContext http,
        JsonElement body,
        out EntitiesDashboardOutcome outcome)
    {
        outcome = null;
        var request = EntitiesDashboardWriteRequest.From(body);
        if (request == null)
        {
            outcome = EntitiesDashboardOutcome.Failed("The request body is not valid.");

            return null;
        }

        var entity = request.Entity == null ? null : http.RequestServices.GetService<EntitiesDashboardCatalog>()?.Find(request.Entity);
        if (entity == null)
        {
            outcome = EntitiesDashboardOutcome.Failed("The entity was not found.", StatusCodes.Status404NotFound);

            return null;
        }

        var source = EntitiesDashboardQueryService.ResolveSource(entity, request.Source);
        if (source == null)
        {
            outcome = EntitiesDashboardOutcome.Failed("The source is not available for this entity.");

            return null;
        }

        return (entity, source, request);
    }

    internal static string GetUserName(HttpContext http) =>
        http.User?.Identity?.Name
        ?? http.User?.FindFirst("name")?.Value
        ?? http.User?.FindFirst("preferred_username")?.Value
        ?? http.User?.FindFirst("sub")?.Value
        ?? "anonymous";

    private EntitiesDashboardOutcome Map(HttpContext http, EntitiesDashboardEntity entity, string operation, IEnumerable<IResultError> errors)
    {
        var list = errors?.ToList() ?? [];
        var correlationId = EntitiesDashboardOutcome.GetCorrelationId(http);
        var outcome = EntitiesDashboardOutcome.FromErrors(list, correlationId, entity.Properties.Select(p => p.Name));
        if (outcome.CorrelationId != null)
        {
            EntitiesDashboardLog.FailedResult(this.logger, entity.Name, operation, correlationId, list.FirstOrDefault()?.GetType().Name, list.FirstOrDefault()?.Message);
        }

        return outcome;
    }

    private EntitiesDashboardOutcome Unexpected(HttpContext http, EntitiesDashboardEntity entity, string operation, Exception exception)
    {
        var correlationId = EntitiesDashboardOutcome.GetCorrelationId(http);
        EntitiesDashboardLog.Failed(this.logger, exception, entity.Name, operation, correlationId);

        return EntitiesDashboardOutcome.FromDatabaseException(exception, correlationId, entity.Properties.Select(p => p.Name))
            ?? EntitiesDashboardOutcome.ServerError(correlationId);
    }
}
