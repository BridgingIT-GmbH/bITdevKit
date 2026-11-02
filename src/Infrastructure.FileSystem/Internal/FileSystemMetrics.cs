namespace BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Exposes process-wide instrumentation for filesystem storage operations.</summary>
/// <remarks>Counters describe application observations, not proven syscalls or remote RPCs. Fields are writable for isolated diagnostic resets; use Interlocked for concurrent access. Resetting during a measurement invalidates its deltas. Counters never determine storage ownership or correctness.</remarks>
/// <example><code>var before = FileSystemMetrics.Snapshot();</code></example>
public static class FileSystemMetrics
{
    /// <summary>Stores the last native error code observed at an uncertain publication boundary, or zero before any such failure.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.IndeterminateNativeCode);</code></example>
    public static long IndeterminateNativeCode;

    /// <summary>Stores completed file flushes.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.FileFlushes);</code></example>
    public static long FileFlushes;

    /// <summary>Stores instrumented managed file-open attempts, rather than operating-system syscall counts.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.ManagedFileOpenAttempts);</code></example>
    public static long ManagedFileOpenAttempts;

    /// <summary>Stores successful file opens with read access across ordinary files, blob payloads and document payloads.</summary>
    /// <example><code>var reads = Interlocked.Read(ref FileSystemMetrics.FileReadOpens);</code></example>
    public static long FileReadOpens;

    /// <summary>Stores successful file opens with write access across ordinary files, blob payloads and document payloads.</summary>
    /// <example><code>var writes = Interlocked.Read(ref FileSystemMetrics.FileWriteOpens);</code></example>
    public static long FileWriteOpens;

    /// <summary>Stores attempted Linux directory fsync operations.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.DirectorySyncs);</code></example>
    public static long DirectorySyncs;

    /// <summary>Stores attempted native namespace publications.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.NamespaceMoves);</code></example>
    public static long NamespaceMoves;

    /// <summary>Stores instrumented Windows write-through publication requests.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.WriteThroughMoves);</code></example>
    public static long WriteThroughMoves;

    /// <summary>Stores attempted manifest reads, including absent records.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.MetadataReads);</code></example>
    public static long MetadataReads;

    /// <summary>Stores attempted payload read opens.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.PayloadOpens);</code></example>
    public static long PayloadOpens;

    /// <summary>Stores attempted payload staging operations.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.PayloadStages);</code></example>
    public static long PayloadStages;

    /// <summary>Stores bytes in successfully staged payloads.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.StagedBytes);</code></example>
    public static long StagedBytes;

    /// <summary>Stores completed native manifest publications before subsequent diagnostic callbacks.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.Publications);</code></example>
    public static long Publications;

    /// <summary>Stores successfully unlinked unreferenced generation files.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.ReclaimedFiles);</code></example>
    public static long ReclaimedFiles;

    /// <summary>Stores logical bytes in successfully unlinked generation files; open readers may retain allocated storage.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.ReclaimedBytes);</code></example>
    public static long ReclaimedBytes;

    /// <summary>Stores successfully acquired key, generation and cursor owners.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.AuthorityAcquisitions);</code></example>
    public static long AuthorityAcquisitions;

    /// <summary>Stores cumulative authority acquisition time in Stopwatch ticks.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.AuthorityWaitTicks);</code></example>
    public static long AuthorityWaitTicks;

    /// <summary>Stores cumulative disposed authority lifetime in Stopwatch ticks.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.AuthorityHoldTicks);</code></example>
    public static long AuthorityHoldTicks;

    /// <summary>Stores cumulative disposed key-authority lifetime in Stopwatch ticks.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.KeyAuthorityHoldTicks);</code></example>
    public static long KeyAuthorityHoldTicks;

    /// <summary>Stores cumulative disposed generation-authority lifetime in Stopwatch ticks.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.GenerationAuthorityHoldTicks);</code></example>
    public static long GenerationAuthorityHoldTicks;

    /// <summary>Stores cumulative disposed cursor-authority lifetime in Stopwatch ticks.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.CursorAuthorityHoldTicks);</code></example>
    public static long CursorAuthorityHoldTicks;

    /// <summary>Stores shared owners deliberately retained at disposal after uncertainty.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.RetainedOwnershipEvents);</code></example>
    public static long RetainedOwnershipEvents;

    /// <summary>Stores managed discovery steps and reserved manifest inspections in checkpointed cursor pages.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.DiscoveryInspections);</code></example>
    public static long DiscoveryInspections;

    /// <summary>Stores instrumented recoverable staging or reclamation failures.</summary>
    /// <example><code>var observed = Interlocked.Read(ref FileSystemMetrics.CleanupFailures);</code></example>
    public static long CleanupFailures;

    /// <summary>Copies named process counters using atomic reads of each value.</summary>
    /// <remarks>The snapshot is independent and not an atomic transaction across counters. Stopwatch tick durations use the runtime's Stopwatch.Frequency. The last native diagnostic error is available separately through IndeterminateNativeCode.</remarks>
    /// <returns>A new dictionary of instrumentation values suitable for before/after measurement deltas.</returns>
    /// <example><code>var after = FileSystemMetrics.Snapshot(); var bytes = after["stagedBytes"];</code></example>
    public static Dictionary<string, long> Snapshot()
    {
        return new()
        {
            ["managedFileOpenAttempts"] = Interlocked.Read(ref ManagedFileOpenAttempts),
            ["fileReadOpens"] = Interlocked.Read(ref FileReadOpens),
            ["fileWriteOpens"] = Interlocked.Read(ref FileWriteOpens),
            ["fileFlushes"] = Interlocked.Read(ref FileFlushes),
            ["directoryFsyncAttempts"] = Interlocked.Read(ref DirectorySyncs),
            ["namespaceMoves"] = Interlocked.Read(ref NamespaceMoves),
            ["windowsWriteThroughMoves"] = Interlocked.Read(ref WriteThroughMoves),
            ["manifestReadAttempts"] = Interlocked.Read(ref MetadataReads),
            ["payloadReadOpens"] = Interlocked.Read(ref PayloadOpens),
            ["payloadStages"] = Interlocked.Read(ref PayloadStages),
            ["stagedBytes"] = Interlocked.Read(ref StagedBytes),
            ["publications"] = Interlocked.Read(ref Publications),
            ["reclaimedFiles"] = Interlocked.Read(ref ReclaimedFiles),
            ["reclaimedBytes"] = Interlocked.Read(ref ReclaimedBytes),
            ["authorityAcquisitions"] = Interlocked.Read(ref AuthorityAcquisitions),
            ["authorityWaitStopwatchTicks"] = Interlocked.Read(ref AuthorityWaitTicks),
            ["authorityHoldStopwatchTicks"] = Interlocked.Read(ref AuthorityHoldTicks),
            ["retainedOwnershipEvents"] = Interlocked.Read(ref RetainedOwnershipEvents),
            ["keyAuthorityHoldStopwatchTicks"] = Interlocked.Read(ref KeyAuthorityHoldTicks),
            ["generationAuthorityHoldStopwatchTicks"] = Interlocked.Read(ref GenerationAuthorityHoldTicks),
            ["cursorAuthorityHoldStopwatchTicks"] = Interlocked.Read(ref CursorAuthorityHoldTicks),
            ["discoveryInspections"] = Interlocked.Read(ref DiscoveryInspections),
            ["cleanupFailures"] = Interlocked.Read(ref CleanupFailures)
        };
    }
}
