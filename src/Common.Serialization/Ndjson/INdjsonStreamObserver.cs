// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Receives one finalized output outcome per observed execution.</summary>
/// <example><code>result.WithObserver(observer);</code></example>
public interface INdjsonStreamObserver
{
    /// <summary>Reports immutable output status without changing transport behavior.</summary>
    /// <example><code>await observer.OnFinishedAsync(outcome, cancellationToken);</code></example>
    Task OnFinishedAsync(NdjsonStreamOutcome outcome, CancellationToken cancellationToken = default);
}
