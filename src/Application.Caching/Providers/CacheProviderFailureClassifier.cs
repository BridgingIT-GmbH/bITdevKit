// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Data.Common;

/// <summary>Separates provider infrastructure outages from source, caller, validity, capacity and payload failures.</summary>
/// <example><code>if (CacheProviderFailureClassifier.IsInfrastructureFailure(result)) { RecordProviderOutage(); }</code></example>
public static class CacheProviderFailureClassifier
{
    /// <summary>Classifies explicit provider-unavailable results without converting other cache failures into outages.</summary>
    /// <param name="result">The actual provider result.</param>
    /// <returns>True for an infrastructure-unavailable outcome.</returns>
    public static bool IsInfrastructureFailure(IResult result) => result.IsFailure && result.Errors.OfType<CacheError>()
        .Any(error => error.Code == CacheErrorCode.ProviderUnavailable);

    /// <summary>Classifies native transport, database and timeout exceptions while preserving caller cancellation.</summary>
    /// <param name="exception">The native exception.</param>
    /// <param name="cancellationToken">The original provider-attempt cancellation token.</param>
    /// <returns>True only for an infrastructure failure.</returns>
    public static bool IsInfrastructureFailure(Exception exception, CancellationToken cancellationToken = default) => exception switch
    {
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        CacheTransferException transfer => transfer.Code == CacheErrorCode.ProviderUnavailable,
        StreamSizeLimitExceededException => false,
        InvalidDataException or EndOfStreamException => false,
        IOException or DbException or TimeoutException => true,
        _ => false
    };
}