// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Defines whether a configured capture source is required or best-effort.
/// </summary>
/// <example>
/// <code>
/// options.Track&lt;Customer&gt;()
///     .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required);
/// </code>
/// </example>
public enum ChangeHistoryCaptureMode
{
    /// <summary>
    /// Capture source failures are logged and skipped.
    /// </summary>
    BestEffort,

    /// <summary>
    /// Capture source failures abort the repository operation before the entity update is saved.
    /// </summary>
    Required,

    /// <summary>
    /// The capture source is disabled.
    /// </summary>
    Disabled
}
