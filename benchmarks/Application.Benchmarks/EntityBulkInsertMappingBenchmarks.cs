// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Measures provider-neutral EF metadata preprocessing without database I/O.
/// </summary>
[MemoryDiagnoser]
public class EntityBulkInsertMappingBenchmarks
{
    private BenchmarkDbContext context;
    private EntityBulkInsertMappingBuilder<FlatEntity> flatBuilder;
    private EntityBulkInsertMappingBuilder<AuditedEntity> auditedBuilder;
    private EntityBulkInsertOptions options;
    private IReadOnlyList<FlatEntity> flatEntities;
    private IReadOnlyList<AuditedEntity> auditedEntities;

    /// <summary>Gets or sets the number of entities preprocessed per operation.</summary>
    [Params(1, 1_000, 10_000)]
    public int EntityCount { get; set; }

    /// <summary>Creates the stable EF model and benchmark inputs.</summary>
    [GlobalSetup]
    public void Setup()
    {
        this.context = new BenchmarkDbContext(
            new DbContextOptionsBuilder<BenchmarkDbContext>()
                .UseInMemoryDatabase($"bulk-benchmark-{Guid.NewGuid():N}")
                .Options
        );
        this.flatBuilder = new EntityBulkInsertMappingBuilder<FlatEntity>();
        this.auditedBuilder = new EntityBulkInsertMappingBuilder<AuditedEntity>();
        this.options = new EntityBulkInsertOptions();
        this.flatEntities = Enumerable
            .Range(1, this.EntityCount)
            .Select(index => new FlatEntity { Id = Guid.NewGuid(), Name = $"flat-{index}" })
            .ToArray();
        this.auditedEntities = Enumerable
            .Range(1, this.EntityCount)
            .Select(index => new AuditedEntity
            {
                Id = Guid.NewGuid(),
                Name = $"audited-{index}",
                AuditState = new AuditState(),
            })
            .ToArray();
    }

    /// <summary>Disposes the benchmark DbContext.</summary>
    [GlobalCleanup]
    public void Cleanup() => this.context?.Dispose();

    /// <summary>Measures analysis and batch creation for a flat entity.</summary>
    [Benchmark(Baseline = true)]
    public int Flat()
    {
        var analysis = this.flatBuilder.Analyze(this.context, this.flatEntities, this.options);
        return this.flatBuilder.Build(analysis).Entities.Count;
    }

    /// <summary>Measures analysis and batch creation with same-table owned audit state.</summary>
    [Benchmark]
    public int Audited()
    {
        var analysis = this.auditedBuilder.Analyze(
            this.context,
            this.auditedEntities,
            this.options
        );
        return this.auditedBuilder.Build(analysis).Entities.Count;
    }

    private sealed class BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FlatEntity>(builder =>
            {
                builder.ToTable("FlatEntities");
                builder.HasKey(entity => entity.Id);
            });
            modelBuilder.Entity<AuditedEntity>(builder =>
            {
                builder.ToTable("AuditedEntities");
                builder.HasKey(entity => entity.Id);
                builder.OwnsOne(entity => entity.AuditState);
            });
        }
    }

    private sealed class FlatEntity : Entity<Guid>
    {
        public string Name { get; set; }
    }

    private sealed class AuditedEntity : Entity<Guid>, IAuditable
    {
        public string Name { get; set; }

        public AuditState AuditState { get; set; }
    }
}
