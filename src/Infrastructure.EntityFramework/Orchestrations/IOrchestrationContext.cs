// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;

/// <summary>
/// Defines the Entity Framework capability contract required by the durable orchestration provider.
/// </summary>
/// <remarks>
/// A host <see cref="DbContext" /> opts into orchestration persistence by implementing this interface and
/// exposing the orchestration sets alongside any other feature-specific sets it already supports.
/// </remarks>
/// <example>
/// <code>
/// public class AppDbContext : DbContext, IOrchestrationContext
/// {
///     public DbSet&lt;OrchestrationInstance&gt; OrchestrationInstances { get; set; }
///
///     public DbSet&lt;OrchestrationHistory&gt; OrchestrationHistory { get; set; }
///
///     public DbSet&lt;OrchestrationSignal&gt; OrchestrationSignals { get; set; }
///
///     public DbSet&lt;OrchestrationTimer&gt; OrchestrationTimers { get; set; }
/// }
/// </code>
/// </example>
public interface IOrchestrationContext
{
    /// <summary>
    /// Gets or sets the durable orchestration instance rows.
    /// </summary>
    DbSet<OrchestrationInstance> OrchestrationInstances { get; set; }

    /// <summary>
    /// Gets or sets the append-only orchestration history rows.
    /// </summary>
    DbSet<OrchestrationHistory> OrchestrationHistory { get; set; }

    /// <summary>
    /// Gets or sets the durable orchestration signal rows.
    /// </summary>
    DbSet<OrchestrationSignal> OrchestrationSignals { get; set; }

    /// <summary>
    /// Gets or sets the durable orchestration timer rows.
    /// </summary>
    DbSet<OrchestrationTimer> OrchestrationTimers { get; set; }
}