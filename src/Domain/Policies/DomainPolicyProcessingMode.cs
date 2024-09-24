// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Specifies the modes of processing domain policies when a policy failure occurs.
/// </summary>
public enum DomainPolicyProcessingMode
{
    /// <summary>
    ///     Continue processing domain policies even if a policy failure occurs.
    /// </summary>
    ContinueOnPolicyFailure = 0,

    /// <summary>
    ///     Cease processing any further domain policies upon encountering a failure.
    /// </summary>
    StopOnPolicyFailure = 1,

    /// <summary>
    ///     This processing mode indicates that an exception should be thrown if any domain policy fails.
    /// </summary>
    ThrowOnPolicyFailure = 2
}