// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

// The provider's synchronization boundary owns every registry call.
internal sealed class ProfilingWriterRegistry(Guid epoch, int capacity, Action<Guid> released = null)
{
    private readonly Dictionary<Guid, Writer> writers = [];
    private readonly Dictionary<Guid, Guid> attempts = [];
    public long Generation { get; private set; }
    public int Count => this.writers.Count;

    internal IReadOnlyList<ProfilingWriterRegistrationState> Snapshot() => Array.AsReadOnly(this.writers.Values.Select(writer =>
        new ProfilingWriterRegistrationState(writer.AttemptId, writer.Lease, writer.Retired)).ToArray());

    internal void Restore(long generation, IReadOnlyList<ProfilingWriterRegistrationState> registrations)
    {
        if (generation < 0 || registrations is null || registrations.Count > capacity || this.writers.Count != 0)
        {
            throw new ArgumentException("A bounded original writer registry snapshot is required.");
        }

        var identities = new HashSet<Guid>();
        var attempts = new HashSet<Guid>();
        var tokens = new HashSet<Guid>();
        foreach (var registration in registrations)
        {
            var lease = registration?.Lease;
            if (registration is null || registration.AttemptId == Guid.Empty || lease is null || lease.WriterId == Guid.Empty || lease.Token == Guid.Empty || lease.StoreEpoch != epoch
                || lease.Generation <= 0 || lease.Generation > generation || lease.NodeId == Guid.Empty || lease.ExpiresUtc.Offset != TimeSpan.Zero || lease.SettledThrough < 0
                || !identities.Add(lease.WriterId) || !attempts.Add(registration.AttemptId) || !tokens.Add(lease.Token))
            {
                throw new ArgumentException("An original writer registration contains invalid persistent state.");
            }

        }

        foreach (var registration in registrations)
        {
            var lease = registration.Lease;
            this.writers.Add(lease.WriterId, new Writer(registration.AttemptId, lease) { Retired = registration.Retired });
            this.attempts.Add(registration.AttemptId, lease.WriterId);
        }

        this.Generation = generation;
    }

    public Result<ProfilingWriterLease> Open(ProfilingOpenWriterRequest request, DateTimeOffset utc, TimeSpan maximumDuration)
    {
        if (request is null || request.AttemptId == Guid.Empty || request.Node is null || request.Node.Identity.Id == Guid.Empty)
        {
            return Result<ProfilingWriterLease>.Failure(new ProfilingValidationError("A stable attempt ID and cached node identity are required."));
        }

        if (request.LeaseDuration <= TimeSpan.Zero || request.LeaseDuration > maximumDuration || request.Node.ProcessStartedUtc.Offset != TimeSpan.Zero)
        {
            return Result<ProfilingWriterLease>.Failure(new ProfilingValidationError("A bounded lease duration and UTC node descriptor are required."));
        }

        if (this.attempts.TryGetValue(request.AttemptId, out var known) && this.writers.TryGetValue(known, out var previous))
        {
            return previous.Lease.NodeId == request.Node.Identity.Id && previous.Lease.ExpiresUtc > utc && !previous.Retired
                ? Result<ProfilingWriterLease>.Success(previous.Lease)
                : Result<ProfilingWriterLease>.Failure(new ProfilingUnavailableError("The original writer attempt is no longer active."));
        }

        this.Prune(utc);
        if (this.writers.Count >= capacity)
        {
            return Result<ProfilingWriterLease>.Failure(new ProfilingBusyError("The profiling writer registry is at capacity."));
        }

        var lease = new ProfilingWriterLease
        {
            WriterId = Guid.NewGuid(), Token = Guid.NewGuid(), StoreEpoch = epoch, Generation = checked(++this.Generation),
            NodeId = request.Node.Identity.Id, ExpiresUtc = utc.Add(request.LeaseDuration),
        };
        this.writers.Add(lease.WriterId, new Writer(request.AttemptId, lease));
        this.attempts.Add(request.AttemptId, lease.WriterId);
        return Result<ProfilingWriterLease>.Success(lease);
    }

    public Writer Find(ProfilingWriterLease lease, DateTimeOffset utc)
    {
        if (lease is null || lease.StoreEpoch != epoch || !this.writers.TryGetValue(lease.WriterId, out var writer)
            || writer.Retired || writer.Lease.ExpiresUtc <= utc || writer.Lease.Token != lease.Token
            || writer.Lease.Generation != lease.Generation || writer.Lease.NodeId != lease.NodeId)
        {
            return null;
        }

        return writer;
    }

    public ProfilingWriterSynchronizationResult Synchronize(ProfilingWriterSynchronizationRequest request, DateTimeOffset utc,
        IReadOnlyList<ProfilingPendingClear> pending)
    {
        var writer = this.Find(request.Lease, utc);
        if (writer is null)
        {
            return new() { Active = false, Lease = request.Lease };
        }

        writer.Lease = writer.Lease with
        {
            SettledThrough = Math.Max(writer.Lease.SettledThrough, request.SettledThrough),
            ExpiresUtc = utc.Add(request.LeaseDuration),
        };
        return new() { Active = true, Lease = writer.Lease, PendingClears = pending };
    }

    public (long Generation, IReadOnlyList<ProfilingWriterLease> Leases) Prepare(DateTimeOffset utc)
    {
        var boundary = this.Generation;
        this.Generation = checked(this.Generation + 1);
        return (boundary, Array.AsReadOnly(this.writers.Values.Where(w => !w.Retired && w.Lease.ExpiresUtc > utc).Select(w => w.Lease).ToArray()));
    }

    public void Close(ProfilingWriterLease lease, DateTimeOffset utc)
    {
        var writer = this.Find(lease, utc);
        if (writer is not null)
        {
            writer.Retired = true;
        }
    }

    public bool ReplayProtected(ProfilingWriteEnvelope envelope, DateTimeOffset utc) =>
        this.Find(envelope.Lease, utc) is { } writer && envelope.CompletionSequence > writer.Lease.SettledThrough;

    public bool ReferencesNode(Guid id, DateTimeOffset utc) => this.writers.Values.Any(writer => !writer.Retired && writer.Lease.ExpiresUtc > utc && writer.Lease.NodeId == id);

    public void Prune(DateTimeOffset utc)
    {
        foreach (var writer in this.writers.Values.Where(w => w.Retired || w.Lease.ExpiresUtc <= utc).ToArray())
        {
            this.writers.Remove(writer.Lease.WriterId);
            this.attempts.Remove(writer.AttemptId);
            released?.Invoke(writer.Lease.NodeId);
        }
    }

    internal sealed class Writer(Guid attemptId, ProfilingWriterLease lease)
    {
        public Guid AttemptId { get; } = attemptId;
        public ProfilingWriterLease Lease { get; set; } = lease;
        public bool Retired { get; set; }
    }
}

internal sealed record ProfilingWriterRegistrationState(Guid AttemptId, ProfilingWriterLease Lease, bool Retired);
