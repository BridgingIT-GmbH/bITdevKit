// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents a scoped operation that can be committed or rolled back.
///     This is the base interface for all operation scopes used with ResultOperationScope.
/// </summary>
/// <remarks>
///     Implement this interface for any operation requiring:
///     <list type="bullet">
///         <item>Lazy start semantics</item>
///         <item>Automatic cleanup (commit/rollback)</item>
///         <item>All-or-nothing behavior</item>
///         <item>Railway-oriented programming pattern</item>
///     </list>
/// </remarks>
/// <example>
/// <code>
/// public class CustomOperationScope : IOperationScope
/// {
///     public async Task CommitAsync(CancellationToken cancellationToken = default)
///     {
///         // Finalize the operation
///         await FinalizeChangesAsync(cancellationToken);
///     }
///
///     public async Task RollbackAsync(CancellationToken cancellationToken = default)
///     {
///         // Undo changes and cleanup
///         await UndoChangesAsync(cancellationToken);
///     }
/// }
///
/// // Usage
/// var result = await Result{Data}.Success(data)
///     .StartOperation(async ct => new CustomOperationScope())
///     .BindAsync(async (d, ct) => await ProcessAsync(d, ct))
///     .EndOperationAsync(cancellationToken);
/// </code>
/// </example>
public interface IOperationScope
{
    /// <summary>
    ///     Commits the operation, finalizing all changes.
    ///     Called when the Result chain completes successfully.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous commit operation.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Rolls back the operation, undoing all changes and cleaning up resources.
    ///     Called when the Result chain fails or an exception occurs.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous rollback operation.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
