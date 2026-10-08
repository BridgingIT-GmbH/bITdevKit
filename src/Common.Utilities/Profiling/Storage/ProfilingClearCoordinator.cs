// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

// All state transitions share the provider's append/runtime synchronization boundary.
internal sealed class ProfilingClearCoordinator(ProfilingStorageOptions options, string scope)
{
    private readonly List<Clear> clears = [];
    public Clear Current => this.clears.FirstOrDefault(c => c.State is ProfilingClearState.Preparing or ProfilingClearState.Applying);
    public IReadOnlyList<Clear> Clears => this.clears;

    internal void Restore(IReadOnlyList<Clear> stored)
    {
        if (stored is null || stored.Count > options.MaximumClearFences || this.clears.Count != 0
            || stored.Any(clear => clear is null) || stored.Count(clear => clear.State is ProfilingClearState.Preparing or ProfilingClearState.Applying) > 1
            || stored.Select(clear => clear.Id).Distinct().Count() != stored.Count)
        {
            throw new ArgumentException("A bounded serialized clear coordination snapshot is required.");
        }

        foreach (var clear in stored)
        {
            if (clear is null || clear.Id == Guid.Empty || Validate(clear.Selection) is not null || !Enum.IsDefined(clear.State)
                || clear.Eligible.Count > options.MaximumWriterLeases || clear.Cutoffs.Count > clear.Eligible.Count
                || clear.PreparedUtc.Offset != TimeSpan.Zero || clear.DeadlineUtc.Offset != TimeSpan.Zero || clear.DeadlineUtc <= clear.PreparedUtc
                || clear.Generation < 0 || clear.Cutoffs.Any(cutoff => cutoff.Value < 0 || !clear.Eligible.ContainsKey(cutoff.Key)))
            {
                throw new ArgumentException("A persisted clear contains invalid eligibility or lifecycle state.");
            }

        }

        this.clears.AddRange(stored);
    }

    public Result<Clear> Prepare(ProfilingClearRequest request, ProfilingWriterRegistry writers, DateTimeOffset utc, bool runtimeActive)
    {
        var error = Validate(request);
        if (error is not null)
        {
            return Result<Clear>.Failure(error);
        }

        this.Prune(writers, utc);
        if (this.Current is not null || this.clears.Count >= options.MaximumClearFences)
        {
            return Result<Clear>.Failure(new ProfilingBusyError("Profiling clear coordination is at capacity or another clear is active."));
        }

        if (request.DataSet != ProfilingDataSet.Operations && runtimeActive)
        {
            return Result<Clear>.Failure(new ProfilingBusyError("An active Runtime session must end before clearing Runtime history."));
        }

        var boundary = request.DataSet == ProfilingDataSet.Runtime ? (0L, (IReadOnlyList<ProfilingWriterLease>)Array.Empty<ProfilingWriterLease>()) : writers.Prepare(utc);
        var clear = new Clear(Guid.NewGuid(), request with { }, utc, utc.Add(options.ClearPreparationTimeout), boundary.Item1, boundary.Item2);
        this.clears.Add(clear);
        return Result<Clear>.Success(clear);
    }

    public IReadOnlyList<ProfilingPendingClear> Pending(ProfilingWriterLease lease, DateTimeOffset utc) => Array.AsReadOnly(
        this.clears.Where(c => c.State == ProfilingClearState.Preparing && c.DeadlineUtc > utc && c.Eligible.ContainsKey(lease.WriterId))
            .Select(c => new ProfilingPendingClear { Id = c.Id, Selection = c.Selection, PreparedUtc = c.PreparedUtc, DeadlineUtc = c.DeadlineUtc }).ToArray());

    public Result<ProfilingClearAcknowledgement> Acknowledge(ProfilingClearAcknowledgement request, ProfilingWriterRegistry writers, DateTimeOffset utc)
    {
        if (request?.Lease is null || request.CompletionCutoff < 0)
        {
            return Result<ProfilingClearAcknowledgement>.Failure(new ProfilingValidationError("An original lease and nonnegative clear cutoff are required."));
        }

        var clear = this.clears.SingleOrDefault(c => c.Id == request.ClearId);
        if (clear is null || !clear.Eligible.TryGetValue(request.Lease.WriterId, out var eligible) || eligible.Token != request.Lease.Token
            || eligible.StoreEpoch != request.Lease.StoreEpoch || eligible.Generation != request.Lease.Generation || eligible.NodeId != request.Lease.NodeId)
        {
            return Result<ProfilingClearAcknowledgement>.Failure(new ProfilingInvalidStateError("The writer is not eligible for this clear."));
        }

        if (clear.Cutoffs.TryGetValue(request.Lease.WriterId, out var original))
        {
            return Result<ProfilingClearAcknowledgement>.Success(request with { CompletionCutoff = original });
        }

        if (clear.State != ProfilingClearState.Preparing || clear.DeadlineUtc <= utc || writers.Find(request.Lease, utc) is null)
        {
            return Result<ProfilingClearAcknowledgement>.Failure(new ProfilingInvalidStateError("The clear preparation or writer has expired."));
        }

        clear.Cutoffs.Add(request.Lease.WriterId, request.CompletionCutoff);
        clear.Pulse();
        return Result<ProfilingClearAcknowledgement>.Success(request with { });
    }

    public bool CanSeal(Clear clear, ProfilingWriterRegistry writers, DateTimeOffset utc) =>
        clear.State == ProfilingClearState.Preparing && clear.DeadlineUtc > utc
        && clear.Eligible.Values.All(lease => clear.Cutoffs.ContainsKey(lease.WriterId) || writers.Find(lease, utc) is null);

    public void Seal(Clear clear, DateTimeOffset utc)
    {
        clear.State = ProfilingClearState.Applying;
        clear.SealedUtc = utc;
        clear.Pulse();
    }

    public bool Fenced(ProfilingWriteEnvelope envelope) => this.clears.Any(c => c.SealedUtc is not null && c.Matches(envelope));

    public void Prune(ProfilingWriterRegistry writers, DateTimeOffset utc)
    {
        this.clears.RemoveAll(c => c.State == ProfilingClearState.Failed && c.SealedUtc is null
            || c.State == ProfilingClearState.Completed && c.Eligible.Values.All(lease =>
                writers.Find(lease, utc) is not { } writer || !c.Cutoffs.TryGetValue(lease.WriterId, out var cutoff) || writer.Lease.SettledThrough >= cutoff));
    }

    public ProfilingClearResult Result(Clear clear) => new(clear.RemovedRuntimeSessions, clear.RemovedSnapshots)
    {
        ClearId = clear.Id, DataSet = clear.Selection.DataSet, FromUtc = clear.Selection.FromUtc, ToUtc = clear.Selection.ToUtc,
        PreparedUtc = clear.PreparedUtc, SealedUtc = clear.SealedUtc, CompletedUtc = clear.CompletedUtc, ProviderScope = scope,
        State = clear.State, RemovedOperationCount = clear.RemovedOperations, RemovedSegmentSummaryCount = clear.RemovedSummaries,
        HasRemainingWork = clear.State is ProfilingClearState.Preparing or ProfilingClearState.Applying,
    };

    public static IResultError Validate(ProfilingClearRequest request) => request is null || !Enum.IsDefined(request.DataSet)
        || request.FromUtc.HasValue != request.ToUtc.HasValue || request.FromUtc?.Offset != null && request.FromUtc.Value.Offset != TimeSpan.Zero
        || request.ToUtc?.Offset != null && request.ToUtc.Value.Offset != TimeSpan.Zero || request.FromUtc >= request.ToUtc
        ? new ProfilingValidationError("Clear selection requires a valid dataset and both UTC bounds with FromUtc < ToUtc, or neither bound.") : null;

    internal sealed class Clear(Guid id, ProfilingClearRequest selection, DateTimeOffset preparedUtc, DateTimeOffset deadlineUtc,
        long generation, IReadOnlyList<ProfilingWriterLease> eligible)
    {
        private TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Guid Id { get; } = id;
        public ProfilingClearRequest Selection { get; } = selection;
        public DateTimeOffset PreparedUtc { get; } = preparedUtc;
        public DateTimeOffset DeadlineUtc { get; } = deadlineUtc;
        public long Generation { get; } = generation;
        public IReadOnlyDictionary<Guid, ProfilingWriterLease> Eligible { get; } = eligible.ToDictionary(l => l.WriterId);
        public Dictionary<Guid, long> Cutoffs { get; } = [];
        public ProfilingClearState State { get; set; } = ProfilingClearState.Preparing;
        public DateTimeOffset? SealedUtc { get; set; }
        public DateTimeOffset? CompletedUtc { get; set; }
        public long RemovedOperations { get; set; }
        public long RemovedSummaries { get; set; }
        public int RemovedRuntimeSessions { get; set; }
        public long RemovedSnapshots { get; set; }
        public Task Changed => this.changed.Task;

        public void Pulse()
        {
            var previous = this.changed;
            this.changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            previous.TrySetResult();
        }

        public bool Matches(ProfilingWriteEnvelope envelope) => this.Selection.DataSet != ProfilingDataSet.Runtime
            && envelope.Lease.Generation <= this.Generation && InRange(this.Selection, envelope.Record.CompletedUtc)
            && (!this.Cutoffs.TryGetValue(envelope.Lease.WriterId, out var cutoff) || envelope.CompletionSequence <= cutoff);
    }

    public static bool InRange(ProfilingClearRequest request, DateTimeOffset utc) =>
        !request.FromUtc.HasValue || utc >= request.FromUtc.Value && utc < request.ToUtc.Value;
}
