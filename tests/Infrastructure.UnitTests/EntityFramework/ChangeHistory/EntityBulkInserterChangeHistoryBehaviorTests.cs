// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.ChangeHistory;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

public class EntityBulkInserterChangeHistoryBehaviorTests
{
    [Fact]
    public async Task InsertAsync_SummaryCapture_PersistsOneBatchSummary()
    {
        // Arrange
        await using var context = CreateContext();
        var inner = new TestBulkInserter();
        var options = CreateOptions(ChangeHistoryBulkInsertCaptureMode.Summary);
        var sut = new EntityBulkInserterChangeHistoryBehavior<BulkEntity, BulkDbContext>(
            context,
            inner,
            options);
        var entities = new[]
        {
            new BulkEntity { Id = Guid.NewGuid(), Name = "first" },
            new BulkEntity { Id = Guid.NewGuid(), Name = "second" }
        };

        // Act
        var result = await sut.InsertAsync(entities);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        inner.InvocationCount.ShouldBe(1);
        var row = await context.ChangeHistory.SingleAsync();
        row.EntityId.ShouldBe("*");
        row.Operation.ShouldBe(ChangeHistoryOperation.BulkInsert.ToString());
        row.CaptureSource.ShouldBe(ChangeHistoryCaptureSource.NativeBulkInsert.ToString());
        row.CaptureStatus.ShouldBe(ChangeHistoryCaptureStatus.Summary.ToString());
        row.AffectedEntityCount.ShouldBe(2);
        row.BulkOperationId.ShouldNotBeNull();
        row.IsRestoreable.ShouldBeFalse();
    }

    [Fact]
    public async Task InsertAsync_DetailedCapture_PersistsProtectedPropertyRowsPerEntity()
    {
        // Arrange
        await using var context = CreateContext();
        var inner = new TestBulkInserter();
        var options = new ChangeHistoryOptions();
        options.Track<BulkEntity>()
            .CaptureBulkInserts(ChangeHistoryBulkInsertCaptureMode.Detailed, maxDetailedEntities: 10)
            .Redact(entity => entity.Email)
            .Exclude(entity => entity.Ignored);
        var sut = new EntityBulkInserterChangeHistoryBehavior<BulkEntity, BulkDbContext>(
            context,
            inner,
            options);
        var entities = new[]
        {
            new BulkEntity
            {
                Id = Guid.NewGuid(),
                Name = "first",
                Email = "first@example.test",
                ApiToken = "first-token",
                Ignored = "ignored"
            },
            new BulkEntity
            {
                Id = Guid.NewGuid(),
                Name = "second",
                Email = "second@example.test",
                ApiToken = "second-token",
                Ignored = "ignored"
            }
        };

        // Act
        var result = await sut.InsertAsync(entities);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var rows = await context.ChangeHistory
            .OrderBy(row => row.EntityId)
            .ThenBy(row => row.ChangeSetSequence)
            .ToListAsync();
        rows.Count.ShouldBe(6);
        rows.Select(row => row.ChangeSetId).Distinct().Count().ShouldBe(2);
        rows.Select(row => row.BulkOperationId).Distinct().Count().ShouldBe(1);
        rows.ShouldAllBe(row => row.Operation == ChangeHistoryOperation.BulkInsert.ToString());
        rows.ShouldAllBe(row => row.CaptureSource == ChangeHistoryCaptureSource.NativeBulkInsert.ToString());
        rows.ShouldAllBe(row => row.AffectedEntityCount == 2);
        rows.ShouldAllBe(row => row.IsRestoreable == false);
        rows.ShouldNotContain(row => row.PropertyName == nameof(BulkEntity.Ignored));
        rows.Where(row => row.PropertyName == nameof(BulkEntity.Email))
            .ShouldAllBe(row => row.NewValue == "\"***REDACTED***\"" && row.NewValueHash != null);
        rows.Where(row => row.PropertyName == nameof(BulkEntity.ApiToken))
            .ShouldAllBe(row => row.NewValue == null && row.NewValueHash != null);
    }

    [Fact]
    public async Task InsertAsync_DetailedCaptureExceedingLimit_DoesNotInvokeInnerInserter()
    {
        // Arrange
        await using var context = CreateContext();
        var inner = new TestBulkInserter();
        var options = CreateOptions(
            ChangeHistoryBulkInsertCaptureMode.Detailed,
            maxDetailedEntities: 1);
        var sut = new EntityBulkInserterChangeHistoryBehavior<BulkEntity, BulkDbContext>(
            context,
            inner,
            options);
        var entities = new[]
        {
            new BulkEntity { Id = Guid.NewGuid(), Name = "first" },
            new BulkEntity { Id = Guid.NewGuid(), Name = "second" }
        };

        // Act
        var action = () => sut.InsertAsync(entities);

        // Assert
        var exception = await action.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain("exceeding the configured limit");
        inner.InvocationCount.ShouldBe(0);
        context.ChangeHistory.ShouldBeEmpty();
    }

    [Fact]
    public async Task InsertAsync_DetailedCaptureWithDefaultId_ThrowsAndDoesNotPersistHistory()
    {
        // Arrange
        await using var context = CreateContext();
        var inner = new TestBulkInserter();
        var options = CreateOptions(ChangeHistoryBulkInsertCaptureMode.Detailed);
        var sut = new EntityBulkInserterChangeHistoryBehavior<BulkEntity, BulkDbContext>(
            context,
            inner,
            options);

        // Act
        var action = () => sut.InsertAsync([new BulkEntity { Name = "database-generated id" }]);

        // Assert
        var exception = await action.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain("requires stable entity identifiers");
        inner.InvocationCount.ShouldBe(1);
        context.ChangeHistory.ShouldBeEmpty();
    }

    [Fact]
    public async Task InsertAsync_InnerFailure_DoesNotPersistHistory()
    {
        // Arrange
        await using var context = CreateContext();
        var inner = new TestBulkInserter { Result = Result<long>.Failure() };
        var options = CreateOptions(ChangeHistoryBulkInsertCaptureMode.Summary);
        var sut = new EntityBulkInserterChangeHistoryBehavior<BulkEntity, BulkDbContext>(
            context,
            inner,
            options);

        // Act
        var result = await sut.InsertAsync([new BulkEntity { Id = Guid.NewGuid(), Name = "first" }]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        inner.InvocationCount.ShouldBe(1);
        context.ChangeHistory.ShouldBeEmpty();
    }

    [Fact]
    public async Task InsertAsync_CaptureDisabled_DelegatesWithoutPersistingHistory()
    {
        // Arrange
        await using var context = CreateContext();
        var inner = new TestBulkInserter();
        var options = new ChangeHistoryOptions();
        options.Track<BulkEntity>();
        var sut = new EntityBulkInserterChangeHistoryBehavior<BulkEntity, BulkDbContext>(
            context,
            inner,
            options);

        // Act
        var result = await sut.InsertAsync([new BulkEntity { Id = Guid.NewGuid(), Name = "first" }]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        inner.InvocationCount.ShouldBe(1);
        context.ChangeHistory.ShouldBeEmpty();
    }

    private static ChangeHistoryOptions CreateOptions(
        ChangeHistoryBulkInsertCaptureMode mode,
        int maxDetailedEntities = 1000)
    {
        var options = new ChangeHistoryOptions();
        options.Track<BulkEntity>()
            .CaptureBulkInserts(mode, maxDetailedEntities);

        return options;
    }

    private static BulkDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BulkDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BulkDbContext(options);
    }

    private sealed class TestBulkInserter : IEntityBulkInserter<BulkEntity>
    {
        public int InvocationCount { get; private set; }

        public Result<long>? Result { get; set; }

        public Task<Result<long>> InsertAsync(
            IEnumerable<BulkEntity> entities,
            CancellationToken cancellationToken = default)
        {
            this.InvocationCount++;
            var items = entities.ToArray();

            return Task.FromResult(this.Result ?? Result<long>.Success(items.LongLength));
        }
    }

    private sealed class BulkDbContext(DbContextOptions<BulkDbContext> options)
        : DbContext(options), IChangeHistoryContext
    {
        public DbSet<BulkEntity> Entities { get; set; }

        public DbSet<ChangeHistoryEntry> ChangeHistory { get; set; }
    }

    private sealed class BulkEntity : Entity<Guid>
    {
        public string Name { get; set; }

        public string Email { get; set; }

        public string ApiToken { get; set; }

        public string Ignored { get; set; }
    }
}