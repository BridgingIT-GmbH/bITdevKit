// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Common;

/// <summary>
/// Reports a safe, provider-neutral precondition failure before an entity bulk insert is executed.
/// </summary>
/// <remarks>
/// The stage and message describe only orchestration state. They must not contain entity values,
/// connection details, or other sensitive data.
/// </remarks>
/// <example>
/// <code>
/// var error = new EntityBulkInsertPreconditionError(
///     "transaction",
///     "Ambient transactions are not supported for entity bulk insertion.");
/// </code>
/// </example>
public sealed class EntityBulkInsertPreconditionError : ResultErrorBase
{
    /// <summary>
    /// Initializes a precondition error with a stable stage and safe message.
    /// </summary>
    /// <param name="stage">The precondition stage that rejected the operation.</param>
    /// <param name="message">A safe message that does not contain operation payload data.</param>
    /// <example>
    /// <code>
    /// var error = new EntityBulkInsertPreconditionError(
    ///     "mapping",
    ///     "The entity type cannot be mapped to a single root table.");
    /// </code>
    /// </example>
    public EntityBulkInsertPreconditionError(string stage, string message)
        : base(string.IsNullOrWhiteSpace(message)
            ? "An entity bulk insert precondition was not met."
            : message)
    {
        this.Stage = string.IsNullOrWhiteSpace(stage) ? "precondition" : stage;
    }

    /// <summary>
    /// Gets the stable precondition stage that rejected the operation.
    /// </summary>
    /// <example>
    /// <code>
    /// var stage = error.Stage;
    /// </code>
    /// </example>
    public string Stage { get; }
}
