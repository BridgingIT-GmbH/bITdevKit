// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines binary units and finite format-one safety bounds shared by providers.</summary>
/// <example><code>var capacity = ByteSize.Megabytes(128);</code></example>
public static class CacheDefaults
{
    /// <summary>One kibibyte.</summary>
    public const long KiB = 1024;

    /// <summary>One mebibyte.</summary>
    public const long MiB = 1024 * KiB;

    /// <summary>One gibibyte.</summary>
    public const long GiB = 1024 * MiB;

    /// <summary>Maximum persisted chunk size.</summary>
    public const int MaximumChunkBytes = 1024 * 1024;

    /// <summary>Initial native metadata/envelope format version.</summary>
    public const int FormatVersion = 1;
}