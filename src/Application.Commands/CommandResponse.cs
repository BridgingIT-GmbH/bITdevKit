// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

public class CommandResponse
{
    public CommandResponse(string cancelledReason = null)
    {
        if (!string.IsNullOrEmpty(cancelledReason))
        {
            this.Cancelled = true;
            this.CancelledReason = cancelledReason;
        }
    }

    public bool Cancelled { get; private set; }

    public string CancelledReason { get; private set; }

    public static CommandResponse<Result> For(Result result = null)
    {
        if (result?.IsFailure == true)
        {
            return new CommandResponse<Result>
            {
                Result = Result.Failure().WithMessages(result?.Messages).WithErrors(result?.Errors)
            };
        }

        return new CommandResponse<Result>
        {
            Result = Result.Success().WithMessages(result?.Messages).WithErrors(result?.Errors)
        };
    }

    public static CommandResponse<Result> Success()
    {
        return new CommandResponse<Result> { Result = Result.Success() };
    }

    public static CommandResponse<Result> Success(string message)
    {
        return new CommandResponse<Result> { Result = Result.Success(message) };
    }

    public static CommandResponse<Result> Success(IEnumerable<string> messages)
    {
        return new CommandResponse<Result> { Result = Result.Success(messages) };
    }

    public static CommandResponse<Result> Failure()
    {
        return new CommandResponse<Result> { Result = Result.Failure() };
    }

    public static CommandResponse<Result> Failure(string message, IResultError error = default)
    {
        return new CommandResponse<Result> { Result = Result.Failure(message, error) };
    }

    public static CommandResponse<Result> Failure(
        IEnumerable<string> messages,
        IEnumerable<IResultError> errors = default)
    {
        return new CommandResponse<Result> { Result = Result.Failure(messages, errors) };
    }

    public static CommandResponse<Result> Failure<TError>(string message = null)
        where TError : IResultError, new()
    {
        return new CommandResponse<Result> { Result = Result.Failure<TError>(message) };
    }

    public static CommandResponse<Result> Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new()
    {
        return new CommandResponse<Result> { Result = Result.Failure<TError>(messages) };
    }

    public static CommandResponse<Result<TValue>> For<TValue>(Result<TValue> result)
    {
        return new CommandResponse<Result<TValue>> { Result = result };
    }

    public static CommandResponse<Result<TResult>> For<TValue, TResult>(Result<TValue> result)
    {
        if (result?.IsFailure == true)
        {
            return new CommandResponse<Result<TResult>>
            {
                Result = Result<TResult>.Failure().WithMessages(result?.Messages).WithErrors(result?.Errors)
            };
        }

        return new CommandResponse<Result<TResult>>
        {
            Result = Result<TResult>.Success().WithMessages(result?.Messages).WithErrors(result?.Errors)
        };
    }

    public static CommandResponse<Result<TValue>> For<TValue>(Result result)
    {
        if (result?.IsFailure == true)
        {
            return new CommandResponse<Result<TValue>>
            {
                Result = Result<TValue>.Failure().WithMessages(result?.Messages).WithErrors(result?.Errors)
            };
        }

        return new CommandResponse<Result<TValue>>
        {
            Result = Result<TValue>.Success().WithMessages(result?.Messages).WithErrors(result?.Errors)
        };
    }

    public static CommandResponse<Result<TValue>> Success<TValue>(TValue value)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Success(value) };
    }

    public static CommandResponse<Result<TValue>> Success<TValue>(TValue value, string message)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Success(value, message) };
    }

    public static CommandResponse<Result<TValue>> Success<TValue>(TValue value, IEnumerable<string> messages)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Success(value, messages) };
    }

    public static CommandResponse<Result<TValue>> Failure<TValue>(TValue value)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Failure(value) };
    }

    public static CommandResponse<Result<TValue>> Failure<TValue>(
        TValue value,
        string message,
        IResultError error = default)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Failure(value, message, error) };
    }

    public static CommandResponse<Result<TValue>> Failure<TValue>(
        TValue value,
        IEnumerable<string> messages,
        IEnumerable<IResultError> errors = default)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Failure(value, messages, errors) };
    }

    public static CommandResponse<Result<TValue>> Failure<TValue>(string message = null, IResultError error = default)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Failure(message, error) };
    }

    //public static CommandResponse<Result<TValue>> Failure<TValue>(TValue value = default, string message = null, IResultError error = default)
    //{
    //    return new CommandResponse<Result<TValue>>()
    //    {
    //        Result = Result<TValue>.Failure(value, message).WithError(error)
    //    };
    //}

    public static CommandResponse<Result<TValue>> Failure<TValue, TError>(TValue value = default, string message = null)
        where TError : IResultError, new()
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Failure<TError>(value, message) };
    }

    public void SetCancelled(string cancelledReason)
    {
        if (!string.IsNullOrEmpty(cancelledReason))
        {
            this.Cancelled = true;
            this.CancelledReason = cancelledReason;
        }
    }
}

public class CommandResponse<TResult>(string cancelledReason = null) : CommandResponse(cancelledReason)
{
    public TResult Result { get; set; }

    public static new CommandResponse<Result<TResult>> For(Result result)
    {
        if (result?.IsFailure == true)
        {
            return new CommandResponse<Result<TResult>>
            {
                Result = Result<TResult>.Failure().WithMessages(result?.Messages).WithErrors(result?.Errors)
            };
        }

        return new CommandResponse<Result<TResult>>
        {
            Result = Result<TResult>.Success().WithMessages(result?.Messages).WithErrors(result?.Errors)
        };
    }
}