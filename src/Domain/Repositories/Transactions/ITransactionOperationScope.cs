// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Represents a repository transaction scope with explicit commit/rollback control.
///     Implements IOperationScope for use with ResultOperationScope.
/// </summary>
public interface ITransactionOperationScope : IOperationScope
{
    // Inherits CommitAsync and RollbackAsync from IOperationScope
}