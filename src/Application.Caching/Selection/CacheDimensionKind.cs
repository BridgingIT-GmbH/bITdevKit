// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Lists the only supported metadata scalar kinds; payload CLR types are unaffected.</summary>
public enum CacheDimensionKind
{
    /// <summary>An ordinal-ignore-case string.</summary>
    String,

    /// <summary>A Boolean.</summary>
    Boolean,

    /// <summary>A signed integer normalized losslessly to Int64.</summary>
    Integer,

    /// <summary>A Guid identifier.</summary>
    Identifier,

    /// <summary>A UTC timestamp retaining .NET tick precision.</summary>
    Timestamp
}

/// <summary>Describes a scalar, comparer-deduplicated set, or half-open interval.</summary>
public enum CacheDimensionForm
{
    /// <summary>One typed scalar, optionally explicitly null.</summary>
    Scalar,

    /// <summary>A bounded set of typed scalars.</summary>
    Set,

    /// <summary>A nonempty Integer or Timestamp interval.</summary>
    Interval
}