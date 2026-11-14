// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Owns one verified journal frame and the memory charge retained while its payload is consumed.</summary>
/// <example><code>await foreach (var frame in frames) { var sequence = frame.Sequence; /* Consume frame.Payload within this iteration. */ }</code></example>
public sealed class CacheJournalFrame(long sequence, byte[] payload, ResourceCharge memory) : IDisposable
{
    /// <summary>Gets the checked consecutive sequence number from the journal frame.</summary>
    public long Sequence { get; } = sequence;

    /// <summary>Gets the framed mutation bytes whose lifetime is bounded by this frame&apos;s memory lease.</summary>
    public ReadOnlyMemory<byte> Payload { get; } = payload;

    /// <inheritdoc />
    public void Dispose() => memory.Dispose();
}