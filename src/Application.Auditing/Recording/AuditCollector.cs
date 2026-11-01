// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

using System.Text;
using System.Runtime.CompilerServices;

/// <summary>Collects bounded mutable evidence for one execution before its owner freezes it.</summary>
/// <example><code>var collector = new AuditCollector(record, options, TimeProvider.System);</code></example>
public sealed class AuditCollector
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, AuditChangeEvidence> changes = [];
    private readonly Dictionary<Guid, (long sequence, int bytes)> changeOwnership = [];
    private long changeSequence;
    private readonly HashSet<string> sourceRows = new(StringComparer.Ordinal);
    private readonly AuditingOptions options;
    private readonly TimeProvider time;
    private AuditRecord root;
    private readonly Dictionary<Guid, AuditAction> actions = [];
    private readonly HashSet<Guid> open = [];
    private readonly Dictionary<string, long> notes = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<Exception, AuditSafeError> exceptions = new();
    private readonly List<WeakReference<AuditScope>> scopes = [];
    private int errorCount;
    private long sequence;
    private int bytes;
    private bool stopped;
    private bool sealedDocument;

    /// <summary>Creates the configured auditing component with explicit ownership dependencies.</summary>
    /// <example><code>var collector = new AuditCollector(record, options, TimeProvider.System);</code></example>
    public AuditCollector(AuditRecord root, AuditingOptions options, TimeProvider time)
    {
        this.root = root;
        this.options = options;
        this.time = time;
        this.bytes = AuditRecordCodec.Encode(root).Length + 8_192 + HmacSha256AuditIntegrityService.ENVELOPE_RESERVE_BYTES;
    }

    /// <summary>The owning execution identity.</summary>
    /// <example><code>var value = collector.Id;</code></example>
    public Guid Id => this.root.AuditId;

    /// <summary>The captured execution start time.</summary>
    /// <example><code>var value = collector.Started;</code></example>
    public DateTimeOffset Started => this.root.StartedUtc;

    /// <summary>Whether the owner has closed collection.</summary>
    /// <example><code>var value = collector.CollectionClosed;</code></example>
    public bool CollectionClosed { get { lock (this.gate) return this.sealedDocument; } }

    /// <summary>Whether collection remains open and within its lifetime.</summary>
    /// <example><code>var value = collector.CanCapture;</code></example>
    public bool CanCapture
    {
        get
        {
            lock (this.gate)
            {
                if (!this.stopped && !this.sealedDocument && this.time.GetUtcNow() - this.Started >= this.options.Limits.MaxLifetime)
                {
                    this.Note("AuditCaptureExpired");
                    this.stopped = true;
                    this.exceptions.Clear();
                    this.ClearContributors();
                }

                return !this.stopped && !this.sealedDocument;
            }
        }
    }

    /// <summary>Admits one bounded action occurrence, or returns null on capture limits.</summary>
    /// <example><code>var actionId = collector.AddAction(options, null, 1, "Customers");</code></example>
    public Guid? AddAction(AuditScopeOptions scope, Guid? parent, int depth, string module)
    {
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument) return null;
            this.root = this.root with { TotalActionCount = this.root.TotalActionCount + 1 };
            var action = new AuditAction
            {
                Id = Guid.NewGuid(), ParentActionId = parent, Sequence = ++this.sequence,
                Key = scope.Descriptor.Key, DisplayName = Text(scope.Descriptor.DisplayName, 1_024), Kind = scope.Kind,
                Reason = Text(scope.Descriptor.Reason, 4_096), ModuleName = module, CorrelationId = CorrelationId.IsValid(CorrelationId.Current) ? CorrelationId.Current : this.root.CorrelationId,
                StartedUtc = this.time.GetUtcNow()
            };
            var cost = JsonSerializer.SerializeToUtf8Bytes(action).Length + 128;
            if (this.actions.Count >= this.options.Limits.MaxActions || depth > this.options.Limits.MaxDepth || this.open.Count >= this.options.Limits.MaxActiveChildren || !this.Reserve(cost))
            {
                this.root = this.root with { OmittedActionCount = this.root.OmittedActionCount + 1 };
                this.Note("AuditActionLimit");
                return null;
            }

            this.actions.Add(action.Id, action);
            this.open.Add(action.Id);
            return action.Id;
        }
    }

    /// <summary>Copies bounded source scalar identities without accepting business payloads.</summary>
    /// <example><code>collector.Source(actionId, new() { Feature = "Requester" });</code></example>
    public void Source(Guid? action, AuditSourceMetadata metadata)
    {
        if (metadata is null) return;
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument) return;
            if (action is Guid id)
            {
                var before = this.actions[id];
                var after = before with
                {
                    Feature = Text(metadata.Feature, 128) ?? before.Feature,
                    RequestType = Text(metadata.RequestType, 1_024) ?? before.RequestType,
                    HandlerType = Text(metadata.HandlerType, 1_024) ?? before.HandlerType,
                    ExecutionStatus = Text(metadata.ExecutionStatus, 128) ?? before.ExecutionStatus,
                    Attempt = metadata.Attempt is >= 1 ? metadata.Attempt : before.Attempt
                };
                if (this.Reserve(JsonSerializer.SerializeToUtf8Bytes(after).Length - JsonSerializer.SerializeToUtf8Bytes(before).Length)) this.actions[id] = after;
                if (metadata.RequestId is not null) this.Detail(action, "source.requestId", AuditValue.From(Text(metadata.RequestId, 128)));
            }
            else
            {
                var after = this.root with
                {
                    RequestId = Text(metadata.RequestId, 128) ?? this.root.RequestId,
                    FlowId = Text(metadata.FlowId, 128) ?? this.root.FlowId,
                    JobName = Text(metadata.JobName, 512) ?? this.root.JobName,
                    JobOccurrenceId = Text(metadata.JobOccurrenceId, 128) ?? this.root.JobOccurrenceId,
                    JobExecutionId = Text(metadata.JobExecutionId, 128) ?? this.root.JobExecutionId
                };
                if (this.Reserve(JsonSerializer.SerializeToUtf8Bytes(after).Length - JsonSerializer.SerializeToUtf8Bytes(this.root).Length)) this.root = after;
                if (metadata.Feature is not null) this.Detail(null, "source.feature", AuditValue.From(Text(metadata.Feature, 128)));
                if (metadata.RequestType is not null) this.Detail(null, "source.requestType", AuditValue.From(Text(metadata.RequestType, 1_024)));
                if (metadata.HandlerType is not null) this.Detail(null, "source.handlerType", AuditValue.From(Text(metadata.HandlerType, 1_024)));
                if (metadata.ExecutionStatus is not null) this.Detail(null, "source.executionStatus", AuditValue.From(Text(metadata.ExecutionStatus, 128)));
                if (metadata.Attempt is >= 1) this.Detail(null, "source.attempt", AuditValue.From((long)metadata.Attempt.Value));
            }
        }
    }

    /// <summary>Reserves bounded copied source evidence while its transaction outcome is unknown.</summary>
    /// <example><code>var handle = collector.StageChange(actionId, sourceEvidence);</code></example>
    public AuditChangeHandle StageChange(Guid? action, AuditChangeEvidence evidence)
    {
        if (evidence is null) return null;
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument) return null;
            if (this.changes.Count >= this.options.Limits.MaxChangeGroups || evidence.Properties.Length + this.changes.Values.Sum(group => group.Properties.Length) > this.options.Limits.MaxChangeProperties ||
                evidence.SourceRowIds.Length > this.options.Limits.MaxChangeProperties || evidence.SourceRowIds.Any(id => this.sourceRows.Contains(evidence.SourceStoreKey + ":" + id)))
            {
                this.Note("AuditChangeLimitOrDuplicate");
                return null;
            }

            var copy = evidence with
            {
                Id = Guid.NewGuid(), ActionId = action, PersistenceState = "Unknown",
                SourceStoreKey = Text(evidence.SourceStoreKey, 512), ChangeSetId = Text(evidence.ChangeSetId, 128),
                BulkOperationId = Text(evidence.BulkOperationId, 128), EntityType = Text(evidence.EntityType, 512), EntityId = Text(evidence.EntityId, 1_024),
                Operation = Text(evidence.Operation, 128), CaptureSource = Text(evidence.CaptureSource, 128),
                CaptureStrategy = Text(evidence.CaptureStrategy, 128), CaptureStatus = Text(evidence.CaptureStatus, 128), CopyMode = Text(evidence.CopyMode, 32),
                SourceRowIds = evidence.SourceRowIds.Select(id => Text(id, 128)).ToImmutableArray(),
                Properties = evidence.Properties.Select(property => property with
                {
                    Path = Text(property.Path, 512), Operation = Text(property.Operation, 128), ItemIdentity = Text(property.ItemIdentity, 1_024),
                    PathKind = Text(property.PathKind, 64), ValueType = Text(property.ValueType, 2_048),
                    OldValue = property.OldValue is null ? null : AuditCapturePolicy.Project(property.Path ?? "value", property.OldValue, this.options),
                    NewValue = property.NewValue is null ? null : AuditCapturePolicy.Project(property.Path ?? "value", property.NewValue, this.options)
                }).ToImmutableArray()
            };
            var cost = JsonSerializer.SerializeToUtf8Bytes(copy).Length + 128;
            if (!this.Reserve(cost)) { this.Note("AuditChangeLimit"); return null; }

            this.changes.Add(copy.Id, copy);
            this.changeOwnership.Add(copy.Id, (++this.changeSequence, cost));
            foreach (var row in copy.SourceRowIds) this.sourceRows.Add(copy.SourceStoreKey + ":" + row);
            return new(this, copy.Id);
        }
    }

    /// <summary>Confirms only rows retained after a source commit, including savepoint membership changes.</summary>
    /// <example><code>collector.ConfirmChange(groupId, committedIds);</code></example>
    public void ConfirmChange(Guid id, IReadOnlySet<string> committedIds)
    {
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument || committedIds is null || !this.changes.TryGetValue(id, out var group)) return;
            var indexes = Enumerable.Range(0, group.SourceRowIds.Length).Where(index => committedIds.Contains(group.SourceRowIds[index])).ToArray();
            if (indexes.Length == 0) { this.DiscardChange(id); return; }

            this.changes[id] = group with
            {
                PersistenceState = "Committed", SourceRowIds = indexes.Select(index => group.SourceRowIds[index]).ToImmutableArray(),
                Properties = group.Properties.Length == group.SourceRowIds.Length ? indexes.Select(index => group.Properties[index]).ToImmutableArray() : group.Properties
            };
        }
    }

    /// <summary>Releases known rolled-back evidence without inventing a persisted change.</summary>
    /// <example><code>collector.DiscardChange(groupId);</code></example>
    public void DiscardChange(Guid id)
    {
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument || !this.changes.Remove(id, out var group)) return;
            foreach (var row in group.SourceRowIds) this.sourceRows.Remove(group.SourceStoreKey + ":" + row);
            if (this.changeOwnership.Remove(id, out var reserved)) this.Reserve(-reserved.bytes);
        }
    }

    private static AuditChangeEvidence WithoutUnconfirmedValues(AuditChangeEvidence group) => group.PersistenceState == "Committed" ? group
        : group with { Properties = group.Properties.Select(property => property with { OldValue = null, NewValue = null }).ToImmutableArray() };

    /// <summary>Copies the live execution's permitted initiating identity and immediate causation.</summary>
    /// <example><code>var link = collector.CaptureLink(actionId);</code></example>
    public AuditExecutionLink CaptureLink(Guid? action)
    {
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument) return null;
            var initiator = this.root.InitiatingActor ?? this.root.Actor;
            var module = action is Guid id && this.actions.TryGetValue(id, out var occurrence) ? occurrence.ModuleName : this.root.ModuleName;
            return new()
            {
                OriginAuditId = this.root.OriginAuditId, CausationAuditId = this.root.AuditId, CausationActionId = action,
                CorrelationId = this.root.CorrelationId, InitiatingModuleName = this.root.InitiatingModuleName ?? module ?? this.root.ModuleName,
                InitiatingActor = initiator is null ? null : new() { Kind = initiator.Kind, UserId = initiator.UserId, DisplayName = initiator.DisplayName, IsAuthenticated = initiator.IsAuthenticated }
            };
        }
    }

    /// <summary>Copies bounded HTTP metadata without query strings, headers, or bodies.</summary>
    /// <example><code>collector.SetHttp(new() { Method = "POST", Status = 200 });</code></example>
    public void SetHttp(AuditHttpContext http)
    {
        if (http is null) return;
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument) return;
            var value = new AuditHttpContext { Method = Text(http.Method, 32), Route = Text(http.Route, 2_048), Path = Text(http.Path?.Split('?')[0], 2_048), Endpoint = Text(http.Endpoint, 1_024), Status = http.Status is >= 100 and <= 599 ? http.Status : null };
            var before = this.root.Http is null ? 4 : JsonSerializer.SerializeToUtf8Bytes(this.root.Http).Length;
            if (this.Reserve(JsonSerializer.SerializeToUtf8Bytes(value).Length - before)) this.root = this.root with { Http = value };
        }
    }

    /// <summary>Refreshes an initially anonymous HTTP actor without replacing a captured authenticated identity.</summary>
    /// <example><code>collector.RefreshActor(currentUser);</code></example>
    public void RefreshActor(ICurrentUserAccessor user)
    {
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument || this.root.Actor.IsAuthenticated || user?.IsAuthenticated != true) return;
            var id = user.UserId;
            if (Text(id, 1_024) != id) { id = null; this.Note("AuditActorIdentityOmitted"); }

            var name = user.UserName;
            var actor = new AuditActor { Kind = "User", IsAuthenticated = true, UserId = id, DisplayName = name?.Contains('@') == true ? null : Text(name, 1_024) };
            if (this.Reserve(JsonSerializer.SerializeToUtf8Bytes(actor).Length - JsonSerializer.SerializeToUtf8Bytes(this.root.Actor).Length)) this.root = this.root with { Actor = actor };
        }
    }

    /// <summary>Adds one projected detail through the shared capture policy.</summary>
    /// <example><code>collector.Detail(null, "count", AuditValue.From(1L));</code></example>
    public void Detail(Guid? action, string name, AuditValue value)
    {
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument) return;
            if (value is null) { this.Note("AuditDetailInvalid"); return; }

            try { name = new AuditDetailName(name).Value; }
            catch (ArgumentException) { this.Note("AuditDetailInvalid"); return; }

            var details = action.HasValue ? this.actions[action.Value].Details : this.root.Details;
            if (!details.ContainsKey(name) && details.Count >= this.options.Limits.MaxDetails) { this.OmitDetail(); return; }

            value = AuditCapturePolicy.Project(name, value, this.options);
            if (value.Marker == "Oversized") this.Note("AuditValueTooLarge");
            var cost = Encoding.UTF8.GetByteCount(name) + JsonSerializer.SerializeToUtf8Bytes(value).Length + 16;
            var priorCost = details.TryGetValue(name, out var prior) ? Encoding.UTF8.GetByteCount(name) + JsonSerializer.SerializeToUtf8Bytes(prior).Length + 16 : 0;
            if (!this.Reserve(cost - priorCost)) { this.OmitDetail(); return; }

            details = details.SetItem(name, value);
            if (action.HasValue) this.actions[action.Value] = this.actions[action.Value] with { Details = details };
            else this.root = this.root with { Details = details };
        }
    }

    /// <summary>Adds a copied business resource reference under capture limits.</summary>
    /// <example><code>collector.Resource(null, resource);</code></example>
    public void Resource(Guid? action, AuditResourceReference resource)
    {
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument || resource is null) return;
            if (Text(resource.DisplayName, 1_024) != resource.DisplayName) this.Note("AuditResourceLabelTruncated");
            var copy = new AuditResourceReference(resource.Type, resource.Id, Text(resource.DisplayName, 1_024));
            var list = action.HasValue ? this.actions[action.Value].Resources : this.root.Resources;
            if (list.Any(existing => string.Equals(existing.Type, copy.Type, StringComparison.OrdinalIgnoreCase) && existing.Id == copy.Id)) return;
            if (list.Length >= 100 || !this.Reserve(JsonSerializer.SerializeToUtf8Bytes(copy).Length + 16)) { this.Note("AuditResourceLimit"); return; }

            list = list.Add(copy);
            if (action.HasValue) this.actions[action.Value] = this.actions[action.Value] with { Resources = list };
            else this.root = this.root with { Resources = list };
        }
    }

    /// <summary>Implements the audit scope ownership contract.</summary>
    /// <example><code>scope.Complete();</code></example>
    public void Complete(Guid? action, AuditOutcome outcome)
    {
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument) return;
            if (action.HasValue) this.actions[action.Value] = this.actions[action.Value] with { Outcome = outcome };
            else this.root = this.root with { Outcome = outcome };
        }
    }

    /// <summary>Implements the audit scope ownership contract.</summary>
    /// <example><code>scope.Fail(error);</code></example>
    public void Fail(Guid? action, Exception exception)
    {
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument || exception is null) return;
            var outcome = exception is OperationCanceledException canceled && canceled.CancellationToken.IsCancellationRequested ? AuditOutcome.Canceled : AuditOutcome.Failed;
            this.Complete(action, outcome);
            if (!this.exceptions.TryGetValue(exception, out var error))
            {
                if (this.errorCount >= this.options.Limits.MaxErrors) { this.Note("AuditErrorLimit"); return; }

                error = new AuditSafeError { ExceptionType = Text(exception.GetType().FullName, 256), Code = outcome.ToString() };
                this.exceptions.Add(exception, error);
                this.errorCount++;
            }

            if (action.HasValue)
            {
                var current = this.actions[action.Value];
                if (!current.Errors.Any(e => e.Id == error.Id))
                {
                    if (this.Reserve(JsonSerializer.SerializeToUtf8Bytes(error).Length + 16)) this.actions[action.Value] = current with { Errors = current.Errors.Add(error) };
                    else this.Note("AuditErrorLimit");
                }
            }
            else if (!this.root.Errors.Any(e => e.Id == error.Id))
            {
                if (this.Reserve(JsonSerializer.SerializeToUtf8Bytes(error).Length + 16)) this.root = this.root with { Errors = this.root.Errors.Add(error) };
                else this.Note("AuditErrorLimit");
            }
        }
    }

    /// <summary>Closes one action or freezes collection for the owning execution.</summary>
    /// <example><code>collector.Close(null);</code></example>
    public void Close(Guid? action)
    {
        lock (this.gate)
        {
            if (this.sealedDocument) return;
            var now = this.time.GetUtcNow();
            if (action.HasValue)
            {
                if (this.open.Remove(action.Value)) this.actions[action.Value] = this.actions[action.Value] with { CompletedUtc = now };
                if (this.actions[action.Value].Outcome == AuditOutcome.Unknown) this.Note("AuditActionIncomplete");
                if (this.open.Any(id => this.actions[id].ParentActionId == action)) this.Note("AuditOutOfOrderClosure");
                return;
            }

            if (this.open.Count > 0) this.Note("AuditActiveChildrenAtClosure");
            if (this.root.Outcome == AuditOutcome.Unknown) this.Note("AuditRootIncomplete");
            foreach (var id in this.open) this.actions[id] = this.actions[id] with { CompletedUtc = now, ExecutionStatus = "Incomplete" };
            this.open.Clear();
            this.root = this.root with { CompletedUtc = now };
            this.stopped = true;
            if (this.changes.Values.Any(group => group.PersistenceState != "Committed")) this.Note("AuditSourceTransactionUnknown");
            foreach (var id in this.changes.Keys.ToArray()) this.changes[id] = WithoutUnconfirmedValues(this.changes[id]);
            this.sealedDocument = true;
            this.exceptions.Clear();
            this.ClearContributors();
        }
    }

    /// <summary>Atomically stops an open collector; false means it already closed.</summary>
    /// <example><code>var stopped = collector.Stop("AuditCaptureExpired");</code></example>
    public bool Stop(string code)
    {
        lock (this.gate)
        {
            if (this.sealedDocument) return false;
            if (!this.stopped) this.Note(code);
            this.stopped = true;
            foreach (var id in this.changes.Keys.ToArray()) this.changes[id] = WithoutUnconfirmedValues(this.changes[id]);
            this.exceptions.Clear();
            this.ClearContributors();
            return true;
        }
    }

    /// <summary>Registers bounded completion ownership only while capture is open.</summary>
    /// <example><code>collector.RegisterCompletion(scope, () =&gt; { });</code></example>
    public bool RegisterCompletion(AuditScope scope, Action completing)
    {
        lock (this.gate)
        {
            if (this.stopped || this.sealedDocument) return false;
            this.scopes.Add(new WeakReference<AuditScope>(scope));
            scope.SetCompletion(completing);
            return true;
        }
    }

    private void ClearContributors()
    {
        foreach (var reference in this.scopes) if (reference.TryGetTarget(out var scope)) scope.ClearCompletion();
        this.scopes.Clear();
    }

    /// <summary>Copies the current logical document; only closed documents can be finalized.</summary>
    /// <example><code>var document = collector.Snapshot();</code></example>
    public AuditRecord Snapshot()
    {
        lock (this.gate)
        {
            return this.root with
            {
                FailedActionCount = this.actions.Values.LongCount(a => a.Outcome == AuditOutcome.Failed),
                HasFailedActions = this.actions.Values.Any(a => a.Outcome == AuditOutcome.Failed),
                Actions = this.actions.Values.OrderBy(a => a.Sequence).ToImmutableArray(),
                EntityChanges = this.changes.Values.OrderBy(group => this.changeOwnership[group.Id].sequence).Select(WithoutUnconfirmedValues).ToImmutableArray(),
                Notes = this.notes.OrderBy(n => n.Key, StringComparer.Ordinal).Select(n => new AuditCaptureNote { Code = n.Key, Count = n.Value }).ToImmutableArray(),
                Completeness = this.notes.Keys.Any(n => n is "AuditRootIncomplete" or "AuditActionIncomplete" or "AuditActiveChildrenAtClosure" or "AuditOutOfOrderClosure" or "AuditCaptureExpired" or "AuditRuntimeStopped") ? AuditCompleteness.Incomplete : this.notes.Count > 0 ? AuditCompleteness.Truncated : AuditCompleteness.Complete
            };
        }
    }

    /// <summary>Adds a bounded safe reason identifier and count.</summary>
    /// <example><code>collector.CaptureNote("AuditDetailLimit");</code></example>
    public void CaptureNote(string code) { lock (this.gate) { if (!this.sealedDocument) this.Note(code); } }

    private bool Reserve(int count) { if ((long)this.bytes + count > this.options.Limits.MaxRecordBytes) return false; this.bytes += count; return true; }
    private void Note(string code) { if (string.IsNullOrEmpty(code) || code.Length > 64 || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-' or ':'))) code = "AuditNoteInvalid"; if (this.notes.TryGetValue(code, out var count)) this.notes[code] = count + 1; else if (this.notes.Count < 32) this.notes.Add(code, 1); }
    private void OmitDetail() { this.root = this.root with { OmittedDetailCount = this.root.OmittedDetailCount + 1 }; this.Note("AuditDetailLimit"); }

    /// <summary>Truncates display text on complete UTF-8 rune boundaries.</summary>
    /// <example><code>var label = AuditCollector.Text("Customer", 20);</code></example>
    public static string Text(string value, int maxBytes)
    {
        if (value is null) return null;
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;
        var builder = new StringBuilder();
        var bytes = 0;
        foreach (var rune in value.EnumerateRunes()) { if (bytes + rune.Utf8SequenceLength > maxBytes) break; builder.Append(rune); bytes += rune.Utf8SequenceLength; }

        return builder.ToString();
    }
}
