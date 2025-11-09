// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using System;
using System.Data;

/// <summary>
/// Specifies that handlers within the attributed class should execute within a database transaction using the provided
/// DbContext type. Allows configuration of transaction isolation level and automatic rollback behavior on failure.
/// </summary>
/// <remarks>Apply this attribute to a handler class to ensure its operations are executed within a transactional
/// scope using the specified DbContext type. This can help maintain data integrity and consistency, especially in
/// workflows where multiple operations must succeed or fail as a unit. The attribute does not support inheritance or
/// multiple applications on the same class.</remarks>
/// <typeparam name="TDBcontext">The type of DbContext to use for managing the transaction. Must derive from <see cref="DbContext"/>.</typeparam>
/// <param name="isolationLevel">The isolation level to apply to the database transaction. Determines how the transaction interacts with other
/// concurrent operations. Defaults to <see cref="IsolationLevel.ReadCommitted"/>.</param>
/// <param name="rollbackOnFailure">Indicates whether the transaction should be automatically rolled back if an operation fails. When <see
/// langword="true"/>, changes made during the transaction are reverted on failure. Defaults to <see langword="true"/>.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerDatabaseTransactionAttribute<TDBcontext>(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, bool rollbackOnFailure = true) : Attribute
    where TDBcontext : DbContext
{
    /// <summary>
    /// Gets the isolation level for the database transaction.
    /// </summary>
    public IsolationLevel IsolationLevel { get; } = isolationLevel;

    /// <summary>
    /// Gets a value indicating whether operations are automatically rolled back if a failure occurs.
    /// </summary>
    /// <remarks>When <see langword="true"/>, any failed operation will trigger a rollback to revert changes
    /// made during the transaction. This property is typically used to ensure data consistency in transactional
    /// workflows.</remarks>
    public bool RollbackOnFailure { get; } = rollbackOnFailure;

    /// <summary>
    /// Gets the type of the DbContext to use for the transaction.
    /// </summary>
    public Type DbContextType { get; } = typeof(TDBcontext);
}