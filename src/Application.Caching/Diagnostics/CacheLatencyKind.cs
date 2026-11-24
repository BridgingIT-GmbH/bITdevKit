// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Identifies supported elapsed-time sample families in seconds.</summary>
/// <example><code>var category = CacheLatencyKind.Operation;</code></example>
public enum CacheLatencyKind
{
    /// <summary>A public operation duration.</summary>
    Operation,

    /// <summary>An actual owned source attempt duration.</summary>
    Source,

    /// <summary>An admission queue duration.</summary>
    Queue,

    /// <summary>A follower observation duration.</summary>
    Wait,

    /// <summary>A protected transfer duration.</summary>
    Transfer,

    /// <summary>One bounded native provider-call duration.</summary>
    Provider
}