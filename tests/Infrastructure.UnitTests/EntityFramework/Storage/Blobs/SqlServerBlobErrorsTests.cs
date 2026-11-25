// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using System.Reflection;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

[UnitTest("Infrastructure")]
public sealed class SqlServerBlobErrorsTests
{
    [Theory]
    [InlineData(1205, true)]
    [InlineData(-2, true)]
    [InlineData(1222, true)]
    [InlineData(233, true)]
    [InlineData(64, true)]
    [InlineData(121, true)]
    [InlineData(10053, true)]
    [InlineData(10054, true)]
    [InlineData(10060, true)]
    [InlineData(10928, true)]
    [InlineData(10929, true)]
    [InlineData(40197, true)]
    [InlineData(40501, true)]
    [InlineData(40613, true)]
    [InlineData(49918, true)]
    [InlineData(49919, true)]
    [InlineData(49920, true)]
    [InlineData(102, false)]
    [InlineData(208, false)]
    [InlineData(547, false)]
    public void ProviderError_WithSqlErrorNumber_ClassifiesFailure(int number, bool transient)
    {
        var exception = CreateSqlException(number);

        SqlServerBlobErrors.IsTransient(exception).ShouldBe(transient);
        SqlServerBlobErrors.ProviderError(exception).IsTransient.ShouldBe(transient);
    }

    [Fact]
    public void IsTransient_WithWrappedDeadlockOrTimeout_ChecksAllInnerExceptions()
    {
        SqlServerBlobErrors.IsTransient(new DbUpdateException("Save failed.", CreateSqlException(1205))).ShouldBeTrue();
        SqlServerBlobErrors.IsTransient(new InvalidOperationException("Wrapped.", new TimeoutException())).ShouldBeTrue();
        SqlServerBlobErrors.IsTransient(new InvalidOperationException("Permanent.")).ShouldBeFalse();
        SqlServerBlobErrors.IsTransient(CreateSqlException(208, 1205)).ShouldBeTrue();
    }

    private static SqlException CreateSqlException(params int[] numbers)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true);
        var add = typeof(SqlErrorCollection).GetMethod("Add", instance);
        var createError = typeof(SqlError).GetConstructor(instance,
            [typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int), typeof(Exception)]);
        foreach (var number in numbers)
        {
            var error = createError.Invoke([number, (byte)0, (byte)16, "test", "SQL error", "test", 1, null]);
            add.Invoke(errors, [error]);
        }

        var createException = typeof(SqlException).GetConstructor(instance,
            [typeof(string), typeof(SqlErrorCollection), typeof(Exception), typeof(Guid)]);
        return (SqlException)createException.Invoke(["SQL operation failed", errors, null, Guid.Empty]);
    }
}
