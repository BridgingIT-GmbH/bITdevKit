// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using System;
using FluentValidation.Results;
using MediatR;

public interface ICommandRequest : IRequest
{
    string Id { get; }

    DateTimeOffset Timestamp { get; }

    ValidationResult Validate();
}

public interface ICommandRequest<out TResult> : IRequest<TResult>
{
    string Id { get; }

    DateTimeOffset Timestamp { get; }

    ValidationResult Validate();
}