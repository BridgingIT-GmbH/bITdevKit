// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using FluentValidation.Results;

public interface ICommandRequest : IRequest
{
    Guid RequestId { get; }

    DateTimeOffset RequestTimestamp { get; }

    ValidationResult Validate();
}

public interface ICommandRequest<out TResult> : IRequest<TResult>
{
    Guid RequestId { get; }

    DateTimeOffset RequestTimestamp { get; }

    ValidationResult Validate();
}