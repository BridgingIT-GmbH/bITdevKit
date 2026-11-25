// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using System.Reflection;
using Microsoft.Data.SqlClient;

/// <summary>Creates SQL Server exceptions for deterministic storage error tests.</summary>
/// <example><code>var exception = SqlServerTestException.Create(1205);</code></example>
public static class SqlServerTestException
{
    /// <summary>Creates a SQL Server exception with the given error numbers.</summary>
    /// <example><code>var exception = SqlServerTestException.Create(547);</code></example>
    public static SqlException Create(params int[] numbers)
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
