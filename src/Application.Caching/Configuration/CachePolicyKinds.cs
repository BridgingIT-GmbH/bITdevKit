// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Controls pressure admission.</summary>
public enum CacheEvictionMode
{
    /// <summary>Allow bounded invalid-first/priority/recency eviction.</summary>
    Evict,

    /// <summary>Reject new storage when capacity is exhausted.</summary>
    Reject
}

/// <summary>Identifies supported explicit compression algorithms.</summary>
public enum CacheCompressionAlgorithm
{
    /// <summary>BCL GZip representation.</summary>
    GZip,

    /// <summary>BCL Brotli representation.</summary>
    Brotli
}

/// <summary>Identifies eligible local hot representations.</summary>
public enum CacheHotEntryRepresentation
{
    /// <summary>Retain an eligible immutable complete binary representation.</summary>
    Binary,

    /// <summary>Retain an approved immutable typed object with known conservative size.</summary>
    Decoded,

    /// <summary>Retain both under one combined charge.</summary>
    BinaryAndDecoded
}

/// <summary>Identifies supported authoritative hot reuse policies.</summary>
public enum CacheHotEntryValidation
{
    /// <summary>Validate provider authority on every reuse.</summary>
    Strict
}