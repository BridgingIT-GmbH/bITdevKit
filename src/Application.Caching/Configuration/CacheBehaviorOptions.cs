// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines optional ordered stage aliases. Explicit configuration lists replace each whole stage.</summary>
/// <example><code>var options = new CacheBehaviorOptions();</code></example>
public sealed record CacheBehaviorOptions
{
    /// <summary>Gets outermost-first public collection operation behaviors.</summary>
    public IReadOnlyList<string> CollectionOperations { get; init; } = [];

    /// <summary>Gets outermost-first owned source behaviors.</summary>
    public IReadOnlyList<string> SourceExecution { get; init; } = [];

    /// <summary>Gets outermost-first binary transformation behaviors.</summary>
    public IReadOnlyList<string> PayloadTransformation { get; init; } = [];
}