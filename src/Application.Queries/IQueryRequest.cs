// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using FluentValidation.Results;
using MediatR;

public interface IQueryRequest<out TResult> : IRequest<TResult>
{
    Guid RequestId { get; }

    DateTimeOffset RequestTimestamp { get; }

    ValidationResult Validate();
}