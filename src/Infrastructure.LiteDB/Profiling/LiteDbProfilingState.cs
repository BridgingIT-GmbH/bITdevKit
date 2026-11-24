// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Common;

public sealed partial class LiteDbProfilingStorageProvider
{
    private sealed partial class RuntimeState
    {
        public IEnumerable<NativeRow> CaptureRows()
        {
            foreach (var row in this.sessions) yield return Pack("runtime.sessions", row.Key, row.Value);
            foreach (var row in this.sessionKeys) yield return Pack("runtime.sessionKeys", row.Key, row.Value);
            foreach (var row in this.nodes) yield return Pack("runtime.nodes", row.Key, row.Value);
            foreach (var row in this.nodeCorrelations) yield return Pack("runtime.nodeCorrelations", row.Key, row.Value);
            foreach (var row in this.participations) yield return Pack("runtime.participations", row.Key, row.Value);
            foreach (var row in this.runtimeContexts) yield return Pack("runtime.runtimeContexts", row.Key, row.Value);
            foreach (var row in this.snapshots) yield return Pack("runtime.snapshots", row.Key, row.Value);
            foreach (var row in this.snapshotKeys) yield return Pack("runtime.snapshotKeys", row.Key, row.Value);
            foreach (var row in this.markers) yield return Pack("runtime.markers", row.Key, row.Value);
            foreach (var row in this.segments) yield return Pack("runtime.segments", row.Key, row.Value);
            foreach (var row in this.metricObservations) yield return Pack("runtime.metricObservations", row.Key, row.Value);
            foreach (var key in this.invalidSessionIds) yield return Pack("runtime.invalidSessionIds", key, true);
            foreach (var key in this.invalidSessionKeys) yield return Pack("runtime.invalidSessionKeys", key, true);
        }

        public Guid? MaintenanceGate { get => this.maintenanceGate; set => this.maintenanceGate = value; }

        public void RestoreRows(IEnumerable<NativeRow> rows)
        {
            foreach (var row in rows)
            {
                switch (row.Kind)
                {
                    case "runtime.sessions": this.sessions.Add(row.Key.Deserialize<Guid>(JsonOptions), row.Value.Deserialize<RuntimeProfilingSession>(JsonOptions)); break;
                    case "runtime.sessionKeys": this.sessionKeys.Add(row.Key.Deserialize<string>(JsonOptions), row.Value.Deserialize<Guid>(JsonOptions)); break;
                    case "runtime.nodes": this.nodes.Add(row.Key.Deserialize<Guid>(JsonOptions), row.Value.Deserialize<ProfilingNode>(JsonOptions)); break;
                    case "runtime.nodeCorrelations": this.nodeCorrelations.Add(row.Key.Deserialize<NodeCorrelationKey>(JsonOptions), row.Value.Deserialize<Guid>(JsonOptions)); break;
                    case "runtime.participations": this.participations.Add(row.Key.Deserialize<(Guid SessionId, Guid NodeId)>(JsonOptions), row.Value.Deserialize<RuntimeProfilingNodeParticipation>(JsonOptions)); break;
                    case "runtime.runtimeContexts": this.runtimeContexts.Add(row.Key.Deserialize<(Guid SessionId, Guid NodeId)>(JsonOptions), row.Value.Deserialize<RuntimeProfilingContext>(JsonOptions)); break;
                    case "runtime.snapshots": this.snapshots.Add(row.Key.Deserialize<Guid>(JsonOptions), row.Value.Deserialize<RuntimeProfilingSnapshot>(JsonOptions)); break;
                    case "runtime.snapshotKeys": this.snapshotKeys.Add(row.Key.Deserialize<string>(JsonOptions), row.Value.Deserialize<Guid>(JsonOptions)); break;
                    case "runtime.markers": this.markers.Add(row.Key.Deserialize<Guid>(JsonOptions), row.Value.Deserialize<ProfilingMarker>(JsonOptions)); break;
                    case "runtime.segments": this.segments.Add(row.Key.Deserialize<Guid>(JsonOptions), row.Value.Deserialize<ProfilingSegment>(JsonOptions)); break;
                    case "runtime.metricObservations": this.metricObservations.Add(row.Key.Deserialize<Guid>(JsonOptions), row.Value.Deserialize<ProfilingMetricObservation>(JsonOptions)); break;
                    case "runtime.invalidSessionIds": this.invalidSessionIds.Add(row.Key.Deserialize<Guid>(JsonOptions)); break;
                    case "runtime.invalidSessionKeys": this.invalidSessionKeys.Add(row.Key.Deserialize<string>(JsonOptions)); break;
                    default: throw new InvalidOperationException("Unknown native runtime record kind.");
                }
            }

            foreach (var session in this.sessions.Values.Where(session => IsTerminal(session.State) && !session.IsPinned))
                this.retentionOrder.Add((TerminalTimestamp(session).UtcTicks, session.Identity.Key));
        }
    }

    private sealed partial class Frame
    {
        public void Restore(NativeState state)
        {
            this.watermark = state.Watermark;
            this.deletionRevision = state.DeletionRevision;
            this.capacityEvictionsToReport = state.CapacityEvictions;
            this.writers.Restore(state.WriterGeneration, state.Writers);
            this.clears.Restore(state.Clears.Select(clear => clear.Restore()).ToArray());
            this.runtime.MaintenanceGate = state.RuntimeGate;
        }

        public void RestoreRows(IEnumerable<NativeRow> rows)
        {
            var materialized = rows.ToArray();
            foreach (var row in materialized.Where(row => row.Kind == "operation"))
            {
                var stored = row.Value.Deserialize<StoredOperation>(JsonOptions);
                stored = stored with { Envelope = stored.Envelope with { Record = ProfilingRecordSnapshot.Copy(stored.Envelope.Record, this.options) } };
                var id = stored.Envelope.Record.Id;
                this.records.Add(id, stored);
                this.sequences.Add((stored.Envelope.Lease.WriterId, stored.Envelope.CompletionSequence), id);
                this.completionOrder.Add(stored);
                this.retainedBytes = checked(this.retainedBytes + stored.Envelope.Record.EstimatedPayloadBytes);
                this.nodeOwners.TryGetValue(stored.Envelope.Record.NodeId, out var owners);
                this.nodeOwners[stored.Envelope.Record.NodeId] = checked(owners + 1);
            }

            this.runtime.RestoreRows(materialized.Where(row => row.Kind != "operation"));
        }

        public IEnumerable<NativeRow> CaptureRows()
        {
            foreach (var row in this.records) yield return Pack("operation", row.Key, row.Value);
            foreach (var row in this.runtime.CaptureRows()) yield return row;
        }

        public NativeState Capture() => new()
        {
            Epoch = this.epoch, Secret = this.querySecret, Watermark = this.watermark,
            DeletionRevision = this.deletionRevision, CapacityEvictions = this.capacityEvictionsToReport,
            WriterGeneration = this.writers.Generation, Writers = this.writers.Snapshot(),
            Clears = this.clears.Clears.Select(ClearSnapshot.Capture).ToArray(), RuntimeGate = this.runtime.MaintenanceGate
        };

        public Result<Guid> PrepareClear(ProfilingClearRequest request)
        {
            var prepared = this.clears.Prepare(request, this.writers, this.clock.GetUtcNow(), this.runtime.HasActiveSession);
            if (prepared.IsFailure) return Result<Guid>.Failure(prepared);
            var clear = prepared.Value;
            if (request.DataSet != ProfilingDataSet.Operations && !this.runtime.ReserveMaintenance(clear.Id))
            {
                clear.State = ProfilingClearState.Failed;
                return Result<Guid>.Failure(new ProfilingBusyError("Runtime maintenance is already reserved."));
            }

            return Result<Guid>.Success(clear.Id);
        }

        public Result<ProfilingClearResult> AdvanceClear(Guid id, CancellationToken cancellationToken, bool cancel = false)
        {
            var clear = this.clears.Clears.Single(clear => clear.Id == id);
            var now = this.clock.GetUtcNow();
            if (clear.SealedUtc is null && (cancel || clear.DeadlineUtc <= now))
            {
                clear.State = ProfilingClearState.Failed;
                this.runtime.ReleaseMaintenance(id);
            }

            if (clear.State == ProfilingClearState.Failed)
                return Result<ProfilingClearResult>.Failure(this.clears.Result(clear)).WithError(new ProfilingBusyError("Clear preparation did not complete."));
            if (this.clears.CanSeal(clear, this.writers, now)) this.clears.Seal(clear, now);
            if (clear.SealedUtc.HasValue) this.ApplyClear(clear, this.storageOptions.MaximumMaintenanceRoots, this.storageOptions.MaintenanceTimeBudget, cancellationToken);
            return Result<ProfilingClearResult>.Success(this.clears.Result(clear));
        }
    }

    private sealed class NativeState
    {
        public NativeState() { }

        public Guid Epoch { get; set; } = Guid.NewGuid();
        public byte[] Secret { get; set; } = RandomNumberGenerator.GetBytes(32);
        public long Watermark { get; set; }

        public long DeletionRevision { get; set; }

        public long CapacityEvictions { get; set; }

        public long WriterGeneration { get; set; }

        public IReadOnlyList<ProfilingWriterRegistrationState> Writers { get; set; } = [];
        public IReadOnlyList<ClearSnapshot> Clears { get; set; } = [];
        public Guid? RuntimeGate { get; set; }
    }

    private sealed class NativeRow
    {
        public NativeRow() { }

        public string Kind { get; set; }

        public JsonElement Key { get; set; }

        public JsonElement Value { get; set; }

        public string Identity => this.Kind + ":" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(this.Key.GetRawText())));
    }

    private sealed class ClearSnapshot
    {
        public ClearSnapshot() { }

        public Guid Id { get; set; }

        public ProfilingClearRequest Selection { get; set; }

        public DateTimeOffset PreparedUtc { get; set; }

        public DateTimeOffset DeadlineUtc { get; set; }

        public long Generation { get; set; }

        public ProfilingWriterLease[] Eligible { get; set; }

        public Dictionary<Guid, long> Cutoffs { get; set; }

        public ProfilingClearState State { get; set; }

        public DateTimeOffset? SealedUtc { get; set; }

        public DateTimeOffset? CompletedUtc { get; set; }

        public long RemovedOperations { get; set; }

        public long RemovedSummaries { get; set; }

        public int RemovedRuntimeSessions { get; set; }

        public long RemovedSnapshots { get; set; }

        public static ClearSnapshot Capture(ProfilingClearCoordinator.Clear clear) => new()
        {
            Id = clear.Id, Selection = clear.Selection, PreparedUtc = clear.PreparedUtc, DeadlineUtc = clear.DeadlineUtc,
            Generation = clear.Generation, Eligible = clear.Eligible.Values.ToArray(), Cutoffs = new(clear.Cutoffs), State = clear.State,
            SealedUtc = clear.SealedUtc, CompletedUtc = clear.CompletedUtc, RemovedOperations = clear.RemovedOperations,
            RemovedSummaries = clear.RemovedSummaries, RemovedRuntimeSessions = clear.RemovedRuntimeSessions, RemovedSnapshots = clear.RemovedSnapshots
        };

        public ProfilingClearCoordinator.Clear Restore()
        {
            var clear = new ProfilingClearCoordinator.Clear(this.Id, this.Selection, this.PreparedUtc, this.DeadlineUtc, this.Generation, this.Eligible)
            {
                State = this.State, SealedUtc = this.SealedUtc, CompletedUtc = this.CompletedUtc, RemovedOperations = this.RemovedOperations,
                RemovedSummaries = this.RemovedSummaries, RemovedRuntimeSessions = this.RemovedRuntimeSessions, RemovedSnapshots = this.RemovedSnapshots
            };
            foreach (var pair in this.Cutoffs) clear.Cutoffs.Add(pair.Key, pair.Value);
            return clear;
        }
    }
}
