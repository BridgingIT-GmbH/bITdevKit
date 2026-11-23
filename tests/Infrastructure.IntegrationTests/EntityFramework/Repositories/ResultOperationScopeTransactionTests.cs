// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Repositories;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
public class ResultOperationScopeTransactionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndOperationAsync_StepFailsAfterSave_RollsBackDatabaseChanges(bool cancelAfterWrite)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TransactionContext>().UseSqlite(connection).Options;
        await using var context = new TransactionContext(options);
        await context.Database.EnsureCreatedAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        using var cancellation = new CancellationTokenSource();
        var failure = new ValidationError("Later operation could not be completed");
        var rowsSaved = 0;
        var sut = Result<int>.Success(42).StartOperation(new EntityFrameworkTransactionScope(transaction));

        var result = await sut
            .TapAsync(async (id, token) =>
            {
                context.Set<TransactionRow>().Add(new TransactionRow { Id = id });
                rowsSaved = await context.SaveChangesAsync(token);
            }, cancellation.Token)
            .BindAsync((_, token) =>
            {
                if (cancelAfterWrite)
                {
                    cancellation.Cancel();
                    return Task.FromCanceled<Result<int>>(token);
                }

                return Task.FromResult(Result<int>.Failure(failure));
            }, cancellation.Token)
            .EndOperationAsync(cancellation.Token);

        rowsSaved.ShouldBe(1);
        result.IsFailure.ShouldBeTrue();
        var error = result.Errors.ShouldHaveSingleItem();
        if (cancelAfterWrite)
        {
            error.ShouldBeOfType<OperationCancelledError>();
        }
        else
        {
            error.ShouldBeSameAs(failure);
        }

        context.Database.CurrentTransaction.ShouldBeNull();
        (await context.Set<TransactionRow>().AsNoTracking().AnyAsync()).ShouldBeFalse();
    }

    private sealed class TransactionContext(DbContextOptions<TransactionContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<TransactionRow>();
    }

    private sealed class TransactionRow
    {
        public int Id { get; set; }
    }
}
