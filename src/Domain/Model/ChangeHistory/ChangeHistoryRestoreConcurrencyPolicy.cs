// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Defines concurrency requirements for restore operations.
/// </summary>
/// <example>
/// <code>
/// options.Track&lt;Customer&gt;().UseRestoreConcurrencyPolicy(ChangeHistoryRestoreConcurrencyPolicy.RequireExpectedVersion);
/// </code>
/// </example>
public enum ChangeHistoryRestoreConcurrencyPolicy
{
    /// <summary>
    /// Do not require or validate an expected concurrency version.
    /// </summary>
    None,

    /// <summary>
    /// Validate an expected version only when one is supplied.
    /// </summary>
    ExpectedVersion,

    /// <summary>
    /// Require and validate an expected version for concurrency-enabled entities.
    /// </summary>
    RequireExpectedVersion
}
