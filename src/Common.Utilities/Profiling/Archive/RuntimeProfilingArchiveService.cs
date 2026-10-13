// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Provides provider-neutral JSON archive export, validation, remapping, and import.</summary>
/// <param name="options">The shared Profiling feature options.</param>
/// <param name="store">The configured Profiling store.</param>
/// <param name="timeProvider">The clock used for export metadata.</param>
/// <example><code>await service.ExportSessionAsync(sessionKey, stream, cancellationToken);</code></example>
public sealed class RuntimeProfilingArchiveService(
    ProfilingOptions options,
    IRuntimeProfilingStore store = null,
    TimeProvider timeProvider = null
) : IRuntimeProfilingArchiveService
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public async Task<IResult> ExportSessionAsync(
        string sessionKey,
        Stream destination,
        CancellationToken cancellationToken = default
    )
    {
        var availability = this.ValidateAvailability();
        if (availability is not null)
        {
            return Result.Failure().WithError(availability);
        }

        if (destination is null || !destination.CanWrite)
        {
            return ArchiveFailure("A writable archive destination is required.");
        }

        var dataResult = await store
            .GetSessionDataAsync(sessionKey, cancellationToken)
            .ConfigureAwait(false);
        if (dataResult.IsFailure)
        {
            return Result.Failure().WithErrors(dataResult.Errors).WithMessages(dataResult.Messages);
        }

        if (!IsTerminal(dataResult.Value.Session.State))
        {
            return ArchiveFailure("A complete session archive can only be created from a terminal session.");
        }

        var archiveResult = CreateArchive(dataResult.Value, RuntimeProfilingArchiveKind.Session);
        return archiveResult.IsFailure
            ? Result.Failure().WithErrors(archiveResult.Errors).WithMessages(archiveResult.Messages)
            : await this.WriteAsync(archiveResult.Value, destination, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IResult> ExportSnapshotAsync(
        string sessionKey,
        string nodeKey,
        string snapshotKey,
        Stream destination,
        CancellationToken cancellationToken = default
    )
    {
        var availability = this.ValidateAvailability();
        if (availability is not null)
        {
            return Result.Failure().WithError(availability);
        }

        if (destination is null || !destination.CanWrite)
        {
            return ArchiveFailure("A writable archive destination is required.");
        }

        var dataResult = await store
            .GetSessionDataAsync(sessionKey, cancellationToken)
            .ConfigureAwait(false);
        if (dataResult.IsFailure)
        {
            return Result.Failure().WithErrors(dataResult.Errors).WithMessages(dataResult.Messages);
        }

        var data = dataResult.Value;
        var snapshot = data.Snapshots.SingleOrDefault(item =>
            string.Equals(item.Identity.Key, snapshotKey, StringComparison.Ordinal)
            && string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal)
        );
        var node = data.Nodes.SingleOrDefault(item =>
            string.Equals(item.Identity.Key, nodeKey, StringComparison.Ordinal)
        );
        if (snapshot is null || node is null)
        {
            return ArchiveFailure("The selected snapshot was not found in the selected session and node.");
        }

        var snapshotData = new RuntimeProfilingSessionData
        {
            Session = data.Session,
            Nodes = [node],
            Participations = data.Participations.Where(item => item.NodeKey == nodeKey).DefaultIfEmpty(
                new RuntimeProfilingNodeParticipation
                {
                    SessionId = data.Session.Identity.Id,
                    SessionKey = data.Session.Identity.Key,
                    NodeId = node.Identity.Id,
                    NodeKey = node.Identity.Key,
                    Role = RuntimeProfilingNodeRole.AdHocContributor,
                    State = RuntimeProfilingParticipationState.Completed,
                    JoinedUtc = snapshot.TimestampUtc,
                    CompletedUtc = snapshot.TimestampUtc,
                    SuccessfulCaptureCount = 1,
                }
            ).ToArray(),
            RuntimeContexts = data.RuntimeContexts.Where(item => item.NodeKey == nodeKey).ToArray(),
            Snapshots = [snapshot],
        };
        var archiveResult = CreateArchive(snapshotData, RuntimeProfilingArchiveKind.Snapshot);
        return archiveResult.IsFailure
            ? Result.Failure().WithErrors(archiveResult.Errors).WithMessages(archiveResult.Messages)
            : await this.WriteAsync(archiveResult.Value, destination, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingArchiveImportResult>> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default
    )
    {
        var availability = this.ValidateAvailability();
        if (availability is not null)
        {
            return Failure<RuntimeProfilingArchiveImportResult>(availability);
        }

        if (source is null || !source.CanRead)
        {
            return ArchiveFailure<RuntimeProfilingArchiveImportResult>("A readable archive source is required.");
        }

        var archiveResult = await ReadAsync(source, cancellationToken).ConfigureAwait(false);
        if (archiveResult.IsFailure)
        {
            return CopyFailure<RuntimeProfilingArchiveImportResult, RuntimeProfilingArchive>(archiveResult);
        }

        var validation = ValidateArchive(archiveResult.Value);
        if (validation is not null)
        {
            return ArchiveFailure<RuntimeProfilingArchiveImportResult>(validation);
        }

        var mapped = MapForImport(archiveResult.Value);
        var importResult = await store
            .ImportSessionAsync(mapped.Data, cancellationToken)
            .ConfigureAwait(false);
        return importResult.IsFailure
            ? CopyFailure<RuntimeProfilingArchiveImportResult, RuntimeProfilingSession>(importResult)
            : Result<RuntimeProfilingArchiveImportResult>.Success(
                new(importResult.Value.Identity.Key, mapped.NodeKeys, mapped.SnapshotKeys)
            );
    }

    private static Result<RuntimeProfilingArchive> CreateArchive(
        RuntimeProfilingSessionData data,
        RuntimeProfilingArchiveKind kind
    )
    {
        var segmentReferences = data.Segments
            .Select((segment, index) => (segment.Id, Reference: index + 1))
            .ToDictionary(item => item.Id, item => item.Reference);
        var segments = new List<RuntimeProfilingArchiveSegment>(data.Segments.Count);
        foreach (var segment in data.Segments)
        {
            if (
                segment.ParentSegmentId is { } parentId
                && !segmentReferences.TryGetValue(parentId, out _)
            )
            {
                return ArchiveFailure<RuntimeProfilingArchive>(
                    "A segment parent is missing from the exported session graph."
                );
            }

            segments.Add(
                new(
                    segmentReferences[segment.Id],
                    segment.ParentSegmentId is { } parent
                        ? segmentReferences[parent]
                        : null,
                    segment
                )
            );
        }

        var observations = new List<RuntimeProfilingArchiveMetricObservation>(
            data.MetricObservations.Count
        );
        foreach (var observation in data.MetricObservations)
        {
            if (
                observation.SegmentId is { } segmentId
                && !segmentReferences.TryGetValue(segmentId, out _)
            )
            {
                return ArchiveFailure<RuntimeProfilingArchive>(
                    "A metric segment is missing from the exported session graph."
                );
            }

            observations.Add(
                new(
                    observation.SegmentId is { } segment
                        ? segmentReferences[segment]
                        : null,
                    observation
                )
            );
        }

        return Result<RuntimeProfilingArchive>.Success(
            new()
            {
                Kind = kind,
                Session = data.Session,
                Nodes = data.Nodes,
                Participations = data.Participations,
                RuntimeContexts = data.RuntimeContexts,
                Snapshots = data.Snapshots,
                Markers = [ ..(data.Markers.Where(marker => marker.Scope == ProfilingMarkerScope.Session)), ..(data.Markers.Where(marker => marker.Scope == ProfilingMarkerScope.Node)) ],
                Segments = segments,
                MetricObservations = observations,
            }
        );
    }

    private async Task<IResult> WriteAsync(
        RuntimeProfilingArchive archive,
        Stream destination,
        CancellationToken cancellationToken
    )
    {
        archive = archive with { ExportedUtc = this.timeProvider.GetUtcNow() };
        try
        {
            await using var buffer = new MemoryStream();
            await JsonSerializer
                .SerializeAsync(buffer, archive, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            if (buffer.Length > RuntimeProfilingArchiveFormat.MaximumSizeBytes)
            {
                return ArchiveFailure("The Profiling archive exceeds the 25 MiB size limit.");
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return ArchiveFailure("The Profiling archive could not be serialized.");
        }
    }

    private static async Task<IResult<RuntimeProfilingArchive>> ReadAsync(
        Stream source,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await using var buffer = new MemoryStream();
            var bytes = new byte[81920];
            while (true)
            {
                var read = await source
                    .ReadAsync(bytes.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (buffer.Length + read > RuntimeProfilingArchiveFormat.MaximumSizeBytes)
                {
                    return ArchiveFailure<RuntimeProfilingArchive>(
                        "The Profiling archive exceeds the 25 MiB size limit."
                    );
                }

                await buffer.WriteAsync(bytes.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (buffer.Length == 0)
            {
                return ArchiveFailure<RuntimeProfilingArchive>("The Profiling archive is empty.");
            }

            buffer.Position = 0;
            var archive = await JsonSerializer
                .DeserializeAsync<RuntimeProfilingArchive>(buffer, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return archive is null
                ? ArchiveFailure<RuntimeProfilingArchive>("The Profiling archive is empty.")
                : Result<RuntimeProfilingArchive>.Success(archive);
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or ArgumentException
        )
        {
            return ArchiveFailure<RuntimeProfilingArchive>("The Profiling archive JSON is invalid.");
        }
    }

    private static string ValidateArchive(RuntimeProfilingArchive archive)
    {
        if (!string.Equals(archive.Format, RuntimeProfilingArchiveFormat.Identifier, StringComparison.Ordinal))
        {
            return "The Profiling archive format is unsupported.";
        }

        if (archive.Version != RuntimeProfilingArchiveFormat.Version)
        {
            return "The Profiling archive version is unsupported.";
        }

        if (!Enum.IsDefined(archive.Kind) || archive.ExportedUtc.Offset != TimeSpan.Zero)
        {
            return "The Profiling archive header is invalid.";
        }

        var session = archive.Session;
        if (
            session is null
            || archive.Nodes is null
            || archive.Participations is null
            || archive.RuntimeContexts is null
            || archive.Snapshots is null
            || archive.Markers is null
            || archive.Segments is null
            || archive.MetricObservations is null
            || !IsPublicKey(session.Identity.Key)
            || session.StartedUtc.Offset != TimeSpan.Zero
            || session.EndsUtc.Offset != TimeSpan.Zero
            || session.EndsUtc < session.StartedUtc
            || session.SamplingInterval <= TimeSpan.Zero
            || session.Duration <= TimeSpan.Zero
        )
        {
            return "The Profiling archive session is invalid.";
        }

        if (archive.Markers.Any(marker => marker is null || !Enum.IsDefined(marker.Scope)
            || string.IsNullOrWhiteSpace(marker.Kind) || marker.Kind.Length > 128
            || string.IsNullOrWhiteSpace(marker.Name) || marker.Name.Trim().Length > 100
            || marker.TimestampUtc.Offset != TimeSpan.Zero
            || marker.Scope == ProfilingMarkerScope.Session && marker.NodeKey is not null
            || marker.Scope == ProfilingMarkerScope.Node && string.IsNullOrWhiteSpace(marker.NodeKey)))
        {
            return "The Profiling archive marker scope or label is invalid.";
        }

        if (archive.Kind == RuntimeProfilingArchiveKind.Session && !IsTerminal(session.State))
        {
            return "A session archive must contain a terminal session.";
        }

        if (archive.Kind == RuntimeProfilingArchiveKind.Snapshot && archive.Snapshots.Count != 1)
        {
            return "A snapshot archive must contain exactly one snapshot.";
        }

        var nodeKeys = archive.Nodes.Select(item => item.Identity.Key).ToArray();
        if (
            nodeKeys.Any(key => !IsPublicKey(key))
            || nodeKeys.Distinct(StringComparer.Ordinal).Count() != nodeKeys.Length
            || archive.Kind == RuntimeProfilingArchiveKind.Snapshot && nodeKeys.Length != 1
        )
        {
            return "The Profiling archive node identities are invalid.";
        }

        var knownNodes = nodeKeys.ToHashSet(StringComparer.Ordinal);
        var snapshotKeys = archive.Snapshots.Select(item => item.Identity.Key).ToArray();
        if (
            snapshotKeys.Any(key => !IsPublicKey(key))
            || snapshotKeys.Distinct(StringComparer.Ordinal).Count() != snapshotKeys.Length
            || archive.Snapshots.Any(item =>
                item.SessionKey != session.Identity.Key
                || !knownNodes.Contains(item.NodeKey)
                || item.TimestampUtc.Offset != TimeSpan.Zero
                || item.Sequence <= 0
                || item.ScheduledElapsed < TimeSpan.Zero
                || item.CaptureStartedElapsed < TimeSpan.Zero
                || item.CaptureDuration < TimeSpan.Zero
            )
        )
        {
            return "The Profiling archive snapshots are invalid or inconsistent.";
        }

        if (
            archive.Participations.Any(item =>
                item.SessionKey != session.Identity.Key || !knownNodes.Contains(item.NodeKey)
            )
            || archive.RuntimeContexts.Any(item =>
                item.SessionKey != session.Identity.Key || !knownNodes.Contains(item.NodeKey)
            )
            || archive.Markers.Where(marker => marker.Scope == ProfilingMarkerScope.Session).Any(item => item.SessionKey != session.Identity.Key)
            || archive.Markers.Where(marker => marker.Scope == ProfilingMarkerScope.Node).Any(item =>
                item.SessionKey != session.Identity.Key || !knownNodes.Contains(item.NodeKey)
            )
        )
        {
            return "The Profiling archive contains inconsistent session or node references.";
        }

        var segmentReferences = archive.Segments.Select(item => item.Reference).ToArray();
        var knownSegments = segmentReferences.ToHashSet();
        if (
            segmentReferences.Any(reference => reference <= 0)
            || knownSegments.Count != segmentReferences.Length
            || archive.Segments.Any(item =>
                item.Segment is null
                || item.Segment.SessionKey != session.Identity.Key
                || !knownNodes.Contains(item.Segment.NodeKey)
                || item.ParentReference == item.Reference
                || item.ParentReference is { } parent && !knownSegments.Contains(parent)
            )
        )
        {
            return "The Profiling archive segments are invalid or inconsistent.";
        }

        var segmentNodes = archive.Segments.ToDictionary(
            item => item.Reference,
            item => item.Segment.NodeKey
        );
        if (
            archive.Segments.Any(item =>
                item.ParentReference is { } parent
                && segmentNodes[parent] != item.Segment.NodeKey
            )
            || archive.MetricObservations.Any(item =>
                item.Observation is null
                || item.Observation.SessionKey != session.Identity.Key
                || !knownNodes.Contains(item.Observation.NodeKey)
                || item.SegmentReference is { } segment && (
                    !knownSegments.Contains(segment)
                    || segmentNodes[segment] != item.Observation.NodeKey
                )
            )
        )
        {
            return "The Profiling archive segment relationships are inconsistent.";
        }

        return null;
    }

    private static ImportMapping MapForImport(RuntimeProfilingArchive archive)
    {
        var sessionIdentity = ProfilingIdentityFactory.CreateRuntimeSession();
        var nodeIdentities = archive.Nodes.ToDictionary(
            item => item.Identity.Key,
            _ => ProfilingIdentityFactory.CreateNode(),
            StringComparer.Ordinal
        );
        var snapshotIdentities = archive.Snapshots.ToDictionary(
            item => item.Identity.Key,
            _ => ProfilingIdentityFactory.CreateRuntimeSnapshot(),
            StringComparer.Ordinal
        );
        var importedSession = archive.Session with
        {
            Identity = sessionIdentity,
            Name = archive.Kind == RuntimeProfilingArchiveKind.Snapshot
                ? $"Imported snapshot — {archive.Session.Name ?? archive.Session.Identity.Key} — #{archive.Snapshots[0].Sequence}"
                : archive.Session.Name,
            State = archive.Kind == RuntimeProfilingArchiveKind.Snapshot
                ? RuntimeProfilingSessionState.Completed
                : archive.Session.State,
            CompletedUtc = archive.Kind == RuntimeProfilingArchiveKind.Snapshot
                ? archive.Snapshots[0].TimestampUtc
                : archive.Session.CompletedUtc,
            IsPinned = archive.Kind == RuntimeProfilingArchiveKind.Session && archive.Session.IsPinned,
            Tags = archive.Session.Tags?.ToArray() ?? [],
        };
        var importedNodes = archive.Nodes
            .Select(node =>
            {
                var identity = nodeIdentities[node.Identity.Key];
                var processStartedUtc = archive.RuntimeContexts
                    .FirstOrDefault(context => context.NodeKey == node.Identity.Key)
                    ?.ProcessStartedUtc ?? archive.ExportedUtc;
                return node with
                {
                    Identity = identity,
                    Correlation = new(
                        $"profiling-import-{sessionIdentity.Id:N}-{identity.Key}",
                        processStartedUtc
                    ),
                };
            })
            .ToArray();

        ProfilingNodeIdentity NodeIdentity(string sourceKey) => nodeIdentities[sourceKey];

        var participations = archive.Participations
            .Select(item => item with
            {
                SessionId = sessionIdentity.Id,
                SessionKey = sessionIdentity.Key,
                NodeId = NodeIdentity(item.NodeKey).Id,
                NodeKey = NodeIdentity(item.NodeKey).Key,
                State = archive.Kind == RuntimeProfilingArchiveKind.Snapshot
                    ? RuntimeProfilingParticipationState.Completed
                    : item.State,
                CompletedUtc = archive.Kind == RuntimeProfilingArchiveKind.Snapshot
                    ? archive.Snapshots[0].TimestampUtc
                    : item.CompletedUtc,
                SuccessfulCaptureCount = archive.Kind == RuntimeProfilingArchiveKind.Snapshot
                    ? Math.Max(1, item.SuccessfulCaptureCount)
                    : item.SuccessfulCaptureCount,
            })
            .ToArray();
        var contexts = archive.RuntimeContexts
            .Select(item => item with
            {
                SessionId = sessionIdentity.Id,
                SessionKey = sessionIdentity.Key,
                NodeId = NodeIdentity(item.NodeKey).Id,
                NodeKey = NodeIdentity(item.NodeKey).Key,
            })
            .ToArray();
        var snapshots = archive.Snapshots
            .Select(item => item with
            {
                Identity = snapshotIdentities[item.Identity.Key],
                SessionId = sessionIdentity.Id,
                SessionKey = sessionIdentity.Key,
                NodeId = NodeIdentity(item.NodeKey).Id,
                NodeKey = NodeIdentity(item.NodeKey).Key,
            })
            .ToArray();
        var markers = archive.Markers.Select(item => item with
        {
            Id = Guid.NewGuid(), SessionId = sessionIdentity.Id, SessionKey = sessionIdentity.Key,
            NodeId = item.Scope == ProfilingMarkerScope.Node ? NodeIdentity(item.NodeKey).Id : null,
            NodeKey = item.Scope == ProfilingMarkerScope.Node ? NodeIdentity(item.NodeKey).Key : null
        }).ToArray();
        var segmentIds = archive.Segments.ToDictionary(item => item.Reference, _ => Guid.NewGuid());
        var segments = archive.Segments
            .Select(item => item.Segment with
            {
                Id = segmentIds[item.Reference],
                SessionId = sessionIdentity.Id,
                SessionKey = sessionIdentity.Key,
                NodeId = NodeIdentity(item.Segment.NodeKey).Id,
                NodeKey = NodeIdentity(item.Segment.NodeKey).Key,
                ParentSegmentId = item.ParentReference is { } parent ? segmentIds[parent] : null,
                Tags = item.Segment.Tags?.ToArray() ?? [],
            })
            .ToArray();
        var observations = archive.MetricObservations
            .Select(item => item.Observation with
            {
                Id = Guid.NewGuid(),
                SessionId = sessionIdentity.Id,
                SessionKey = sessionIdentity.Key,
                NodeId = NodeIdentity(item.Observation.NodeKey).Id,
                NodeKey = NodeIdentity(item.Observation.NodeKey).Key,
                SegmentId = item.SegmentReference is { } segment ? segmentIds[segment] : null,
            })
            .ToArray();

        return new(
            new RuntimeProfilingSessionData
            {
                Session = importedSession,
                Nodes = importedNodes,
                Participations = participations,
                RuntimeContexts = contexts,
                Snapshots = snapshots,
                Markers = markers,
                Segments = segments,
                MetricObservations = observations,
            },
            nodeIdentities.ToDictionary(
                item => item.Key,
                item => item.Value.Key,
                StringComparer.Ordinal
            ),
            snapshotIdentities.ToDictionary(
                item => item.Key,
                item => item.Value.Key,
                StringComparer.Ordinal
            )
        );
    }

    private IResultError ValidateAvailability() =>
        options is null || !options.Enabled
            ? new ProfilingDisabledError()
            : store is null
                ? new ProfilingUnavailableError("No profiling store is registered.")
                : null;

    private static bool IsTerminal(RuntimeProfilingSessionState state) =>
        state
            is RuntimeProfilingSessionState.Completed
                or RuntimeProfilingSessionState.CompletedWithWarnings
                or RuntimeProfilingSessionState.Stopped
                or RuntimeProfilingSessionState.Failed;

    private static bool IsPublicKey(string value) =>
        value?.Length == 8
        && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9');

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var result = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        result.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        result.Converters.Add(new ProfilingSessionIdentityConverter());
        result.Converters.Add(new ProfilingNodeIdentityConverter());
        result.Converters.Add(new ProfilingSnapshotIdentityConverter());
        return result;
    }

    private static Result ArchiveFailure(string message) =>
        Result.Failure().WithError(new ProfilingArchiveError(message));

    private static Result<T> ArchiveFailure<T>(string message) =>
        Failure<T>(new ProfilingArchiveError(message));

    private static Result<T> Failure<T>(IResultError error) =>
        Result<T>.Failure().WithError(error);

    private static Result<TTarget> CopyFailure<TTarget, TSource>(IResult<TSource> source) =>
        Result<TTarget>.Failure().WithErrors(source.Errors).WithMessages(source.Messages);

    private sealed record ImportMapping(
        RuntimeProfilingSessionData Data,
        IReadOnlyDictionary<string, string> NodeKeys,
        IReadOnlyDictionary<string, string> SnapshotKeys
    );

    private abstract class ProfilingIdentityConverter<TIdentity> : JsonConverter<TIdentity>
    {
        public sealed override TIdentity Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (
                root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Count() != 1
                || !root.TryGetProperty("key", out var keyProperty)
                || keyProperty.ValueKind != JsonValueKind.String
                || !IsPublicKey(keyProperty.GetString())
            )
            {
                throw new JsonException("A valid readable Profiling identity is required.");
            }

            return this.Create(keyProperty.GetString());
        }

        public sealed override void Write(
            Utf8JsonWriter writer,
            TIdentity value,
            JsonSerializerOptions options
        )
        {
            writer.WriteStartObject();
            writer.WriteString("key", this.GetKey(value));
            writer.WriteEndObject();
        }

        protected abstract TIdentity Create(string key);

        protected abstract string GetKey(TIdentity identity);
    }

    private sealed class ProfilingSessionIdentityConverter
        : ProfilingIdentityConverter<RuntimeProfilingSessionIdentity>
    {
        protected override RuntimeProfilingSessionIdentity Create(string key) => new(Guid.NewGuid(), key);

        protected override string GetKey(RuntimeProfilingSessionIdentity identity) => identity.Key;
    }

    private sealed class ProfilingNodeIdentityConverter
        : ProfilingIdentityConverter<ProfilingNodeIdentity>
    {
        protected override ProfilingNodeIdentity Create(string key) => new(Guid.NewGuid(), key);

        protected override string GetKey(ProfilingNodeIdentity identity) => identity.Key;
    }

    private sealed class ProfilingSnapshotIdentityConverter
        : ProfilingIdentityConverter<RuntimeProfilingSnapshotIdentity>
    {
        protected override RuntimeProfilingSnapshotIdentity Create(string key) => new(Guid.NewGuid(), key);

        protected override string GetKey(RuntimeProfilingSnapshotIdentity identity) => identity.Key;
    }
}
