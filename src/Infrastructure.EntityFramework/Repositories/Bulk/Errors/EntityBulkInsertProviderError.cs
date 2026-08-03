// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Common;

/// <summary>
/// Reports a provider-selection, transaction, or native-write failure for an entity bulk insert.
/// </summary>
/// <example>
/// <code>
/// var error = new EntityBulkInsertProviderError(
///     "provider",
///     "Microsoft.EntityFrameworkCore.SqlServer",
///     "The native provider failed.",
///     exception);
/// </code>
/// </example>
public sealed class EntityBulkInsertProviderError : ResultErrorBase
{
    /// <summary>
    /// Initializes a provider-stage error.
    /// </summary>
    /// <param name="stage">The provider orchestration stage that failed.</param>
    /// <param name="providerName">The exact Entity Framework provider name.</param>
    /// <param name="message">The safe failure message.</param>
    /// <param name="exception">The underlying exception, when available.</param>
    /// <example>
    /// <code>
    /// var error = new EntityBulkInsertProviderError(
    ///     "transaction",
    ///     providerName,
    ///     exception.Message,
    ///     exception);
    /// </code>
    /// </example>
    public EntityBulkInsertProviderError(
        string stage,
        string providerName,
        string message,
        Exception exception = null
    )
        : base(message)
    {
        this.Stage = string.IsNullOrWhiteSpace(stage) ? "provider" : stage;
        this.ProviderName = string.IsNullOrWhiteSpace(providerName) ? "<unknown>" : providerName;
        this.Exception = exception;
    }

    /// <summary>Gets the provider orchestration stage.</summary>
    /// <example><code>var stage = error.Stage;</code></example>
    public string Stage { get; }

    /// <summary>Gets the exact Entity Framework provider name.</summary>
    /// <example><code>var providerName = error.ProviderName;</code></example>
    public string ProviderName { get; }

    /// <summary>Gets the underlying exception, when available.</summary>
    /// <example><code>var exception = error.Exception;</code></example>
    public Exception Exception { get; }
}
