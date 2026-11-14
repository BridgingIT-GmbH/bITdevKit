// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Represents one explicitly enabled bounded diagnostic key-operation event, without retaining the raw key.</summary>
/// <param name="At">The actual process observation timestamp.</param>
/// <param name="Operation">The controlled public operation category.</param>
/// <param name="KeyFingerprint">A keyed SHA-256 fingerprint comparable only within this collection's current process lifetime.</param>
/// <remarks>Fingerprint values never become metric labels or trace tags. Restarting the host changes the secret and fingerprints.</remarks>
/// <example><code>foreach (var observation in diagnostics.ForCollection("cards").GetKeyHistory(32)) { Inspect(observation.Operation); }</code></example>
public sealed record CacheKeyObservation(DateTimeOffset At, CacheOperationKind Operation, string KeyFingerprint);