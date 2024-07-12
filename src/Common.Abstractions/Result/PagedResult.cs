// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

public class PagedResult<TValue> : Result<IEnumerable<TValue>>
{
    public PagedResult()
    {
    }

    public PagedResult(IEnumerable<TValue> value = default, IEnumerable<string> messages = null, long count = 0, int page = 1, int pageSize = 10)
    {
        this.Value = value;
        this.CurrentPage = page;
        this.PageSize = pageSize;
        this.TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        this.TotalCount = count;
        this.WithMessages(messages);
    }

    public int CurrentPage { get; init; }

    public int TotalPages { get; init; }

    public long TotalCount { get; init; }

    public int PageSize { get; init; }

    public bool HasPreviousPage => this.CurrentPage > 1;

    public bool HasNextPage => this.CurrentPage < this.TotalPages;

    public static new PagedResult<TValue> Failure()
    {
        return new PagedResult<TValue>(default) { success = false };
    }

    public static new PagedResult<TValue> Failure<TError>()
        where TError : IResultError, new()
    {
        return new PagedResult<TValue>(default) { success = false }.WithError<TError>();
    }

    public static new PagedResult<TValue> Failure(string message, IResultError error = null)
    {
        return new PagedResult<TValue>(default) { success = false }
            .WithMessage(message).WithError(error);
    }

    public static new PagedResult<TValue> Failure(IEnumerable<string> messages, IEnumerable<IResultError> errors)
    {
        return new PagedResult<TValue>(default) { success = false }
            .WithMessages(messages).WithErrors(errors);
    }

    public static PagedResult<TValue> Success(IEnumerable<TValue> value, long count = 0, int page = 1, int pageSize = 10)
    {
        return new PagedResult<TValue>(value, null, count, page, pageSize);
    }

    public static PagedResult<TValue> Success(IEnumerable<TValue> value, string message, long count = 0, int page = 1, int pageSize = 10)
    {
        return new PagedResult<TValue>(value, null, count, page, pageSize).WithMessage(message);
    }

    public static PagedResult<TValue> Success(IEnumerable<TValue> value, IEnumerable<string> messages, long count = 0, int page = 1, int pageSize = 10)
    {
        return new PagedResult<TValue>(value, null, count, page, pageSize).WithMessages(messages);
    }

    public new PagedResult<TValue> WithMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            this.messages.Add(message);
        }

        return this;
    }

    public new PagedResult<TValue> WithMessages(IEnumerable<string> messages)
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

    public new PagedResult<TValue> WithError(IResultError error)
    {
        if (error is not null)
        {
            this.errors.Add(error);
            this.success = false;
        }

        return this;
    }

    public new PagedResult<TValue> WithError<TError>()
        where TError : IResultError, new()
    {
        this.WithError(Activator.CreateInstance<TError>());
        this.success = false;

        return this;
    }

    public new PagedResult<TValue> WithErrors(IEnumerable<IResultError> errors)
    {
        if (errors is not null)
        {
            foreach (var error in errors)
            {
                this.WithError(error);
            }
        }

        return this;
    }
}