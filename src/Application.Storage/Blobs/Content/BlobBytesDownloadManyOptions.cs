// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures a bulk byte-array blob download.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobBytesDownloadManyOptions
/// {
///     MaxDegreeOfParallelism = 32,
///     MaxConcurrentBatches = 4,
///     ContinueOnError = true
/// };
/// </code>
/// </example>
public sealed class BlobBytesDownloadManyOptions
{
    /// <summary>
    /// Gets the maximum number of keys processed at the same time. The default is 16.
    /// </summary>
    /// <example>
    /// <code>
    /// var parallelism = options.MaxDegreeOfParallelism;
    /// </code>
    /// </example>
    public int MaxDegreeOfParallelism { get; init; } = 16;

    /// <summary>
    /// Gets the maximum number of batched provider reads running at the same time when the provider
    /// implements <see cref="IBlobStoreBatchDownloadProvider" />. The default is 4. Ignored otherwise.
    /// </summary>
    /// <example>
    /// <code>
    /// var batches = options.MaxConcurrentBatches;
    /// </code>
    /// </example>
    public int MaxConcurrentBatches { get; init; } = 4;

    /// <summary>
    /// Gets a value indicating whether failed downloads are reported per item while the remaining
    /// downloads continue. When <c>false</c>, the first failure cancels the remaining downloads and fails the whole operation.
    /// </summary>
    /// <example>
    /// <code>
    /// var continueOnError = options.ContinueOnError;
    /// </code>
    /// </example>
    public bool ContinueOnError { get; init; } = true;
}
