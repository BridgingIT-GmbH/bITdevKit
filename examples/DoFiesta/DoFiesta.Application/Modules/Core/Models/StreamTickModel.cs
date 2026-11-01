// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

/// <summary>Fictional live-stream data without broker or replay guarantees.</summary>
/// <param name="Sequence">Ascending response-local sequence number.</param>
/// <param name="Timestamp">UTC timestamp supplied by the clock.</param>
/// <example><code>var tick = new StreamTickModel(1, TimeProvider.System.GetUtcNow());</code></example>
public sealed record StreamTickModel(long Sequence, DateTimeOffset Timestamp);
