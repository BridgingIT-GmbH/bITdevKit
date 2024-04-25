// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

public abstract class ModuleDbContextBase : DbContext
{
    protected ModuleDbContextBase(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// The Schema property is used to specify the schema that this context should use.
    /// If it's not specified, the name of the context class with the "DBContext" suffix removed is used.
    /// </summary>
    protected string Schema { get; set; }

    /// <summary>
    ///  Specify whether or not to apply configuration during model building.
    ///  The configuration classes should be located in the same assembly as the context it is applied upon.
    /// </summary>
    protected bool ApplyConfigurationsFromAssembly { get; set; } = true;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(c => c.Log((RelationalEventId.CommandExecuted, LogLevel.Debug)));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var schema = this.Schema ?? this.GetType().Name.ToLowerInvariant().Replace("dbcontext", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(schema))
        {
            if (schema.EndsWith("module", StringComparison.OrdinalIgnoreCase) && !schema.Equals("module", StringComparison.OrdinalIgnoreCase))
            {
                schema = schema.Replace("module", string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            modelBuilder.HasDefaultSchema(schema);
        }

        if (this.ApplyConfigurationsFromAssembly)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(this.GetType().Assembly);
        }

        base.OnModelCreating(modelBuilder);
    }
}