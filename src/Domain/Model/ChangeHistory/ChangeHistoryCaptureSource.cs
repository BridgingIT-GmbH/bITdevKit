// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Identifies the source that produced a change-history row.
/// </summary>
/// <example>
/// <code>
/// var source = ChangeHistoryCaptureSource.EntityChange;
/// </code>
/// </example>
public enum ChangeHistoryCaptureSource
{
    /// <summary>
    /// The row came from an <see cref="EntityChangeBuilder{TEntity}" /> change set.
    /// </summary>
    EntityChange,

    /// <summary>
    /// The row came from comparing an entity with a repository snapshot.
    /// </summary>
    RepositorySnapshot,

    /// <summary>
    /// The row came from EF Core change tracker original/current values.
    /// </summary>
    EfChangeTracker,

    /// <summary>
    /// The row came from initial create capture.
    /// </summary>
    Create,

    /// <summary>
    /// The row came from a set-based repository update.
    /// </summary>
    UpdateSet,

    /// <summary>
    /// The row came from an explicitly configured native entity bulk insert.
    /// </summary>
    NativeBulkInsert,

    /// <summary>
    /// The row came from a restore operation.
    /// </summary>
    Restore
}