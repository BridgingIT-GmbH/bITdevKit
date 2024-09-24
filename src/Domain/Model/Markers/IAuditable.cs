// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
///     Represents an entity that can be audited.
/// </summary>
public interface IAuditable
{
    /// <summary>
    ///     Represents the state of an audit for an entity, including creation, update, deactivation, and deletion details.
    ///     Provides properties to track who made changes, when changes were made, and descriptions/reasons for changes.
    ///     Exposes methods to determine the current state of the entity (created, updated, deactivated, or deleted).
    /// </summary>
    AuditState AuditState { get; set; }
}