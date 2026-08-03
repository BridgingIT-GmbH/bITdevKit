// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents a downloaded blob stream and its provider-neutral metadata.
/// </summary>
/// <example>
/// <code>
/// await using var download = result.Value;
/// await download.Content.CopyToAsync(targetStream, cancellationToken);
/// </code>
/// </example>
public sealed class BlobDownload : IAsyncDisposable
{
    /// <summary>
    /// Gets the readable content stream owned by this download result.
    /// </summary>
    /// <example>
    /// <code>
    /// await download.Content.CopyToAsync(target, cancellationToken);
    /// </code>
    /// </example>
    public Stream Content { get; init; }

    /// <summary>
    /// Gets the blob information returned with the content stream.
    /// </summary>
    /// <example>
    /// <code>
    /// var length = download.Info.Length;
    /// </code>
    /// </example>
    public BlobInfo Info { get; init; }

    /// <summary>
    /// Disposes the owned content stream.
    /// </summary>
    /// <returns>A task-like value representing asynchronous disposal.</returns>
    /// <example>
    /// <code>
    /// await download.DisposeAsync();
    /// </code>
    /// </example>
    public ValueTask DisposeAsync()
    {
        return this.Content.DisposeAsync();
    }
}
