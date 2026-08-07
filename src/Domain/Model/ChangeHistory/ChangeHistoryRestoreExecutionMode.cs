// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Defines how a ChangeHistory restore path mutates an entity.
/// </summary>
/// <example>
/// <code>
/// options.Track&lt;Customer&gt;()
///     .AllowRestore(c =&gt; c.FirstName)
///     .UseValidatedSetter();
/// </code>
/// </example>
public enum ChangeHistoryRestoreExecutionMode
{
    /// <summary>
    /// Restore must call configured domain logic.
    /// </summary>
    DomainLogic,

    /// <summary>
    /// Restore delegates to a registered restore plan.
    /// </summary>
    RestorePlan,

    /// <summary>
    /// Restore uses an explicitly allowed public setter fallback.
    /// </summary>
    ValidatedSetter
}
