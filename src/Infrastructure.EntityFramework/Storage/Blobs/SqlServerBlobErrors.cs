// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Application.Storage;
using BridgingIT.DevKit.Common;

/// <summary>Classifies blob storage timeouts and transient relational failures.</summary>
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

    /// <summary>Checks the exception and its inner exceptions for timeouts and transient database errors.</summary>
    /// <param name="exception">The provider exception.</param>
    /// <example><code>var retry = SqlServerBlobErrors.IsTransient(exception);</code></example>
    public static bool IsTransient(Exception exception) => RelationalStorageErrors.IsTransient(exception);
}
