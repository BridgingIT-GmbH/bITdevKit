// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides provider-neutral Blob Storage diagnostics snapshots.
/// </summary>
/// <example>
/// <code>
/// var snapshot = await diagnostics.GetSnapshotAsync();
/// </code>
/// </example>
public interface IBlobStorageDiagnosticsService
{
    /// <summary>
    /// Gets a snapshot containing registration, capability, and health information.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the snapshot.</param>
    /// <returns>A result containing the diagnostics snapshot.</returns>
    /// <example>
    /// <code>
    /// var result = await diagnostics.GetSnapshotAsync(cancellationToken);
    /// </code>
    /// </example>
    Task<Result<BlobStorageDiagnosticsSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
