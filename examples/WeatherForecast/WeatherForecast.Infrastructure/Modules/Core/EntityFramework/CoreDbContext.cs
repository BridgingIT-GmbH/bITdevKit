// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;

using System;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

public class CoreDbContext(DbContextOptions<CoreDbContext> options) : ModuleDbContextBase(options), IOutboxDomainEventContext, IOutboxMessageContext
{
    // All aggregate roots and entities are exposed as dbsets
    public DbSet<Forecast> Forecasts { get; set; }

    public DbSet<ForecastType> ForecastTypes { get; set; }

    public DbSet<DbUserAccount> UserAccounts { get; set; }

    public DbSet<TestGuidEntity> TestGuidEntities { get; set; }

    public DbSet<TestIntEntity> TestIntEntities { get; set; }

    public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }

    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // seed some data
        modelBuilder.Entity<ForecastType>().HasData(
            new { Id = Guid.Parse("102954ff-aa73-495b-a730-98f2d5ca10f3"), Name = "AAA", Description = "test" },
            new { Id = Guid.Parse("f059e932-d6ff-406d-ba9d-282fe4fdc084"), Name = "BBB", Description = "test" });

        base.OnModelCreating(modelBuilder);
    }
}