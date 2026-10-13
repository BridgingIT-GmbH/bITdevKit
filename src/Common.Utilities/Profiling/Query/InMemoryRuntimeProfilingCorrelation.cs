// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

sealed partial class InMemoryRuntimeProfilingStore
{
    /// <inheritdoc />
    public Task<IResult<RuntimeProfilingCorrelationSelection>> QueryCorrelationAsync(RuntimeProfilingCorrelationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var valid = OperationRuntimeCorrelationService.Validate(request);
        if (valid.IsFailure) { return Task.FromResult<IResult<RuntimeProfilingCorrelationSelection>>(Result<RuntimeProfilingCorrelationSelection>.Failure(valid)); }

        lock (this.sync)
        {
            if (!this.nodes.TryGetValue(request.Node.Identity.Id, out var node) || !OperationRuntimeCorrelationService.MatchesNode(node, request.Node))
            { return Task.FromResult<IResult<RuntimeProfilingCorrelationSelection>>(Result<RuntimeProfilingCorrelationSelection>.Success(new())); }

            var participations = this.participations.Values.Where(value => value.NodeId == node.Identity.Id
                && value.JoinedUtc <= request.ToUtc && (value.CompletedUtc is null || value.CompletedUtc >= request.FromUtc))
                .Take(request.MaximumSnapshots + 1).ToArray();
            if (participations.Length > request.MaximumSnapshots) { return Limit(); }

            var snapshots = new List<RuntimeProfilingSnapshot>();
            var sessions = new List<RuntimeProfilingSession>();
            foreach (var participation in participations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!this.sessions.TryGetValue(participation.SessionId, out var session)) { continue; }

                sessions.Add(session);
                var source = this.snapshots.Values.Where(value => value.NodeId == participation.NodeId && value.SessionId == participation.SessionId
                    && value.TimestampUtc >= participation.JoinedUtc && (participation.CompletedUtc is null || value.TimestampUtc <= participation.CompletedUtc));
                var inside = source.Where(value => OperationRuntimeCorrelationService.Relation(value.TimestampUtc, request.FromUtc, request.ToUtc) == "Inside")
                    .Take(request.MaximumSnapshots - snapshots.Count + 1).ToArray();
                snapshots.AddRange(inside);
                if (snapshots.Count > request.MaximumSnapshots) { return Limit(); }

                var before = source.Where(value => value.TimestampUtc < request.FromUtc).MaxBy(value => value.TimestampUtc);
                var after = source.Where(value => OperationRuntimeCorrelationService.Relation(value.TimestampUtc, request.FromUtc, request.ToUtc) == "After").MinBy(value => value.TimestampUtc);
                if (before is not null) { snapshots.Add(before); }

                if (after is not null) { snapshots.Add(after); }

                if (snapshots.Count > request.MaximumSnapshots) { return Limit(); }
            }

            return Task.FromResult<IResult<RuntimeProfilingCorrelationSelection>>(Result<RuntimeProfilingCorrelationSelection>.Success(new()
            {
                Snapshots = snapshots.ToArray(), Sessions = sessions.ToArray(), Participations = participations,
            }));
        }

        static Task<IResult<RuntimeProfilingCorrelationSelection>> Limit() => Task.FromResult<IResult<RuntimeProfilingCorrelationSelection>>(Result<RuntimeProfilingCorrelationSelection>.Failure(new ProfilingQueryLimitError()));
    }
}
