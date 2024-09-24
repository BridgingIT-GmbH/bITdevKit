// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Infrastructure;

using DevKit.Infrastructure.EntityFramework;
using Domain;
using Microsoft.EntityFrameworkCore;

public class MarketingDbContext(DbContextOptions<MarketingDbContext> options) : ModuleDbContextBase(options)
{
    // All aggregate roots and entities are exposed as dbsets
    public DbSet<Customer> Customers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TODO: seed some data here

        base.OnModelCreating(modelBuilder);
    }
}