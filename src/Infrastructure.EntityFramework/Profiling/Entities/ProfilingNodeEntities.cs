// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

/// <summary>Represents one process-lifetime profiling node.</summary>
/// <example><code>public DbSet&lt;ProfilingNodeEntity&gt; ProfilingNodes { get; set; }</code></example>
[Table("__Profiling_Nodes")]
[Index(nameof(Key), IsUnique = true)]
[Index(nameof(BroadcastNodeIdentity), nameof(ProcessStartedUtc), IsUnique = true)]
public sealed class ProfilingNodeEntity
{
    private DateTimeOffset processStartedUtc;
    private DateTimeOffset executionStartedUtc;

    /// <summary>Gets or sets the node identifier.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the readable node key.</summary>
    [Required]
    [MaxLength(8)]
    public string Key { get; set; }

    /// <summary>Gets or sets the private Broadcast node identity.</summary>
    [MaxLength(256)]
    public string BroadcastNodeIdentity { get; set; }

    /// <summary>Gets or sets the process start timestamp.</summary>
    [Required]
    public DateTimeOffset ProcessStartedUtc
    {
        get => this.processStartedUtc;
        set
        {
            this.processStartedUtc = value;
            this.ProcessStartedUtcTicks = value.UtcTicks;
        }
    }

    /// <summary>Gets or sets the exact cached process-start ticks independent of database timestamp precision.</summary>
    /// <example><code>var ticks = entity.ProcessStartedUtcTicks;</code></example>
    public long ProcessStartedUtcTicks { get; set; }

    /// <summary>Gets or sets hostname metadata.</summary>
    [MaxLength(256)]
    public string HostName { get; set; }

    /// <summary>Gets or sets the cached process-start UTC independently of Broadcast registration time.</summary>
    /// <example><code>var utc = node.ExecutionStartedUtc;</code></example>
    public DateTimeOffset ExecutionStartedUtc
    {
        get => this.executionStartedUtc;
        set
        {
            this.executionStartedUtc = value;
            this.ExecutionStartedUtcTicks = value.UtcTicks;
        }
    }

    /// <summary>Gets or sets lossless process identity ticks for cross-provider correlation.</summary>
    /// <example><code>var ticks = entity.ExecutionStartedUtcTicks;</code></example>
    public long ExecutionStartedUtcTicks { get; set; }

    /// <summary>Gets or sets the bounded display name.</summary>
    /// <example><code>node.DisplayName = "worker-a";</code></example>
    [MaxLength(128)]
    public string DisplayName { get; set; }

    /// <summary>Gets or sets bounded application-version metadata.</summary>
    /// <example><code>node.ApplicationVersion = "1.2.3";</code></example>
    [MaxLength(128)]
    public string ApplicationVersion { get; set; }

    /// <summary>Gets or sets process identifier metadata.</summary>
    [Required]
    public int ProcessId { get; set; }
}

/// <summary>Represents one node's mutable participation in a profiling session.</summary>
/// <example><code>public DbSet&lt;RuntimeProfilingParticipationEntity&gt; ProfilingParticipations { get; set; }</code></example>
[Table("__Profiling_RuntimeParticipations")]
[PrimaryKey(nameof(SessionId), nameof(NodeId))]
[Index(nameof(SessionId), nameof(State))]
public sealed class RuntimeProfilingParticipationEntity
{
    private DateTimeOffset joinedUtc;
    private DateTimeOffset? completedUtc;
    /// <summary>Gets or sets the session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the node identifier.</summary>
    public Guid NodeId { get; set; }

    /// <summary>Gets or sets the node role.</summary>
    [Required]
    public RuntimeProfilingNodeRole Role { get; set; }

    /// <summary>Gets or sets the participation state.</summary>
    [Required]
    public RuntimeProfilingParticipationState State { get; set; }

    /// <summary>Gets or sets when the node joined.</summary>
    [Required]
    public DateTimeOffset JoinedUtc
    {
        get => this.joinedUtc;
        set { this.joinedUtc = value; this.JoinedUtcTicks = value.UtcTicks; }
    }

    /// <summary>Gets or sets exact UTC ticks for the actual collection-window start.</summary>
    /// <example><code>var ticks = participation.JoinedUtcTicks;</code></example>
    public long JoinedUtcTicks { get; set; }

    /// <summary>Gets or sets when local participation ended.</summary>
    public DateTimeOffset? CompletedUtc
    {
        get => this.completedUtc;
        set { this.completedUtc = value; this.CompletedUtcTicks = value?.UtcTicks; }
    }

    /// <summary>Gets or sets exact UTC ticks for the actual collection-window end.</summary>
    /// <example><code>var ticks = participation.CompletedUtcTicks;</code></example>
    public long? CompletedUtcTicks { get; set; }

    /// <summary>Gets or sets successful capture count.</summary>
    [Required]
    public long SuccessfulCaptureCount { get; set; }

    /// <summary>Gets or sets skipped capture count.</summary>
    [Required]
    public long SkippedCaptureCount { get; set; }

    /// <summary>Gets or sets failed capture count.</summary>
    [Required]
    public long FailedCaptureCount { get; set; }

    /// <summary>Gets or sets a safe optional failure description.</summary>
    [MaxLength(4000)]
    public string Failure { get; set; }

    /// <summary>Gets or sets the optimistic concurrency token.</summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the owning session.</summary>
    [Required]
    [ForeignKey(nameof(SessionId))]
    public RuntimeProfilingSessionEntity Session { get; set; }

    /// <summary>Gets or sets the participating node.</summary>
    [Required]
    [ForeignKey(nameof(NodeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ProfilingNodeEntity Node { get; set; }

    /// <summary>Advances the optimistic concurrency token.</summary>
    /// <example><code>entity.AdvanceConcurrencyVersion();</code></example>
    public void AdvanceConcurrencyVersion() => this.ConcurrencyVersion = Guid.NewGuid();
}

/// <summary>Represents immutable runtime context owned by a profiling session JSON document.</summary>
/// <example><code>session.RuntimeContexts.Add(new RuntimeProfilingRuntimeContextEntity { NodeId = nodeId });</code></example>
public sealed class RuntimeProfilingRuntimeContextEntity
{
    /// <summary>Gets or sets the session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the node identifier.</summary>
    public Guid NodeId { get; set; }

    /// <summary>Gets or sets application name.</summary>
    [MaxLength(256)]
    public string ApplicationName { get; set; }

    /// <summary>Gets or sets application version.</summary>
    [MaxLength(256)]
    public string ApplicationVersion { get; set; }

    /// <summary>Gets or sets runtime description.</summary>
    [MaxLength(512)]
    public string RuntimeDescription { get; set; }

    /// <summary>Gets or sets runtime version.</summary>
    [MaxLength(128)]
    public string RuntimeVersion { get; set; }

    /// <summary>Gets or sets operating-system description.</summary>
    [MaxLength(1024)]
    public string OperatingSystemDescription { get; set; }

    /// <summary>Gets or sets operating-system architecture.</summary>
    [MaxLength(64)]
    public string OperatingSystemArchitecture { get; set; }

    /// <summary>Gets or sets process architecture.</summary>
    [MaxLength(64)]
    public string ProcessArchitecture { get; set; }

    /// <summary>Gets or sets whether server GC is active.</summary>
    public bool? ServerGarbageCollection { get; set; }

    /// <summary>Gets or sets logical processor count.</summary>
    public int? LogicalProcessorCount { get; set; }

    /// <summary>Gets or sets process start timestamp.</summary>
    [Required]
    public DateTimeOffset ProcessStartedUtc { get; set; }

    /// <summary>Gets or sets whether a debugger was attached.</summary>
    [Required]
    public bool DebuggerAttached { get; set; }
}
