// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using Common;

public static class QueryResponse
{
    public static QueryResponse<Result<TValue>> For<TValue>(Result<TValue> result)
    {
        return new QueryResponse<Result<TValue>> { Result = result };
    }

    public static QueryResponse<Result<TResult>> For<TValue, TResult>(Result<TValue> result, IMapper mapper)
        where TResult : class
    {
        if (result?.IsFailure == true)
        {
            return new QueryResponse<Result<TResult>>
            {
                Result = Result<TResult>.Failure().WithMessages(result?.Messages).WithErrors(result?.Errors)
            };
        }

        return new QueryResponse<Result<TResult>>
        {
            Result = Result<TResult>.Success(result != null ? mapper.Map<TValue, TResult>(result.Value) : null)
                .WithMessages(result?.Messages)
                .WithErrors(result?.Errors)
        };
    }

    public static QueryResponse<Result<TValue>> For<TValue>(Result result)
    {
        if (result?.IsFailure == true)
        {
            return new QueryResponse<Result<TValue>>
            {
                Result = Result<TValue>.Failure().WithMessages(result?.Messages).WithErrors(result?.Errors)
            };
        }

        return new QueryResponse<Result<TValue>>
        {
            Result = Result<TValue>.Success().WithMessages(result?.Messages).WithErrors(result?.Errors)
        };
    }

    public static QueryResponse<Result<TValue>> Success<TValue>(TValue value)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Success(value) };
    }

    public static QueryResponse<Result<TValue>> Success<TValue>(TValue value, string message)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Success(value, message) };
    }

    public static QueryResponse<Result<TValue>> Success<TValue>(TValue value, IEnumerable<string> messages)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Success(value, messages) };
    }

    public static QueryResponse<Result<TValue>> Failure<TValue>(TValue value)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(value) };
    }

    public static QueryResponse<Result<TValue>> Failure<TValue>(
        TValue value,
        string message,
        IResultError error = default)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(value, message, error) };
    }

    public static QueryResponse<Result<TValue>> Failure<TValue>(
        TValue value,
        IEnumerable<string> messages,
        IEnumerable<IResultError> errors = null)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(value, messages, errors) };
    }

    public static QueryResponse<Result<TValue>> Failure<TValue>(string message)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(message) };
    }

    public static QueryResponse<Result<TValue>> Failure<TValue>(string message = null, IResultError error = default)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(message, error) };
    }

    public static QueryResponse<Result<TValue>> Failure<TValue>(
        IEnumerable<string> messages,
        IEnumerable<IResultError> errors = default)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(messages, errors) };
    }

    [Obsolete]
    public static QueryResponse<Result<TValue>> Fail<TValue, TError>(
        TValue value = default,
        string message = null,
        IResultError error = null)
        where TError : IResultError, new()
    {
        return new QueryResponse<Result<TValue>>
        {
            Result = Result<TValue>.Failure<TError>(value, message).WithError(error)
        };
    }

    public static QueryResponse<Result<TValue>> Failure<TValue, TError>(
        TValue value = default,
        string message = null,
        IResultError error = null)
        where TError : IResultError, new()
    {
        return new QueryResponse<Result<TValue>>
        {
            Result = Result<TValue>.Failure<TError>(value, message).WithError(error)
        };
    }
}