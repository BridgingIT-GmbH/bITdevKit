// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Defines how ChangeHistory captures mutations that were not produced by <see cref="EntityChangeBuilder{TEntity}" />.
/// </summary>
/// <example>
/// <code>
/// options.Track&lt;Customer&gt;()
///     .UseCaptureStrategy(ChangeHistoryCaptureStrategy.RepositorySnapshot);
/// </code>
/// </example>
public enum ChangeHistoryCaptureStrategy
{
    /// <summary>
    /// Captures only pending <see cref="EntityChangeSet" /> records produced by <see cref="EntityChangeBuilder{TEntity}" />.
    /// </summary>
    EntityChangeOnly,

    /// <summary>
    /// Compares the submitted entity with a no-tracking repository snapshot loaded before update.
    /// </summary>
    RepositorySnapshot,

    /// <summary>
    /// Compares EF Core tracked original and current values.
    /// </summary>
    EfChangeTracker
}
