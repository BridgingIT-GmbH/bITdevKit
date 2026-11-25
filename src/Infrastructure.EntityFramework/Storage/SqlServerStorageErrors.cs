// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Microsoft.Data.SqlClient;

/// <summary>Classifies timeouts and transient SQL Server failures for storage providers.</summary>
/// <example><code>var transient = SqlServerStorageErrors.IsTransient(exception);</code></example>
public static class SqlServerStorageErrors
{
    /// <summary>Checks the exception chain for timeouts or transient SQL Server errors, including deadlocks.</summary>
    /// <example><code>var retry = SqlServerStorageErrors.IsTransient(updateException);</code></example>
    public static bool IsTransient(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException ||
                current is SqlException sql && (sql.IsTransient || sql.Errors.Cast<SqlError>().Any(error =>
                    error.Number is 1205 or -2 or 1222 or 233 or 64 or 121 or 10053 or 10054 or 10060
                        or 10928 or 10929 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920)))
            {
                return true;
            }
        }

        return false;
    }
}
