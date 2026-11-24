// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Identifies process-local active-work gauges.</summary>
/// <example><code>var category = CacheActiveKind.Owners;</code></example>
public enum CacheActiveKind
{
    /// <summary>Admitted source owners.</summary>
    Owners,

    /// <summary>Callers currently observing shared work.</summary>
    Waiters,

    /// <summary>Protected payload transfers.</summary>
    Transfers,

    /// <summary>Admitted automatic refresh source units.</summary>
    Refresh
}