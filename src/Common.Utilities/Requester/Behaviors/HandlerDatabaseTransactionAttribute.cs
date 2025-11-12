// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

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
public class HandlerDatabaseTransactionAttribute<TDBcontext>(DatabaseTransactionIsolationLevel isolationLevel = DatabaseTransactionIsolationLevel.ReadCommitted, bool rollbackOnFailure = true) : Attribute
//where TDBcontext : DbContext
{
    /// <summary>
    /// Gets the isolation level for the database transaction.
    /// </summary>
    public DatabaseTransactionIsolationLevel IsolationLevel { get; } = isolationLevel;

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

/// <summary>
/// Declares that a handler should execute within a database transaction using
/// the DbContext identified by <see cref="ContextName"/>. This attribute is
/// infrastructure-agnostic and can be used from the application layer.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HandlerDatabaseTransactionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the attribute.
    /// </summary>
    /// <param name="isolationLevel">Desired transaction isolation level.</param>
    /// <param name="rollbackOnFailure">Whether to rollback on exception.</param>
    /// <param name="contextName">
    /// Logical DbContext name; may omit the "DbContext" suffix (e.g., "Core" or "CoreDbContext").
    /// </param>
    public HandlerDatabaseTransactionAttribute(
        DatabaseTransactionIsolationLevel isolationLevel = DatabaseTransactionIsolationLevel.ReadCommitted,
        bool rollbackOnFailure = true,
        string contextName = null)
    {
        this.IsolationLevel = isolationLevel;
        this.RollbackOnFailure = rollbackOnFailure;
        this.ContextName = contextName;
    }

    public DatabaseTransactionIsolationLevel IsolationLevel { get; }

    public bool RollbackOnFailure { get; }

    public string ContextName { get; }
}

/// <summary>
/// Specifies the transaction isolation level used for database operations.
/// Matches values commonly used by ADO.NET providers.
/// </summary>
public enum DatabaseTransactionIsolationLevel
{
    /// <summary>
    /// A different isolation level than the one specified is being used,
    /// but the level cannot be determined.
    /// </summary>
    Unspecified = -1,

    /// <summary>
    /// The pending changes from more highly isolated transactions cannot be overwritten.
    /// </summary>
    Chaos = 16,

    /// <summary>
    /// A dirty read is possible, meaning that no shared locks are issued and
    /// no exclusive locks are honored.
    /// </summary>
    ReadUncommitted = 256,

    /// <summary>
    /// Shared locks are held while the data is being read to avoid dirty reads,
    /// but the data can be changed before the end of the transaction, resulting
    /// in non-repeatable reads or phantom data.
    /// </summary>
    ReadCommitted = 4096,

    /// <summary>
    /// Locks are placed on all data that is used in a query, preventing other users
    /// from updating the data. Prevents non-repeatable reads but phantom rows are still possible.
    /// </summary>
    RepeatableRead = 65536,

    /// <summary>
    /// A range lock is placed on the dataset, preventing other users from updating
    /// or inserting rows into the dataset until the transaction is complete.
    /// </summary>
    Serializable = 1048576,

    /// <summary>
    /// Reduces blocking by storing a version of data that one application can read
    /// while another is modifying the same data. Indicates that from one transaction
    /// you cannot see changes made in other transactions, even if you requery.
    /// </summary>
    Snapshot = 16777216
}