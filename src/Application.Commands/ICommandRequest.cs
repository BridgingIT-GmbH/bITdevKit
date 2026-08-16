// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using FluentValidation.Results;

/// <summary>
/// Defines operations for i command request.
/// </summary>
public interface ICommandRequest : MediatR.IRequest
{
    /// <summary>
    /// Gets the request id.
    /// </summary>
    Guid RequestId { get; }

    /// <summary>
    /// Gets the request timestamp.
    /// </summary>
    DateTimeOffset RequestTimestamp { get; }

    /// <summary>
    /// Validates .
    /// </summary>
    /// <returns>The result of the operation.</returns>
    ValidationResult Validate();
}

/// <summary>
/// Defines operations for i command request.
/// </summary>
public interface ICommandRequest<out TResult> : MediatR.IRequest<TResult>
{
    /// <summary>
    /// Gets the request id.
    /// </summary>
    Guid RequestId { get; }

    /// <summary>
    /// Gets the request timestamp.
    /// </summary>
    DateTimeOffset RequestTimestamp { get; }

    /// <summary>
    /// Validates .
    /// </summary>
    /// <returns>The result of the operation.</returns>
    ValidationResult Validate();
}
