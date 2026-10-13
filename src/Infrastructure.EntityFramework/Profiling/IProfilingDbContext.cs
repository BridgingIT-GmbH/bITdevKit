// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using Microsoft.EntityFrameworkCore;

/// <summary>Provides EF sets for the model registered by ConfigureProfiling, without host-owned property boilerplate.</summary>
/// <remarks>DbContext already implements this contract. Profiling tables are registered explicitly by ConfigureProfiling.</remarks>
/// <example>
/// <code>
/// public sealed class AppDbContext(DbContextOptions&lt;AppDbContext&gt; options)
///     : DbContext(options), IProfilingDbContext
/// {
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         base.OnModelCreating(modelBuilder);
///         modelBuilder.ConfigureProfiling();
///     }
/// }
/// </code>
/// </example>
public interface IProfilingDbContext
{
    /// <summary>Returns an EF set for a configured Profiling entity; inherited DbContext.Set supplies the implementation.</summary>
    /// <example><code>var operations = context.Set&lt;OperationProfilingEntity&gt;();</code></example>
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
}
