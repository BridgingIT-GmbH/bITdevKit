// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

public sealed partial class EntityFrameworkProfilingStorageProvider<TContext> : IProfilingStorageProvider, IOperationProfilingStore
{
    /// <summary>Gets operation persistence and server-side history queries.</summary>
    /// <example><code>var record = await provider.Operations.FindAsync(id);</code></example>
    public IOperationProfilingStore Operations => this;

    /// <inheritdoc />
    public Task<IResult<OperationProfilingPage>> QueryAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) =>
        this.unitOfWork.ReadAsync(async (context, token) =>
        {
            var prepared = await this.PrepareQueryAsync(context, query, "records", token).ConfigureAwait(false);
            if (prepared.IsFailure) { return Result<OperationProfilingPage>.Failure(prepared); }

            var selection = prepared.Value;
            if (selection.Empty) { return Result<OperationProfilingPage>.Success(new()); }

            var total = await selection.Roots.LongCountAsync(token).ConfigureAwait(false);
            var ids = await EntityFrameworkProfilingQuerySql<TContext>.PageIdsAsync(context, selection.Roots, selection.Query.View, selection.Cursor, selection.Query.PageSize + 1, token).ConfigureAwait(false);
            var more = ids.Length > selection.Query.PageSize;
            var records = await this.LoadRecordsAsync(context, ids.Take(selection.Query.PageSize).ToArray(), token).ConfigureAwait(false);
            if (!await ValidRevisionAsync(context, selection.Boundary, token).ConfigureAwait(false))
            {
                return Result<OperationProfilingPage>.Failure(new ProfilingQueryBoundaryError());
            }

            return Result<OperationProfilingPage>.Success(new()
            {
                Records = records, TotalCount = total, Boundary = selection.Boundary, HasMore = more,
                NextCursor = more ? Cursor(selection, records[^1], "records") : null,
            });
        }, cancellationToken);

    /// <inheritdoc />
    public Task<IResult<OperationProfilingGroupPage>> GroupAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) =>
        this.unitOfWork.ReadAsync(async (context, token) =>
        {
            var prepared = await this.PrepareQueryAsync(context, query, "groups", token).ConfigureAwait(false);
            if (prepared.IsFailure) { return Result<OperationProfilingGroupPage>.Failure(prepared); }

            var selection = prepared.Value;
            if (selection.Empty) { return Result<OperationProfilingGroupPage>.Success(new()); }

            var nodeIds = EntityFrameworkOperationProfilingFilters.Apply(context, selection.Query with { NodeId = null }, selection.Boundary.CommitWatermark).Select(root => root.NodeId).Distinct();
            var nodes = await context.Set<ProfilingNodeEntity>().AsNoTracking().Where(node => nodeIds.Contains(node.Id))
                .OrderBy(node => node.HostName).ThenBy(node => node.Key).Take(this.queryOptions.MaximumNodeChoices + 1).ToArrayAsync(token).ConfigureAwait(false);
            var count = await EntityFrameworkProfilingQuerySql<TContext>.CountsAsync(context, selection.Roots, selection.Query, token).ConfigureAwait(false);
            IReadOnlyList<OperationProfilingRecord> visible = [];
            EntityFrameworkProfilingQuerySql<TContext>.GroupRow[] rows;
            bool more;
            string next;
            if (selection.Query.View == OperationProfilingView.ByCount)
            {
                var page = await EntityFrameworkProfilingQuerySql<TContext>.GroupsAsync(context, selection.Roots, selection.Query, selection.Cursor, null, token).ConfigureAwait(false);
                more = page.Length > selection.Query.PageSize;
                rows = page.Take(selection.Query.PageSize).ToArray();
                next = more ? selection.Codec.Encode(new(rows[^1].Count, rows[^1].Key, "groups", selection.Boundary)) : null;
            }
            else
            {
                var ids = await EntityFrameworkProfilingQuerySql<TContext>.PageIdsAsync(context, selection.Roots, selection.Query.View, selection.Cursor, selection.Query.PageSize + 1, token).ConfigureAwait(false);
                more = ids.Length > selection.Query.PageSize;
                visible = await this.LoadRecordsAsync(context, ids.Take(selection.Query.PageSize).ToArray(), token).ConfigureAwait(false);
                var keys = visible.Select(record => GroupKey(record, selection.Query)).Distinct(StringComparer.Ordinal).ToArray();
                rows = keys.Length == 0 ? [] : await EntityFrameworkProfilingQuerySql<TContext>.GroupsAsync(context, selection.Roots, selection.Query, null, keys, token).ConfigureAwait(false);
                next = more ? Cursor(selection, visible[^1], "groups") : null;
            }

            var labelIds = rows.SelectMany(row => new[] { row.LabelId, row.RepresentativeId }).Distinct().ToArray();
            var labels = (await this.LoadRecordsAsync(context, labelIds, token).ConfigureAwait(false)).ToDictionary(record => record.Id);
            if (!await ValidRevisionAsync(context, selection.Boundary, token).ConfigureAwait(false))
            {
                return Result<OperationProfilingGroupPage>.Failure(new ProfilingQueryBoundaryError());
            }

            var indexed = rows.ToDictionary(row => row.Key, StringComparer.Ordinal);
            var orderedKeys = selection.Query.View == OperationProfilingView.ByCount ? rows.Select(row => row.Key)
                : visible.Select(record => GroupKey(record, selection.Query)).Distinct(StringComparer.Ordinal);
            var groups = orderedKeys.Select(key =>
            {
                var row = indexed[key];
                var label = labels[row.LabelId];
                var occurrences = visible.Where(record => GroupKey(record, selection.Query) == key).ToArray();
                var representative = occurrences.Length > 0 ? occurrences[0] : labels[row.RepresentativeId];
                var dimensions = selection.Query.GroupingDimensions.Select(name => label.Dimensions.FirstOrDefault(dimension => ProfilingKeyComparer.Canonicalize(dimension.Key) == name)).Where(dimension => dimension is not null).ToArray();
                return new OperationProfilingGroup
                {
                    ComparisonKey = key, Key = label.Key, Kind = label.Kind, HttpMethod = selection.Query.GroupByHttpMethod ? label.Http?.Method : null,
                    Dimensions = Array.AsReadOnly(dimensions), Count = row.Count, TotalDuration = row.Unavailable ? TimeSpan.Zero : TimeSpan.FromTicks(row.DurationTicks),
                    DurationUnavailable = row.Unavailable, MinimumDuration = TimeSpan.FromTicks(row.MinimumTicks), MaximumDuration = TimeSpan.FromTicks(row.MaximumTicks),
                    LatestCompletedUtc = new(row.LatestTicks, TimeSpan.Zero), Representative = representative, Occurrences = Array.AsReadOnly(occurrences),
                };
            }).ToArray();
            if (!await ValidRevisionAsync(context, selection.Boundary, token).ConfigureAwait(false))
            {
                return Result<OperationProfilingGroupPage>.Failure(new ProfilingQueryBoundaryError());
            }

            return Result<OperationProfilingGroupPage>.Success(new()
            {
                Groups = Array.AsReadOnly(groups), TotalOperationCount = count.Operations, TotalGroupCount = count.Groups,
                Boundary = selection.Boundary, HasMore = more, NextCursor = next,
                Nodes = Array.AsReadOnly(nodes.Take(this.queryOptions.MaximumNodeChoices).Select(ProfilingEntityMapper.ToModel).ToArray()), NodesTruncated = nodes.Length > this.queryOptions.MaximumNodeChoices,
            });
        }, cancellationToken);

    /// <inheritdoc />
    public Task<IResult<OperationProfilingAnalysisSelection>> SelectAnalysisAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) =>
        this.unitOfWork.ReadAsync(async (context, token) =>
        {
            var prepared = await this.PrepareQueryAsync(context, query, "analysis", token).ConfigureAwait(false);
            if (prepared.IsFailure) { return Result<OperationProfilingAnalysisSelection>.Failure(prepared); }

            var selection = prepared.Value;
            if (selection.Empty) { return Result<OperationProfilingAnalysisSelection>.Success(new()); }

            if (await selection.Roots.LongCountAsync(token).ConfigureAwait(false) > selection.Query.MaximumAnalysisCount)
            {
                return Result<OperationProfilingAnalysisSelection>.Failure(new ProfilingQueryLimitError());
            }

            var entities = await selection.Roots.Take(selection.Query.MaximumAnalysisCount + 1).ToArrayAsync(token).ConfigureAwait(false);
            if (entities.Length > selection.Query.MaximumAnalysisCount)
            {
                return Result<OperationProfilingAnalysisSelection>.Failure(new ProfilingQueryLimitError());
            }

            var records = Array.AsReadOnly(entities.Select(entity => ProfilingEntityMapper.ToOperationModel(entity, this.options)).ToArray());
            if (!await ValidRevisionAsync(context, selection.Boundary, token).ConfigureAwait(false))
            {
                return Result<OperationProfilingAnalysisSelection>.Failure(new ProfilingQueryBoundaryError());
            }

            return Result<OperationProfilingAnalysisSelection>.Success(new() { Records = records, Boundary = selection.Boundary });
        }, cancellationToken);

    private async Task<Result<QuerySelection>> PrepareQueryAsync(TContext context, OperationProfilingQuery query, string target, CancellationToken token)
    {
        try
        {
            this.ConfigureQueryTimeout(context);
            var state = await context.Set<ProfilingStoreStateEntity>().AsNoTracking().SingleOrDefaultAsync(state => state.Id == 1, token).ConfigureAwait(false);
            var codec = new ProfilingOperationQueryCodec(this.options, this.queryOptions, this.clock, state?.QuerySecret ?? new byte[32]);
            if (state is null)
            {
                if (query?.Cursor is not null || query?.Boundary is not null)
                {
                    return Result<QuerySelection>.Failure(new ProfilingQueryBoundaryError());
                }

                var emptyQuery = codec.Normalize(query, null);
                return await context.Set<OperationProfilingEntity>().AnyAsync(token).ConfigureAwait(false)
                    ? Result<QuerySelection>.Failure(new ProfilingUnavailableError("The original profiling coordination state is absent."))
                    : Result<QuerySelection>.Success(new(emptyQuery, null, null, codec, null, true));
            }

            var cursor = query?.Cursor is not null ? codec.Decode(query.Cursor) : null;
            if (cursor is not null && cursor.Target != target || query?.Boundary is not null && cursor is not null && query.Boundary != cursor.Boundary)
            {
                return Result<QuerySelection>.Failure(new ProfilingQueryBoundaryError());
            }

            var normalized = codec.Normalize(query, cursor?.Boundary ?? query?.Boundary);
            var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(normalized with { Boundary = null, Cursor = null, PageSize = 0, MaximumAnalysisCount = 0 }))));
            var boundary = cursor?.Boundary ?? query.Boundary;
            var utc = await EntityFrameworkProfilingUnitOfWork<TContext>.ProviderUtcAsync(context, token).ConfigureAwait(false);
            var revision = checked(state.DeletionRevision + await context.Set<ProfilingRuntimeGateEntity>().AsNoTracking().Where(gate => gate.Id == 1).Select(gate => gate.DeletionRevision).SingleOrDefaultAsync(token).ConfigureAwait(false));
            if (boundary is null)
            {
                boundary = new()
                {
                    StoreEpoch = state.StoreEpoch, CommitWatermark = state.PublicationWatermark, DeletionRevision = revision, FilterFingerprint = fingerprint,
                    FromUtc = normalized.FromUtc, ToUtc = normalized.ToUtc, ExpiresUtc = utc.Add(this.queryOptions.BoundaryLifetime),
                };
                boundary = boundary with { Signature = codec.SignBoundary(boundary) };
            }
            else if (boundary.StoreEpoch != state.StoreEpoch || boundary.CommitWatermark > state.PublicationWatermark || boundary.CommitWatermark < 0
                || boundary.DeletionRevision != revision || boundary.ExpiresUtc <= utc || boundary.FilterFingerprint != fingerprint
                || !ProfilingOperationQueryCodec.TagsEqual(boundary.Signature, codec.SignBoundary(boundary with { Signature = null })))
            {
                return Result<QuerySelection>.Failure(new ProfilingQueryBoundaryError());
            }

            if (cursor is not null && normalized.View != OperationProfilingView.ByCount && !Guid.TryParseExact(cursor.Identity, "N", out _))
            {
                return Result<QuerySelection>.Failure(new ProfilingQueryBoundaryError());
            }

            return Result<QuerySelection>.Success(new(normalized, boundary, cursor, codec,
                EntityFrameworkOperationProfilingFilters.Apply(context, normalized, boundary.CommitWatermark), false));
        }
        catch (ArgumentException)
        {
            return Result<QuerySelection>.Failure(new ProfilingValidationError("Profiling filters require bounded keys, typed values, UTC bounds and supported selectors."));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return Result<QuerySelection>.Failure(new ProfilingQueryBoundaryError());
        }
    }

    private async Task<IReadOnlyList<OperationProfilingRecord>> LoadRecordsAsync(TContext context, Guid[] ids, CancellationToken token)
    {
        if (ids.Length == 0) { return Array.Empty<OperationProfilingRecord>(); }

        var entities = await context.Set<OperationProfilingEntity>().AsNoTracking().Where(root => ids.Contains(root.Id)).ToArrayAsync(token).ConfigureAwait(false);
        var index = entities.ToDictionary(entity => entity.Id);
        // Missing graphs are reported by the final deletion-revision check, never filled with placeholders.
        return Array.AsReadOnly(ids.Where(index.ContainsKey).Select(id => ProfilingEntityMapper.ToOperationModel(index[id], this.options)).ToArray());
    }

    private static async Task<bool> ValidRevisionAsync(TContext context, ProfilingQueryBoundary boundary, CancellationToken token)
    {
        var state = await context.Set<ProfilingStoreStateEntity>().AsNoTracking().Where(state => state.Id == 1).Select(state => new { state.StoreEpoch, state.DeletionRevision }).SingleOrDefaultAsync(token).ConfigureAwait(false);
        var runtime = await context.Set<ProfilingRuntimeGateEntity>().AsNoTracking().Where(gate => gate.Id == 1).Select(gate => gate.DeletionRevision).SingleOrDefaultAsync(token).ConfigureAwait(false);
        return state is not null && state.StoreEpoch == boundary.StoreEpoch && checked(state.DeletionRevision + runtime) == boundary.DeletionRevision;
    }

    private static string Cursor(QuerySelection selection, OperationProfilingRecord record, string target) => selection.Codec.Encode(new(
        selection.Query.View == OperationProfilingView.Recent ? record.CompletedUtc.UtcTicks : record.Duration.Ticks, record.Id.ToString("N"), target, selection.Boundary));

    private static string GroupKey(OperationProfilingRecord record, OperationProfilingQuery query)
    {
        var bytes = new List<byte>();
        bytes.AddRange(ProfilingOperationComparisons.BaseGroup(record.Kind, record.Key));
        if (query.GroupByHttpMethod) { bytes.AddRange(ProfilingOperationComparisons.MethodPart(record.Http?.Method)); }

        foreach (var name in query.GroupingDimensions)
        {
            bytes.AddRange(ProfilingOperationComparisons.Part(name));
            bytes.AddRange(ProfilingOperationComparisons.ValuePart(record.Dimensions.FirstOrDefault(dimension => ProfilingKeyComparer.Canonicalize(dimension.Key) == name)?.Value));
        }

        return Encoding.BigEndianUnicode.GetString(bytes.ToArray());
    }

    private sealed record QuerySelection(OperationProfilingQuery Query, ProfilingQueryBoundary Boundary, ProfilingPageCursor Cursor,
        ProfilingOperationQueryCodec Codec, IQueryable<OperationProfilingEntity> Roots, bool Empty);
}
