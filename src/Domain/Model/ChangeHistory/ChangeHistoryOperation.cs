// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Defines the logical operation represented by a change-history row.
/// </summary>
/// <example>
/// <code>
/// var operation = ChangeHistoryOperation.Update;
/// </code>
/// </example>
public enum ChangeHistoryOperation
{
    /// <summary>
    /// The row records an entity update.
    /// </summary>
    Update,

    /// <summary>
    /// The row records initial entity creation values.
    /// </summary>
    Create,

    /// <summary>
    /// The row records a restore operation.
    /// </summary>
    Restore,

    /// <summary>
    /// The row records a set-based update.
    /// </summary>
    BulkUpdate,

    /// <summary>
    /// The row records a native entity bulk insert.
    /// </summary>
    BulkInsert,

    /// <summary>
    /// The row records a collection mutation.
    /// </summary>
    CollectionChanged,

    /// <summary>
    /// The row records a graph mutation.
    /// </summary>
    GraphChanged
}