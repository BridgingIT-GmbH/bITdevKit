// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;

public static class QueryResponse
{
    public static QueryResponse<Result<TValue>> For<TValue>(Result<TValue> result)
    {
        return new QueryResponse<Result<TValue>>()
        {
            Result = result,
        };
    }

    public static QueryResponse<Result<TValue>> Success<TValue>(TValue value)
    {
        return new QueryResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Success(value)
        };
    }

    public static QueryResponse<Result<TValue>> Success<TValue>(TValue value, string message)
    {
        return new QueryResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Success(value, message)
        };
    }

    public static QueryResponse<Result<TValue>> Success<TValue>(TValue value, IEnumerable<string> messages)
    {
        return new QueryResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Success(value, messages)
        };
    }

    public static QueryResponse<Result<TValue>> Fail<TValue>(TValue value)
    {
        return new QueryResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure(value)
        };
    }

    public static QueryResponse<Result<TValue>> Fail<TValue>(TValue value, string message)
    {
        return new QueryResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure(value, message)
        };
    }

    public static QueryResponse<Result<TValue>> Fail<TValue>(TValue value, IEnumerable<string> messages)
    {
        return new QueryResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure(value, messages)
        };
    }

    public static QueryResponse<Result<TValue>> Fail<TValue>(string message)
    {
        return new QueryResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure(message)
        };
    }

    public static QueryResponse<Result<TValue>> Fail<TValue>(IResultError error = default, string message = null)
    {
        return new QueryResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure(message).WithError(error)
        };
    }

    public static QueryResponse<Result<TValue>> Fail<TValue>(TValue value = default, IResultError error = default, string message = null)
    {
        return new QueryResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure(value, message).WithError(error)
        };
    }

    public static QueryResponse<Result<TValue>> Fail<TValue, TError>(TValue value = default, string message = null)
        where TError : IResultError, new()
    {
        return new QueryResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure<TError>(value, message)
        };
    }
}