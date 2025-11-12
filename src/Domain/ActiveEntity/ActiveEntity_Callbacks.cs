// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Threading.Tasks;

public abstract partial class ActiveEntity<TEntity, TId> : Entity<TId> // Lifecycle callbacks
    where TEntity : ActiveEntity<TEntity, TId>
{
    /// <summary>
    /// Callback before an entity is inserted.
    /// Return a Failure Result to halt the operation.
    /// </summary>
    protected virtual Task<Result> OnBeforeInsertAsync(IActiveEntityEntityProvider<TEntity, TId> provider, CancellationToken ct)
        => Task.FromResult(Result.Success());

    /// <summary>
    /// Callback after an entity is inserted.
    /// </summary>
    protected virtual Task<Result> OnAfterInsertAsync(IActiveEntityEntityProvider<TEntity, TId> provider, CancellationToken ct)
        => Task.FromResult(Result.Success());

    /// <summary>
    /// Callback before an entity is updated.
    /// Return a Failure Result to halt the operation.
    /// </summary>
    protected virtual Task<Result> OnBeforeUpdateAsync(IActiveEntityEntityProvider<TEntity, TId> provider, CancellationToken ct)
        => Task.FromResult(Result.Success());

    /// <summary>
    /// Callback after an entity is updated.
    /// </summary>
    protected virtual Task<Result> OnAfterUpdateAsync(IActiveEntityEntityProvider<TEntity, TId> provider, CancellationToken ct)
        => Task.FromResult(Result.Success());

    /// <summary>
    /// Callback before an entity is upserted (both insert and update).
    /// Return a Failure Result to halt the operation.
    /// </summary>
    protected virtual Task<Result> OnBeforeUpsertAsync(IActiveEntityEntityProvider<TEntity, TId> provider, CancellationToken ct)
        => Task.FromResult(Result.Success());

    /// <summary>
    /// Callback after an entity is upserted (both insert and update).
    /// </summary>
    protected virtual Task<Result> OnAfterUpsertAsync(IActiveEntityEntityProvider<TEntity, TId> provider, CancellationToken ct)
        => Task.FromResult(Result.Success());

    /// <summary>
    /// Callback before an entity is deleted.
    /// Return a Failure Result to halt the operation.
    /// </summary>
    protected virtual Task<Result> OnBeforeDeleteAsync(IActiveEntityEntityProvider<TEntity, TId> provider, CancellationToken ct)
        => Task.FromResult(Result.Success());

    /// <summary>
    /// Callback after an entity is deleted.
    /// </summary>
    protected virtual Task<Result> OnAfterDeleteAsync(IActiveEntityEntityProvider<TEntity, TId> provider, CancellationToken ct)
        => Task.FromResult(Result.Success());
}
