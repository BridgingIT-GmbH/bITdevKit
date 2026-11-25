// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using System.Data.Common;
using Microsoft.Data.SqlClient;

/// <summary>Classifies temporary database failures in relational storage providers.</summary>
/// <example><code>var transient = RelationalStorageErrors.IsTransient(exception);</code></example>
public static class RelationalStorageErrors
{
    /// <summary>Checks for timeouts and known transient errors from the supported engines.</summary>
    /// <example><code>var transient = RelationalStorageErrors.IsTransient(updateException);</code></example>
    public static bool IsTransient(Exception exception) =>
        SqlServerStorageErrors.IsTransient(exception) || WasWriteRejected(exception);

    /// <summary>Checks for a lock or serialization failure that rejected the write.</summary>
    /// <example><code>var retryable = RelationalStorageErrors.WasWriteRejected(updateException);</code></example>
    public static bool WasWriteRejected(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && sql.Errors.Cast<SqlError>().Any(error => error.Number is 1205 or 1222))
            {
                return true;
            }

            if (current is DbException { SqlState: "55P03" or "40P01" or "40001" })
            {
                return true;
            }

            // The SQLite driver is optional for the shared EF package.
            var type = current.GetType();
            if (type.FullName == "Microsoft.Data.Sqlite.SqliteException" &&
                type.GetProperty("SqliteErrorCode")?.GetValue(current) is 5 or 6)
            {
                return true;
            }
        }

        return false;
    }
}
