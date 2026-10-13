// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Declares a returned business result's observed Completed, Failed or Canceled outcome.</summary>
/// <example><code>var classification = new ProfilingResultClassification { Outcome = OperationProfilingOutcome.Failed };</code></example>
public sealed record ProfilingResultClassification
{
    /// <summary>Gets the reliably observed result outcome.</summary>
    /// <example><code>var outcome = classification.Outcome;</code></example>
    public OperationProfilingOutcome Outcome { get; init; } = OperationProfilingOutcome.Completed;

    /// <summary>Gets optional bounded failure information already safe for diagnostics.</summary>
    /// <example><code>var failure = classification.Failure;</code></example>
    public ProfilingFailureDescriptor Failure { get; init; }
}
