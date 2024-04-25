// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Generic;

public class Result : IResult
{
    protected readonly List<string> messages = new();
    protected readonly List<IResultError> errors = new();
    protected bool success = true;

    protected Result()
    {
    }

    public IReadOnlyList<string> Messages { get => this.messages; }

    public IReadOnlyList<IResultError> Errors { get => this.errors; }

    public bool IsSuccess { get => this.success; init => this.success = value; }

    public bool IsFailure { get => !this.success; init => this.success = !value; }

    public static Result Failure()
    {
        return new Result { IsSuccess = false };
    }

    public static Result Failure<TError>()
        where TError : IResultError, new()
    {
        return new Result() { IsSuccess = false }.WithError<TError>();
    }

    public static Result Failure(string message)
    {
        return new Result() { IsSuccess = false }.WithMessage(message);
    }

    public static Result Failure<TError>(string message)
        where TError : IResultError, new()
    {
        return new Result() { IsSuccess = false }.WithMessage(message).WithError<TError>();
    }

    public static Result Failure(IEnumerable<string> messages)
    {
        return new Result() { IsSuccess = false }.WithMessages(messages);
    }

    public static Result Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new()
    {
        return new Result() { IsSuccess = false }.WithMessages(messages).WithError<TError>();
    }

    public static Result Success()
    {
        return new Result();
    }

    public static Result Success(string message)
    {
        return new Result().WithMessage(message);
    }

    public static Result Success(IEnumerable<string> messages)
    {
        return new Result().WithMessages(messages);
    }

    public Result WithMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            this.messages.Add(message);
        }

        return this;
    }

    public Result WithMessages(IEnumerable<string> messages)
    {
        if (messages is not null)
        {
            foreach (var message in messages)
            {
                this.WithMessage(message);
            }
        }

        return this;
    }

    public Result WithError(IResultError error)
    {
        if (error is not null)
        {
            this.errors.Add(error);
            this.success = false;
        }

        return this;
    }

    public Result WithError<TError>()
        where TError : IResultError, new()
    {
        this.WithError(Activator.CreateInstance<TError>());
        this.success = false;

        return this;
    }

    public bool HasError<TError>()
        where TError : IResultError
    {
        var errorType = typeof(TError);

        return this.errors.Find(e => e.GetType() == errorType) is not null;
    }

    public bool HasError()
    {
        return this.Errors.Any();
    }

    public bool HasError<TError>(out IEnumerable<IResultError> result)
        where TError : IResultError
    {
        var errorType = typeof(TError);
        result = this.errors.Where(e => e.GetType() == errorType);

        return result?.Any() == true;
    }
}

public class Result<TValue> : Result, IResult<TValue>
{
    public TValue Value { get; set; }

    public static new Result<TValue> Failure()
    {
        return new Result<TValue> { IsSuccess = false };
    }

    public static new Result<TValue> Failure<TError>()
        where TError : IResultError, new()
    {
        return new Result<TValue> { IsSuccess = false }.WithError<TError>();
    }

    public static Result<TValue> Failure(TValue value)
    {
        return new Result<TValue> { IsSuccess = false, Value = value };
    }

    public static new Result<TValue> Failure(string message)
    {
        return new Result<TValue> { IsSuccess = false }.WithMessage(message);
    }

    public static Result<TValue> Failure(TValue value, string message)
    {
        return new Result<TValue> { IsSuccess = false, Value = value }.WithMessage(message);
    }

    public static Result<TValue> Failure(TValue value, IEnumerable<string> messages)
    {
        return new Result<TValue> { IsSuccess = false, Value = value }.WithMessages(messages);
    }

    public static new Result<TValue> Failure<TError>(string message)
        where TError : IResultError, new()
    {
        return new Result<TValue> { IsSuccess = false }.WithMessage(message).WithError<TError>();
    }

    public static Result<TValue> Failure<TError>(TValue value, string message)
        where TError : IResultError, new()
    {
        return new Result<TValue> { IsSuccess = false, Value = value }.WithMessage(message).WithError<TError>();
    }

    public static new Result<TValue> Failure(IEnumerable<string> messages)
    {
        return new Result<TValue> { IsSuccess = false }.WithMessages(messages);
    }

    public static new Result Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new()
    {
        return new Result<TValue> { IsSuccess = false }.WithMessages(messages).WithError<TError>();
    }

    public static new Result<TValue> Success()
    {
        return new Result<TValue>();
    }

    public static new Result<TValue> Success(string message)
    {
        return new Result<TValue>().WithMessage(message);
    }

    public static Result<TValue> Success(TValue value)
    {
        return new Result<TValue> { Value = value };
    }

    public static Result<TValue> Success(TValue value, string message = null)
    {
        return new Result<TValue> { Value = value }.WithMessage(message);
    }

    public static Result<TValue> Success(TValue value, IEnumerable<string> messages)
    {
        return new Result<TValue> { Value = value }.WithMessages(messages);
    }

    public new Result<TValue> WithMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            this.messages.Add(message);
        }

        return this;
    }

    public new Result<TValue> WithMessages(IEnumerable<string> messages)
    {
        if (messages is not null)
        {
            foreach (var message in messages)
            {
                this.WithMessage(message);
            }
        }

        return this;
    }

    public new Result<TValue> WithError(IResultError error)
    {
        if (error is not null)
        {
            this.errors.Add(error);
            this.success = false;
        }

        return this;
    }

    public new Result<TValue> WithError<TError>()
        where TError : IResultError, new()
    {
        this.WithError(Activator.CreateInstance<TError>());
        this.success = false;

        return this;
    }
}