// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using FluentValidation.Results;

/// <summary>
/// Represents command request base.
/// </summary>
/// <param name="id">The entity identifier.</param>
[Obsolete("Use the new Requester from now on")]
public abstract class CommandRequestBase(Guid id) : ICommandRequest<CommandResponse>
{
    /// <summary>
    /// Initializes a new instance of the <c>CommandRequestBase</c> class.
    /// </summary>
    protected CommandRequestBase()
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

/// <summary>
/// Represents command request base.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
/// <param name="id">The entity identifier.</param>
[Obsolete("Use the new Requester from now on")]
public abstract class CommandRequestBase<TResult>(Guid id) : ICommandRequest<CommandResponse<TResult>>
{
    /// <summary>
    /// Initializes a new instance of the <c>CommandRequestBase</c> class.
    /// </summary>
    protected CommandRequestBase()
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
