// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Bounds relational metadata serialization to the deployed 64 KiB format limit.</summary>
/// <example><code>using var stream = new CacheRelationalMetadataStream(); JsonSerializer.Serialize(stream, metadata);</code></example>
public sealed class CacheRelationalMetadataStream : MemoryStream
{
    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length > 65536 - this.Length) { throw new CacheRelationalException(CacheErrorCode.Capacity, "Cache metadata exceeds its fixed byte bound."); }

        base.Write(buffer);
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (count > 65536 - this.Length) { throw new CacheRelationalException(CacheErrorCode.Capacity, "Cache metadata exceeds its fixed byte bound."); }

        base.Write(buffer, offset, count);
    }
}