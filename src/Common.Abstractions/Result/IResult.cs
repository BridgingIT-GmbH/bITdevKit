// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Generic;

public interface IResult
{
    IReadOnlyList<string> Messages { get; }

    IReadOnlyList<IResultError> Errors { get; }

    bool IsSuccess { get; }

    bool IsFailure { get; }

    Result WithMessage(string message);

    Result WithError(IResultError error);

    Result WithError<TError>()
        where TError : IResultError, new();

    Result WithMessages(IEnumerable<string> messages);

    bool HasError();

    bool HasError<TError>()
        where TError : IResultError;

    bool HasError<TError>(out IEnumerable<IResultError> result)
        where TError : IResultError;
}

public interface IResult<out TValue> : IResult
{
    TValue Value { get; }
}