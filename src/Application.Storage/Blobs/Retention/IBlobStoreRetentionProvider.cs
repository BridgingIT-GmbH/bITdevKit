// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines provider-side expired blob sweeping without adding maintenance methods to <see cref="IBlobStoreClient" />.
/// </summary>
/// <example>
/// <code>
/// var result = await provider.SweepExpiredAsync(request, cancellationToken);
/// </code>
/// </example>
public interface IBlobStoreRetentionProvider
{
    /// <summary>
    /// Deletes expired blobs through the provider's native indexed or conditional retention path.
    /// </summary>
    /// <param name="request">The retention sweep request.</param>
    /// <param name="cancellationToken">The token used to cancel the sweep.</param>
    /// <returns>A result describing the provider-side sweep outcome.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.SweepExpiredAsync(request, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<BlobRetentionSweepResult>> SweepExpiredAsync(
        BlobRetentionSweepRequest request,
        CancellationToken cancellationToken = default);
}
