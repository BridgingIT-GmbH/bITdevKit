// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.Data.SqlClient;

/// <summary>Classifies blob storage timeouts and transient SQL Server failures.</summary>
/// <example><code>var error = SqlServerBlobErrors.ProviderError(exception);</code></example>
public static class SqlServerBlobErrors
{
    /// <summary>Creates a blob provider error with the exception's message and retry classification.</summary>
    /// <param name="exception">The provider exception.</param>
    /// <example><code>return Result.Failure(SqlServerBlobErrors.ProviderError(exception));</code></example>
    public static BlobStoreProviderError ProviderError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new BlobStoreProviderError(exception.GetFullMessage(), IsTransient(exception));
    }

    /// <summary>Checks the exception and its inner exceptions for timeouts and transient SQL Server errors.</summary>
    /// <param name="exception">The provider exception.</param>
    /// <example><code>var retry = SqlServerBlobErrors.IsTransient(exception);</code></example>
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
