// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Text.Json;

/// <summary>Writes stored Profiling evidence as Perfetto-compatible Trace Event JSON.</summary>
/// <param name="options">The shared Profiling feature options.</param>
/// <param name="store">The configured Profiling store.</param>
/// <example><code>await exporter.ExportSessionAsync(sessionKey, destination, cancellationToken);</code></example>
public sealed class ProfilingPerfettoExportService(
    ProfilingOptions options,
    IProfilingStore store = null
) : IProfilingPerfettoExportService
{
    private const int SessionProcessId = 1;
    private const int RuntimeMetricsThreadId = 1;
    private const int EventsThreadId = 2;
    private const int SegmentsThreadId = 3;
    private const int CustomMetricsThreadId = 4;

    /// <inheritdoc />
    public async Task<Result> ExportSessionAsync(
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
            return Failure("A writable Perfetto trace destination is required.");
        }

        var dataResult = await store
            .GetSessionDataAsync(sessionKey, cancellationToken)
            .ConfigureAwait(false);
        if (dataResult.IsFailure)
        {
            return Result.Failure().WithErrors(dataResult.Errors).WithMessages(dataResult.Messages);
        }

        var data = dataResult.Value;
        if (!IsTerminal(data.Session.State))
        {
            return Result
                .Failure()
                .WithError(
                    new ProfilingInvalidStateError(
                        "A Perfetto trace can only be exported from a terminal session."
                    )
                );
        }

        try
        {
            await using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                WriteTrace(writer, data, cancellationToken);
                writer.Flush();
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException
        )
        {
            return Failure("The Perfetto trace could not be serialized.");
        }
    }

    private static void WriteTrace(
        Utf8JsonWriter writer,
        ProfilingSessionData data,
        CancellationToken cancellationToken
    )
    {
        var origin = data.Session.StartedUtc;
        var nodeKeys = GetNodeKeys(data);
        var processIds = nodeKeys
            .Select((nodeKey, index) => (nodeKey, processId: index + 2))
            .ToDictionary(item => item.nodeKey, item => item.processId, StringComparer.Ordinal);

        writer.WriteStartObject();
        writer.WritePropertyName("traceEvents");
        writer.WriteStartArray();

        WriteProcessMetadata(
            writer,
            SessionProcessId,
            $"Profiling session {data.Session.Identity.Key}",
            0
        );
        WriteThreadMetadata(writer, SessionProcessId, EventsThreadId, "Session markers");
        WriteSessionContext(writer, data.Session);

        foreach (var nodeKey in nodeKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processId = processIds[nodeKey];
            WriteProcessMetadata(writer, processId, GetNodeDisplayName(data, nodeKey), processId - 1);
            WriteThreadMetadata(writer, processId, RuntimeMetricsThreadId, "Runtime metric counters");
            WriteThreadMetadata(writer, processId, EventsThreadId, "Events");
            WriteThreadMetadata(writer, processId, SegmentsThreadId, "Measured segments");
            WriteThreadMetadata(writer, processId, CustomMetricsThreadId, "Custom metric counters");

            var context = data.RuntimeContexts.FirstOrDefault(item =>
                string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal)
            );
            WriteNodeContext(writer, data, nodeKey, processId, context);
        }

        foreach (var marker in data.PhaseMarkers.OrderBy(item => item.TimestampUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteInstantEvent(
                writer,
                marker.Name,
                "profiling.phase",
                SessionProcessId,
                EventsThreadId,
                ToMicroseconds(marker.TimestampUtc, origin),
                arguments =>
                {
                    arguments.WriteString("session_key", marker.SessionKey);
                    arguments.WriteString("timestamp_utc", FormatTimestamp(marker.TimestampUtc));
                }
            );
        }

        foreach (var nodeKey in nodeKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processId = processIds[nodeKey];
            WriteNodeEvidence(writer, data, nodeKey, processId, origin, cancellationToken);
        }

        writer.WriteEndArray();
        writer.WriteString("displayTimeUnit", "ms");
        writer.WriteEndObject();
    }

    private static void WriteNodeEvidence(
        Utf8JsonWriter writer,
        ProfilingSessionData data,
        string nodeKey,
        int processId,
        DateTimeOffset origin,
        CancellationToken cancellationToken
    )
    {
        foreach (
            var snapshot in data.Snapshots
                .Where(item => string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal))
                .OrderBy(item => item.TimestampUtc)
                .ThenBy(item => item.Sequence)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timestamp = ToMicroseconds(snapshot.TimestampUtc, origin);
            WriteInstantEvent(
                writer,
                $"Snapshot #{snapshot.Sequence.ToString(CultureInfo.InvariantCulture)}",
                "profiling.snapshot",
                processId,
                EventsThreadId,
                timestamp,
                arguments =>
                {
                    arguments.WriteString("snapshot_key", snapshot.Identity.Key);
                    arguments.WriteString("node_key", snapshot.NodeKey);
                    arguments.WriteString("timestamp_utc", FormatTimestamp(snapshot.TimestampUtc));
                }
            );
            WriteSnapshotCounters(writer, snapshot, processId, timestamp);
        }

        foreach (
            var marker in data.ActionMarkers
                .Where(item => string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal))
                .OrderBy(item => item.TimestampUtc)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteInstantEvent(
                writer,
                marker.Name,
                "profiling.action",
                processId,
                EventsThreadId,
                ToMicroseconds(marker.TimestampUtc, origin),
                arguments =>
                {
                    arguments.WriteString("session_key", marker.SessionKey);
                    arguments.WriteString("node_key", marker.NodeKey);
                    arguments.WriteString("timestamp_utc", FormatTimestamp(marker.TimestampUtc));
                }
            );
        }

        var nodeSegments = data.Segments
            .Where(item => string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal))
            .OrderBy(item => item.StartedUtc)
            .ToArray();
        var segmentsById = nodeSegments.ToDictionary(item => item.Id);
        foreach (var segment in nodeSegments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteSegment(writer, segment, segmentsById, processId, origin);
        }

        foreach (
            var observation in data.MetricObservations
                .Where(item => string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal))
                .OrderBy(item => item.TimestampUtc)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!double.IsFinite(observation.Value))
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(observation.Unit)
                ? observation.MetricIdentifier
                : $"{observation.MetricIdentifier} ({observation.Unit})";
            WriteCounterEvent(
                writer,
                name,
                $"profiling.custom_metric.{observation.Kind.ToString().ToLowerInvariant()}",
                processId,
                CustomMetricsThreadId,
                ToMicroseconds(observation.TimestampUtc, origin),
                "value",
                observation.Value
            );
        }
    }

    private static void WriteSessionContext(Utf8JsonWriter writer, ProfilingSession session) =>
        WriteInstantEvent(
            writer,
            "Session context",
            "profiling.context",
            SessionProcessId,
            EventsThreadId,
            0,
            arguments =>
            {
                arguments.WriteString("session_key", session.Identity.Key);
                WriteOptionalString(arguments, "name", session.Name);
                arguments.WriteString("state", session.State.ToString());
                arguments.WriteString("started_utc", FormatTimestamp(session.StartedUtc));
                arguments.WriteString("ends_utc", FormatTimestamp(session.EndsUtc));
                if (session.CompletedUtc is { } completedUtc)
                {
                    arguments.WriteString("completed_utc", FormatTimestamp(completedUtc));
                }

                arguments.WriteNumber("sampling_interval_ms", session.SamplingInterval.TotalMilliseconds);
                arguments.WriteNumber("duration_ms", session.Duration.TotalMilliseconds);
                arguments.WriteBoolean("pinned", session.IsPinned);
                if (session.Tags.Count > 0)
                {
                    arguments.WriteString("tags", string.Join(", ", session.Tags));
                }

                WriteOptionalString(arguments, "note", session.Note);
            }
        );

    private static void WriteNodeContext(
        Utf8JsonWriter writer,
        ProfilingSessionData data,
        string nodeKey,
        int processId,
        ProfilingRuntimeContext context
    )
    {
        var node = data.Nodes.FirstOrDefault(item =>
            string.Equals(item.Identity.Key, nodeKey, StringComparison.Ordinal)
        );
        var snapshot = data.Snapshots.FirstOrDefault(item =>
            string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal)
        );
        WriteInstantEvent(
            writer,
            "Runtime context",
            "profiling.context",
            processId,
            EventsThreadId,
            0,
            arguments =>
            {
                arguments.WriteString("node_key", nodeKey);
                WriteOptionalString(arguments, "host_name", node?.HostName ?? snapshot?.HostName);
                if ((node?.ProcessId ?? snapshot?.ProcessId) is { } processIdentifier)
                {
                    arguments.WriteNumber("process_id", processIdentifier);
                }

                if (context is null)
                {
                    return;
                }

                WriteOptionalString(arguments, "application_name", context.ApplicationName);
                WriteOptionalString(arguments, "application_version", context.ApplicationVersion);
                WriteOptionalString(arguments, "runtime", context.RuntimeDescription);
                WriteOptionalString(arguments, "runtime_version", context.RuntimeVersion);
                WriteOptionalString(
                    arguments,
                    "operating_system",
                    context.OperatingSystemDescription
                );
                WriteOptionalString(
                    arguments,
                    "operating_system_architecture",
                    context.OperatingSystemArchitecture
                );
                WriteOptionalString(
                    arguments,
                    "process_architecture",
                    context.ProcessArchitecture
                );
                if (context.ServerGarbageCollection is { } serverGarbageCollection)
                {
                    arguments.WriteBoolean("server_gc", serverGarbageCollection);
                }

                if (context.LogicalProcessorCount is { } logicalProcessorCount)
                {
                    arguments.WriteNumber("logical_processor_count", logicalProcessorCount);
                }

                arguments.WriteString(
                    "process_started_utc",
                    FormatTimestamp(context.ProcessStartedUtc)
                );
                arguments.WriteBoolean("debugger_attached", context.DebuggerAttached);
            }
        );
    }

    private static void WriteSegment(
        Utf8JsonWriter writer,
        ProfilingSegment segment,
        IReadOnlyDictionary<Guid, ProfilingSegment> segmentsById,
        int processId,
        DateTimeOffset origin
    )
    {
        var elapsed = segment.Elapsed
            ?? (segment.EndedUtc is { } endedUtc ? endedUtc - segment.StartedUtc : null);
        var duration = elapsed is { } value && value >= TimeSpan.Zero
            ? ToMicroseconds(value)
            : (long?)null;
        var parentName = segment.ParentSegmentId is { } parentId
            && segmentsById.TryGetValue(parentId, out var parent)
                ? parent.Name
                : null;
        Action<Utf8JsonWriter> writeArguments = arguments =>
        {
            arguments.WriteString("session_key", segment.SessionKey);
            arguments.WriteString("node_key", segment.NodeKey);
            arguments.WriteString("outcome", segment.Outcome.ToString());
            arguments.WriteString("started_utc", FormatTimestamp(segment.StartedUtc));
            if (segment.EndedUtc is { } endedUtc)
            {
                arguments.WriteString("ended_utc", FormatTimestamp(endedUtc));
            }

            if (elapsed is { } elapsedValue)
            {
                arguments.WriteNumber("duration_ms", elapsedValue.TotalMilliseconds);
            }

            WriteOptionalString(arguments, "parent_segment", parentName);
            WriteOptionalString(arguments, "correlation_id", segment.CorrelationId);
            WriteOptionalString(arguments, "exception_type", segment.ExceptionType);
            WriteOptionalString(arguments, "exception_message", segment.ExceptionMessage);
            WriteOptionalString(arguments, "note", segment.Note);
            if (segment.Tags.Count > 0)
            {
                arguments.WriteString("tags", string.Join(", ", segment.Tags));
            }

            arguments.WriteBoolean(
                "collection_ended_before_operation",
                segment.CollectionEndedBeforeOperation
            );
        };

        if (duration is { } durationMicroseconds)
        {
            WriteCompleteEvent(
                writer,
                segment.Name,
                "profiling.segment",
                processId,
                SegmentsThreadId,
                ToMicroseconds(segment.StartedUtc, origin),
                durationMicroseconds,
                writeArguments
            );
            return;
        }

        WriteInstantEvent(
            writer,
            $"{segment.Name} (incomplete)",
            "profiling.segment",
            processId,
            SegmentsThreadId,
            ToMicroseconds(segment.StartedUtc, origin),
            writeArguments
        );
    }

    private static void WriteSnapshotCounters(
        Utf8JsonWriter writer,
        ProfilingSnapshot snapshot,
        int processId,
        long timestamp
    )
    {
        WriteCounter(writer, "Capture duration", "profiling.collection", processId, timestamp, "milliseconds", snapshot.CaptureDuration.TotalMilliseconds);
        WriteCounter(writer, "Scheduled elapsed", "profiling.collection", processId, timestamp, "milliseconds", snapshot.ScheduledElapsed.TotalMilliseconds);
        WriteCounter(writer, "Capture-start elapsed", "profiling.collection", processId, timestamp, "milliseconds", snapshot.CaptureStartedElapsed.TotalMilliseconds);
        WriteCounter(writer, "Skipped captures", "profiling.collection", processId, timestamp, "count", snapshot.SkippedCaptureCount);
        WriteCounter(writer, "Failed captures", "profiling.collection", processId, timestamp, "count", snapshot.FailedCaptureCount);
        WriteCounter(writer, "CPU usage", "profiling.cpu", processId, timestamp, "percent", snapshot.CpuUsagePercent);
        WriteCounter(writer, "Process CPU time", "profiling.cpu", processId, timestamp, "milliseconds", snapshot.ProcessCpuDuration?.TotalMilliseconds);
        WriteCounter(writer, "Logical processors", "profiling.runtime", processId, timestamp, "count", snapshot.LogicalProcessorCount);
        WriteCounter(writer, "Working set", "profiling.memory", processId, timestamp, "bytes", snapshot.WorkingSetBytes);
        WriteCounter(writer, "Private memory", "profiling.memory", processId, timestamp, "bytes", snapshot.PrivateMemoryBytes);
        WriteCounter(writer, "Managed memory", "profiling.memory", processId, timestamp, "bytes", snapshot.ManagedMemoryBytes);
        WriteCounter(writer, "Total physical memory", "profiling.memory", processId, timestamp, "bytes", snapshot.TotalPhysicalMemoryBytes);
        WriteCounter(writer, "Available physical memory", "profiling.memory", processId, timestamp, "bytes", snapshot.AvailablePhysicalMemoryBytes);
        WriteCounter(writer, "Used physical memory", "profiling.memory", processId, timestamp, "bytes", snapshot.UsedPhysicalMemoryBytes);
        WriteCounter(writer, "Managed heap size", "profiling.memory", processId, timestamp, "bytes", snapshot.ManagedHeapSizeBytes);
        WriteCounter(writer, "Fragmented managed heap", "profiling.memory", processId, timestamp, "bytes", snapshot.FragmentedBytes);
        WriteCounter(writer, "Heap fragmentation", "profiling.memory", processId, timestamp, "percent", snapshot.HeapFragmentationPercent);
        WriteCounter(writer, "Runtime memory load", "profiling.memory", processId, timestamp, "bytes", snapshot.MemoryLoadBytes);
        WriteCounter(writer, "Runtime available memory", "profiling.memory", processId, timestamp, "bytes", snapshot.TotalAvailableMemoryBytes);
        WriteCounter(writer, "High memory-load threshold", "profiling.memory", processId, timestamp, "bytes", snapshot.HighMemoryLoadThresholdBytes);
        WriteCounter(writer, "Total committed memory", "profiling.memory", processId, timestamp, "bytes", snapshot.TotalCommittedBytes);
        WriteCounter(writer, "Total allocated memory", "profiling.memory", processId, timestamp, "bytes", snapshot.TotalAllocatedBytes);
        WriteCounter(writer, "Allocation rate", "profiling.memory", processId, timestamp, "bytes_per_second", snapshot.AllocationRateBytesPerSecond);
        WriteCounter(writer, "Memory pressure", "profiling.memory", processId, timestamp, "percent", snapshot.MemoryPressurePercent);
        WriteCounter(writer, "Gen0 collections", "profiling.gc", processId, timestamp, "count", snapshot.Gen0CollectionCount);
        WriteCounter(writer, "Gen1 collections", "profiling.gc", processId, timestamp, "count", snapshot.Gen1CollectionCount);
        WriteCounter(writer, "Gen2 collections", "profiling.gc", processId, timestamp, "count", snapshot.Gen2CollectionCount);
        WriteCounter(writer, "Latest GC index", "profiling.gc", processId, timestamp, "index", snapshot.LatestGcIndex);
        WriteCounter(writer, "Latest GC generation", "profiling.gc", processId, timestamp, "generation", snapshot.LatestGcGeneration);
        WriteCounter(writer, "Latest post-GC managed heap", "profiling.gc", processId, timestamp, "bytes", snapshot.LatestGcManagedHeapBytes);
        WriteCounter(writer, "Latest post-GC LOH", "profiling.gc", processId, timestamp, "bytes", snapshot.LatestGcLargeObjectHeapBytes);
        WriteCounter(writer, "Latest GC compacting", "profiling.gc", processId, timestamp, "boolean", ToNumber(snapshot.LatestGcCompacting));
        WriteCounter(writer, "Latest GC concurrent", "profiling.gc", processId, timestamp, "boolean", ToNumber(snapshot.LatestGcConcurrent));
        WriteCounter(writer, "Latest Gen2 GC index", "profiling.gc", processId, timestamp, "index", snapshot.LatestGen2GcIndex);
        WriteCounter(writer, "Latest post-Gen2 managed heap", "profiling.gc", processId, timestamp, "bytes", snapshot.LatestGen2ManagedHeapBytes);
        WriteCounter(writer, "Latest post-Gen2 LOH", "profiling.gc", processId, timestamp, "bytes", snapshot.LatestGen2LargeObjectHeapBytes);
        WriteCounter(writer, "Latest Gen2 GC compacting", "profiling.gc", processId, timestamp, "boolean", ToNumber(snapshot.LatestGen2GcCompacting));
        WriteCounter(writer, "Latest Gen2 GC concurrent", "profiling.gc", processId, timestamp, "boolean", ToNumber(snapshot.LatestGen2GcConcurrent));
        WriteCounter(writer, "Cumulative GC pause", "profiling.gc", processId, timestamp, "milliseconds", snapshot.CumulativeGcPauseDuration?.TotalMilliseconds);
        WriteCounter(writer, "GC pause", "profiling.gc", processId, timestamp, "percent", snapshot.GcPausePercent);
        WriteCounter(writer, "Pinned objects", "profiling.gc", processId, timestamp, "count", snapshot.PinnedObjectCount);
        WriteCounter(writer, "Finalization pending", "profiling.gc", processId, timestamp, "count", snapshot.FinalizationPendingCount);
        WriteCounter(writer, "Large Object Heap size", "profiling.gc", processId, timestamp, "bytes", snapshot.LargeObjectHeapBytes);
        WriteCounter(writer, "Fragmented Large Object Heap", "profiling.gc", processId, timestamp, "bytes", snapshot.LargeObjectHeapFragmentedBytes);
        WriteCounter(writer, "Large Object Heap fragmentation", "profiling.gc", processId, timestamp, "percent", snapshot.LargeObjectHeapFragmentationPercent);
        WriteCounter(writer, "Server GC", "profiling.gc", processId, timestamp, "boolean", ToNumber(snapshot.ServerGarbageCollection));
        WriteCounter(writer, "Process handles", "profiling.runtime", processId, timestamp, "count", snapshot.ProcessHandleCount);
        WriteCounter(writer, "Process threads", "profiling.runtime", processId, timestamp, "count", snapshot.ProcessThreadCount);
        WriteCounter(writer, "Thread-pool threads", "profiling.runtime", processId, timestamp, "count", snapshot.ThreadPoolThreadCount);
        WriteCounter(writer, "Completed thread-pool work items", "profiling.runtime", processId, timestamp, "count", snapshot.ThreadPoolCompletedWorkItemCount);
        WriteCounter(writer, "Pending thread-pool work items", "profiling.runtime", processId, timestamp, "count", snapshot.ThreadPoolPendingWorkItemCount);
        WriteCounter(writer, "Available worker threads", "profiling.runtime", processId, timestamp, "count", snapshot.ThreadPoolAvailableWorkerThreadCount);
        WriteCounter(writer, "Available completion-port threads", "profiling.runtime", processId, timestamp, "count", snapshot.ThreadPoolAvailableCompletionPortThreadCount);
        WriteCounter(writer, "Active TCP connections", "profiling.network", processId, timestamp, "count", snapshot.ActiveTcpConnectionCount);
        WriteCounter(writer, "TCP listeners", "profiling.network", processId, timestamp, "count", snapshot.TcpListenerCount);
        WriteCounter(writer, "UDP listeners", "profiling.network", processId, timestamp, "count", snapshot.UdpListenerCount);
        WriteCounter(writer, "Used sockets", "profiling.network", processId, timestamp, "count", snapshot.TotalUsedSocketCount);
    }

    private static void WriteProcessMetadata(
        Utf8JsonWriter writer,
        int processId,
        string name,
        int sortIndex
    )
    {
        WriteMetadataEvent(writer, "process_name", processId, 0, arguments =>
            arguments.WriteString("name", name)
        );
        WriteMetadataEvent(writer, "process_sort_index", processId, 0, arguments =>
            arguments.WriteNumber("sort_index", sortIndex)
        );
    }

    private static void WriteThreadMetadata(
        Utf8JsonWriter writer,
        int processId,
        int threadId,
        string name
    ) =>
        WriteMetadataEvent(writer, "thread_name", processId, threadId, arguments =>
            arguments.WriteString("name", name)
        );

    private static void WriteMetadataEvent(
        Utf8JsonWriter writer,
        string name,
        int processId,
        int threadId,
        Action<Utf8JsonWriter> writeArguments
    )
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("cat", "__metadata");
        writer.WriteString("ph", "M");
        writer.WriteNumber("pid", processId);
        writer.WriteNumber("tid", threadId);
        writer.WritePropertyName("args");
        writer.WriteStartObject();
        writeArguments(writer);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteInstantEvent(
        Utf8JsonWriter writer,
        string name,
        string category,
        int processId,
        int threadId,
        long timestamp,
        Action<Utf8JsonWriter> writeArguments
    )
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("cat", category);
        writer.WriteString("ph", "i");
        writer.WriteString("s", "p");
        writer.WriteNumber("ts", timestamp);
        writer.WriteNumber("pid", processId);
        writer.WriteNumber("tid", threadId);
        writer.WritePropertyName("args");
        writer.WriteStartObject();
        writeArguments(writer);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteCompleteEvent(
        Utf8JsonWriter writer,
        string name,
        string category,
        int processId,
        int threadId,
        long timestamp,
        long duration,
        Action<Utf8JsonWriter> writeArguments
    )
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("cat", category);
        writer.WriteString("ph", "X");
        writer.WriteNumber("ts", timestamp);
        writer.WriteNumber("dur", duration);
        writer.WriteNumber("pid", processId);
        writer.WriteNumber("tid", threadId);
        writer.WritePropertyName("args");
        writer.WriteStartObject();
        writeArguments(writer);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteCounter(
        Utf8JsonWriter writer,
        string name,
        string category,
        int processId,
        long timestamp,
        string unit,
        double? value
    )
    {
        if (value is not { } number || !double.IsFinite(number))
        {
            return;
        }

        WriteCounterEvent(
            writer,
            name,
            category,
            processId,
            RuntimeMetricsThreadId,
            timestamp,
            unit,
            number
        );
    }

    private static void WriteCounter(
        Utf8JsonWriter writer,
        string name,
        string category,
        int processId,
        long timestamp,
        string unit,
        long? value
    )
    {
        if (value is not { } number)
        {
            return;
        }

        WriteCounterEvent(
            writer,
            name,
            category,
            processId,
            RuntimeMetricsThreadId,
            timestamp,
            unit,
            number
        );
    }

    private static void WriteCounter(
        Utf8JsonWriter writer,
        string name,
        string category,
        int processId,
        long timestamp,
        string unit,
        int? value
    ) => WriteCounter(writer, name, category, processId, timestamp, unit, (long?)value);

    private static void WriteCounterEvent(
        Utf8JsonWriter writer,
        string name,
        string category,
        int processId,
        int threadId,
        long timestamp,
        string unit,
        double value
    )
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("cat", category);
        writer.WriteString("ph", "C");
        writer.WriteNumber("ts", timestamp);
        writer.WriteNumber("pid", processId);
        writer.WriteNumber("tid", threadId);
        writer.WritePropertyName("args");
        writer.WriteStartObject();
        writer.WriteNumber(unit, value);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteCounterEvent(
        Utf8JsonWriter writer,
        string name,
        string category,
        int processId,
        int threadId,
        long timestamp,
        string unit,
        long value
    )
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("cat", category);
        writer.WriteString("ph", "C");
        writer.WriteNumber("ts", timestamp);
        writer.WriteNumber("pid", processId);
        writer.WriteNumber("tid", threadId);
        writer.WritePropertyName("args");
        writer.WriteStartObject();
        writer.WriteNumber(unit, value);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static IReadOnlyList<string> GetNodeKeys(ProfilingSessionData data) =>
        data
            .Nodes.Select(item => item.Identity.Key)
            .Concat(data.Participations.Select(item => item.NodeKey))
            .Concat(data.RuntimeContexts.Select(item => item.NodeKey))
            .Concat(data.Snapshots.Select(item => item.NodeKey))
            .Concat(data.ActionMarkers.Select(item => item.NodeKey))
            .Concat(data.Segments.Select(item => item.NodeKey))
            .Concat(data.MetricObservations.Select(item => item.NodeKey))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

    private static string GetNodeDisplayName(ProfilingSessionData data, string nodeKey)
    {
        var node = data.Nodes.FirstOrDefault(item =>
            string.Equals(item.Identity.Key, nodeKey, StringComparison.Ordinal)
        );
        var snapshot = data.Snapshots.FirstOrDefault(item =>
            string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal)
        );
        var hostName = node?.HostName ?? snapshot?.HostName ?? "Unknown node";
        var processId = node?.ProcessId ?? snapshot?.ProcessId;
        return processId is null
            ? $"{hostName} · {nodeKey}"
            : $"{hostName} · PID {processId.Value.ToString(CultureInfo.InvariantCulture)} · {nodeKey}";
    }

    private IResultError ValidateAvailability() =>
        options is null || !options.Enabled
            ? new ProfilingDisabledError()
            : store is null
                ? new ProfilingUnavailableError("No profiling store is registered.")
                : null;

    private static bool IsTerminal(ProfilingSessionState state) =>
        state
            is ProfilingSessionState.Completed
                or ProfilingSessionState.CompletedWithWarnings
                or ProfilingSessionState.Stopped
                or ProfilingSessionState.Failed;

    private static long ToMicroseconds(DateTimeOffset timestamp, DateTimeOffset origin) =>
        Math.Max(0, (timestamp - origin).Ticks / 10);

    private static long ToMicroseconds(TimeSpan duration) => Math.Max(0, duration.Ticks / 10);

    private static int? ToNumber(bool? value) => value is null ? null : value.Value ? 1 : 0;

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static void WriteOptionalString(
        Utf8JsonWriter writer,
        string propertyName,
        string value
    )
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static Result Failure(string message) =>
        Result.Failure().WithError(new ProfilingTraceExportError(message));
}