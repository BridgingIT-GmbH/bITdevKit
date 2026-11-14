// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Describes an immutable payload&apos;s generated version, logical length and full SHA-256 digest independently of block framing.</summary>
/// <example><code>reference.Validate(storeId); var physicalBytes = reference.PhysicalBytes;</code></example>
public sealed record CachePayloadReference
{
    /// <summary>The maximum of 65,536 logical payload bytes per independently checked block.</summary>
    public const int BlockBytes = 65536;

    /// <summary>The 48 framing bytes stored with each payload block.</summary>
    public const int FrameBytes = 48;

    /// <summary>Gets the immutable payload descriptor format, currently one.</summary>
    public int Format { get; init; } = 1;

    /// <summary>Gets the generated root identity owning this payload.</summary>
    public string StoreId { get; init; }

    /// <summary>Gets the generated immutable payload version used in physical file names.</summary>
    public string Version { get; init; }

    /// <summary>Gets the logical payload byte length excluding block framing.</summary>
    public long Length { get; init; }

    /// <summary>Gets the hexadecimal full logical-payload SHA-256 digest.</summary>
    public string Hash { get; init; }

    /// <summary>Gets the number of framed blocks needed for the logical payload length.</summary>
    public long Blocks => this.Length / BlockBytes + (this.Length % BlockBytes == 0 ? 0 : 1);

    /// <summary>Gets the logical length plus framing bytes for every stored block.</summary>
    public long PhysicalBytes => checked(this.Length + this.Blocks * FrameBytes);

    /// <summary>Rejects another store&apos;s descriptor, invalid generated version, negative or excessive length and malformed hash.</summary>
    public void Validate(string storeId)
    {
        if (this.Format != 1 || this.StoreId != storeId || !Guid.TryParseExact(this.StoreId, "N", out _) ||
            !Guid.TryParseExact(this.Version, "N", out _) || this.Length < 0 || this.Length > long.MaxValue / 2 ||
            this.Hash?.Length != 64 || !this.Hash.All(char.IsAsciiHexDigit))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <summary>Builds a generated-version payload or staging path and rejects filesystem links along that path.</summary>
    public static string PathFor(string directory, string version, bool staging = false)
    {
        if (!Guid.TryParseExact(version, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        var path = Path.Combine(directory, version + (staging ? ".payload.tmp" : ".payload"));
        FileSystemPaths.RejectLinks(path);
        return path;
    }
}