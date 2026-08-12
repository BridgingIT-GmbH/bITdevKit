// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using Microsoft.EntityFrameworkCore;

/// <summary>Provides Entity Framework model configuration for durable Profiling storage.</summary>
/// <example>
/// <code>
/// protected override void OnModelCreating(ModelBuilder modelBuilder)
/// {
///     base.OnModelCreating(modelBuilder);
///     modelBuilder.ConfigureProfiling();
/// }
/// </code>
/// </example>
public static class ProfilingModelBuilderExtensions
{
    /// <summary>
    /// Configures low-volume session-owned records as JSON documents while retaining hot,
    /// high-volume, and independently addressable records as tables.
    /// </summary>
    /// <param name="modelBuilder">The application model builder.</param>
    /// <returns>The supplied model builder.</returns>
    /// <example><code>modelBuilder.ConfigureProfiling();</code></example>
    public static ModelBuilder ConfigureProfiling(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var session = modelBuilder.Entity<ProfilingSessionEntity>();

        session.OwnsMany(
            x => x.Tags,
            owned =>
            {
                owned.ToJson("Tags");
                owned.WithOwner().HasForeignKey(x => x.SessionId);
            }
        );

        session.OwnsMany(
            x => x.RuntimeContexts,
            owned =>
            {
                owned.ToJson("RuntimeContexts");
                owned.WithOwner().HasForeignKey(x => x.SessionId);
            }
        );

        session.OwnsMany(
            x => x.PhaseMarkers,
            owned =>
            {
                owned.ToJson("PhaseMarkers");
                owned.WithOwner().HasForeignKey(x => x.SessionId);
            }
        );

        session.OwnsMany(
            x => x.ActionMarkers,
            owned =>
            {
                owned.ToJson("ActionMarkers");
                owned.WithOwner().HasForeignKey(x => x.SessionId);
            }
        );

        session.OwnsMany(
            x => x.Segments,
            owned =>
            {
                owned.ToJson("Segments");
                owned.WithOwner().HasForeignKey(x => x.SessionId);
                owned.OwnsMany(x => x.Tags);
            }
        );

        return modelBuilder;
    }
}
