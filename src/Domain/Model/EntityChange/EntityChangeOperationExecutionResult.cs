// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using BridgingIT.DevKit.Common;
using System.Collections.Generic;

/// <summary>
/// Represents the result of executing an ordered operation in a change transaction.
/// </summary>
internal class EntityChangeOperationExecutionResult
{
    /// <summary>
    /// Gets a value indicating whether the operation executed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation signaled cancellation of remaining operations.
    /// </summary>
    public bool IsCancelled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !this.IsSuccess && !this.IsCancelled;

    /// <summary>
    /// Gets a value indicating whether the operation resulted in changes to the entity.
    /// </summary>
    public bool HasChanged { get; init; }

    /// <summary>
    /// Gets the errors that occurred during operation execution.
    /// </summary>
    public IEnumerable<IResultError> Errors { get; init; } = [];

    /// <summary>
    /// Gets the messages associated with the operation execution.
    /// </summary>
    public IEnumerable<string> Messages { get; init; } = [];

    /// <summary>
    /// Creates a successful operation result.
    /// </summary>
    public static EntityChangeOperationExecutionResult Success(bool hasChanged = false) =>
        new() { IsSuccess = true, HasChanged = hasChanged };

    /// <summary>
    /// Creates a cancelled operation result (circuit breaker triggered).
    /// </summary>
    public static EntityChangeOperationExecutionResult Cancelled() =>
        new() { IsCancelled = true };

    /// <summary>
    /// Creates a failed operation result with errors and messages.
    /// </summary>
    public static EntityChangeOperationExecutionResult Failure(IEnumerable<IResultError> errors = null, IEnumerable<string> messages = null) =>
        new() { Errors = errors ?? [], Messages = messages ?? [] };
}
