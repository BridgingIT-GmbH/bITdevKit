// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using FluentValidation.Results;

/// <summary>
/// Represents query request base.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
/// <param name="id">The entity identifier.</param>
[Obsolete("Use the new Requester from now on")]
public abstract class QueryRequestBase<TResult>(Guid id) : IQueryRequest<QueryResponse<TResult>>, IQueryHandler
{
    /// <summary>
    /// Initializes a new instance of the <c>QueryRequestBase</c> class.
    /// </summary>
    protected QueryRequestBase()
        : this(GuidGenerator.CreateSequential()) { }

    /// <summary>
    /// Gets the request id.
    /// </summary>
    public Guid RequestId { get; } = id;

    /// <summary>
    /// Gets the request timestamp.
    /// </summary>
    public DateTimeOffset RequestTimestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Validates .
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public virtual ValidationResult Validate()
    {
        return new ValidationResult();
    }
}
