// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;

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

    public static CommandResponse<Result> For(Result result)
    {
        return new CommandResponse<Result>()
        {
            Result = result,
        };
    }

    public static CommandResponse<Result> Success()
    {
        return new CommandResponse<Result>()
        {
            Result = Result.Success()
        };
    }

    public static CommandResponse<Result> Success(string message)
    {
        return new CommandResponse<Result>()
        {
            Result = Result.Success(message)
        };
    }

    public static CommandResponse<Result> Success(IEnumerable<string> messages)
    {
        return new CommandResponse<Result>()
        {
            Result = Result.Success(messages)
        };
    }

    public static CommandResponse<Result> Fail()
    {
        return new CommandResponse<Result>()
        {
            Result = Result.Failure()
        };
    }

    public static CommandResponse<Result> Fail(string message)
    {
        return new CommandResponse<Result>()
        {
            Result = Result.Failure(message)
        };
    }

    public static CommandResponse<Result> Fail(IEnumerable<string> messages)
    {
        return new CommandResponse<Result>()
        {
            Result = Result.Failure(messages)
        };
    }

    public static CommandResponse<Result> Fail(IResultError error = default, string message = null)
    {
        return new CommandResponse<Result>()
        {
            Result = Result.Failure(message).WithError(error)
        };
    }

    public static CommandResponse<Result> Fail<TError>(string message = null)
        where TError : IResultError, new()
    {
        return new CommandResponse<Result>()
        {
            Result = Result.Failure<TError>(message)
        };
    }

    public static CommandResponse<Result<TValue>> Create<TValue>(Result<TValue> result)
    {
        return new CommandResponse<Result<TValue>>()
        {
            Result = result,
        };
    }

    public static CommandResponse<Result<TValue>> Success<TValue>(TValue value)
    {
        return new CommandResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Success(value)
        };
    }

    public static CommandResponse<Result<TValue>> Success<TValue>(TValue value, string message)
    {
        return new CommandResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Success(value, message)
        };
    }

    public static CommandResponse<Result<TValue>> Success<TValue>(TValue value, IEnumerable<string> messages)
    {
        return new CommandResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Success(value, messages)
        };
    }

    public static CommandResponse<Result<TValue>> Fail<TValue>(TValue value)
    {
        return new CommandResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure(value)
        };
    }

    public static CommandResponse<Result<TValue>> Fail<TValue>(TValue value, string message)
    {
        return new CommandResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure(value, message)
        };
    }

    public static CommandResponse<Result<TValue>> Fail<TValue>(TValue value, IEnumerable<string> messages)
    {
        return new CommandResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure(value, messages)
        };
    }

    public static CommandResponse<Result<TValue>> Fail<TValue>(IResultError error = default, string message = null)
    {
        return new CommandResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure(message).WithError(error)
        };
    }

    public static CommandResponse<Result<TValue>> Fail<TValue>(TValue value = default, IResultError error = default, string message = null)
    {
        return new CommandResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure(value, message).WithError(error)
        };
    }

    public static CommandResponse<Result<TValue>> Fail<TValue, TError>(TValue value = default, string message = null)
        where TError : IResultError, new()
    {
        return new CommandResponse<Result<TValue>>()
        {
            Result = Result<TValue>.Failure<TError>(value, message)
        };
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
}