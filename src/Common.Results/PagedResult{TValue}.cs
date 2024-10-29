// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents a paged result containing a collection of values with pagination details.
/// </summary>
/// <typeparam name="TValue">The type of the values included in the paged result.</typeparam>
public class PagedResult<TValue> : Result<IEnumerable<TValue>>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PagedResult{TValue}" /> class.
    ///     Represents a paginated result with additional metadata for paging.
    /// </summary>
    /// <typeparam name="TValue">The type of the values contained in the paginated result.</typeparam>
    public PagedResult() { } // needs to be public for mapster

    /// <summary>
    ///     Initializes a new instance of the <see cref="PagedResult{TValue}" /> class.
    ///     Represents a paged result, extending the standard Result class with pagination capabilities.
    /// </summary>
    /// <typeparam name="TValue">The type of the elements in the paged result.</typeparam>
    private PagedResult(
        IEnumerable<TValue> values = default,
        long count = 0,
        int page = 1,
        int pageSize = 10)
    {
        this.Value = values;
        this.TotalCount = count;
        this.CurrentPage = page;
        this.PageSize = pageSize;
        this.TotalPages = (int)Math.Ceiling(count / (double)pageSize);
    }

    /// <summary>
    ///     Gets the current page number in a paginated result set.
    /// </summary>
    public int CurrentPage { get; }

    /// <summary>
    ///     Gets the total number of pages available in the paged result.
    ///     This property is calculated based on the total count of items divided by the page size.
    /// </summary>
    public int TotalPages { get; }

    /// <summary>
    ///     Gets the total count of items available before paging is applied.
    /// </summary>
    public long TotalCount { get; }

    /// <summary>
    ///     Gets the number of items to be displayed in a single page of the paginated result.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    ///     Gets a value indicating whether there is a previous page of results available.
    ///     Returns <c>true</c> if the current page number is greater than 1; otherwise, <c>false</c>.
    /// </summary>
    public bool HasPreviousPage => this.CurrentPage > 1;

    /// <summary>
    ///     Gets a value indicating whether there is a next page of results available.
    /// </summary>
    public bool HasNextPage => this.CurrentPage < this.TotalPages;

    /// <summary>
    ///     Creates a new instance of <see cref="PagedResult{TValue}" /> with a failure status.
    /// </summary>
    /// <returns>A <see cref="PagedResult{TValue}" /> with its success property set to false.</returns>
    public static new PagedResult<TValue> Failure()
    {
        return new PagedResult<TValue>(default) { success = false };
    }

    /// <summary>
    ///     Creates a new instance of PagedResult with a failure state.
    /// </summary>
    /// <returns>A new PagedResult instance marked as a failure.</returns>
    public static new PagedResult<TValue> Failure<TError>()
        where TError : IResultError, new()
    {
        return new PagedResult<TValue>(default) { success = false }
            .WithError<TError>();
    }

    /// <summary>
    ///     Creates a failed result with a specified message and optional error.
    /// </summary>
    /// <param name="message">The message associated with the failure.</param>
    /// <param name="error">The error related to the failure, if any.</param>
    /// <returns>A new <see cref="PagedResult{TValue}" /> representing the failure.</returns>
    public static new PagedResult<TValue> Failure(string message, IResultError error = null)
    {
        return new PagedResult<TValue>(default) { success = false }
            .WithMessage(message).WithError(error);
    }

    /// <summary>
    ///     Creates a PagedResult object representing a failure state.
    /// </summary>
    /// <param name="messages">A collection of error messages associated with the failure.</param>
    /// <param name="errors">A collection of errors associated with the failure.</param>
    /// <returns>A new instance of PagedResult with the provided error messages and errors.</returns>
    public static new PagedResult<TValue> Failure(IEnumerable<string> messages, IEnumerable<IResultError> errors)
    {
        return new PagedResult<TValue>(default) { success = false }
            .WithMessages(messages).WithErrors(errors);
    }

    /// <summary>
    ///     Creates a successful PagedResult instance containing a collection of values.
    /// </summary>
    /// <typeparam name="TValue">The type of elements in the collection.</typeparam>
    /// <param name="values">The collection of values.</param>
    /// <param name="count"></param>
    /// <param name="page">The current page number. Default is 1.</param>
    /// <param name="pageSize">The number of items per page. Default is 10.</param>
    /// <returns>A new instance of <see cref="PagedResult{TValue}" /> containing the provided values.</returns>
    public static PagedResult<TValue> Success(
        IEnumerable<TValue> values,
        long count = 0,
        int page = 1,
        int pageSize = 10)
    {
        return new PagedResult<TValue>(values, count, page, pageSize);
    }

    /// <summary>
    ///     Creates a successful PagedResult with a single message.
    /// </summary>
    /// <typeparam name="TValue">The type of the items in the paged result.</typeparam>
    /// <param name="values">The items for the paged result.</param>
    /// <param name="message">A message associated with the success.</param>
    /// <param name="count"></param>
    /// <param name="page">The current page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>A successful PagedResult containing the provided items and message.</returns>
    public static PagedResult<TValue> Success(
        IEnumerable<TValue> values,
        string message,
        long count = 0,
        int page = 1,
        int pageSize = 10)
    {
        return new PagedResult<TValue>(values, count, page, pageSize).WithMessage(message);
    }

    /// <summary>
    ///     Creates a successful <see cref="PagedResult{TValue}" /> with the specified values and messages.
    /// </summary>
    /// <param name="values">The enumerable of values to be included in the result.</param>
    /// <param name="messages">The collection of messages to be included in the result.</param>
    /// <param name="count"></param>
    /// <param name="page">The current page number.</param>
    /// <param name="pageSize">The size of each page.</param>
    /// <returns>A <see cref="PagedResult{TValue}" /> containing the provided values and messages.</returns>
    public static PagedResult<TValue> Success(
        IEnumerable<TValue> values,
        IEnumerable<string> messages,
        long count = 0,
        int page = 1,
        int pageSize = 10)
    {
        return new PagedResult<TValue>(values, count, page, pageSize).WithMessages(messages);
    }

    /// <summary>
    ///     Adds a message to the result.
    /// </summary>
    /// <param name="message">The message to add. If the message is null or whitespace, it will not be added.</param>
    /// <returns>The current PagedResult instance with the added message.</returns>
    public new PagedResult<TValue> WithMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return this;
        }

        this.messages.Add(message);

        return this;
    }

    /// <summary>
    ///     Adds multiple messages to the result.
    /// </summary>
    /// <param name="messages">The collection of messages to add.</param>
    /// <returns>The updated PagedResult instance with the added messages.</returns>
    public new PagedResult<TValue> WithMessages(IEnumerable<string> messages)
    {
        if (messages is null)
        {
            return this;
        }

        foreach (var message in messages)
        {
            this.WithMessage(message);
        }

        return this;
    }

    /// <summary>
    ///     Adds an error to the PagedResult and marks the result as unsuccessful.
    /// </summary>
    /// <param name="error">The error to add. If null, the method will have no effect.</param>
    /// <returns>The updated PagedResult with the added error.</returns>
    public new PagedResult<TValue> WithError(IResultError error)
    {
        if (error is null)
        {
            return this;
        }

        this.errors.Add(error);
        this.success = false;

        return this;
    }

    /// <summary>
    ///     Adds a specific error to the current result and sets the success flag to false.
    /// </summary>
    /// <typeparam name="TError">The type of the error to be added. Must implement IResultError.</typeparam>
    /// <returns>Returns the current instance of <see cref="PagedResult{TValue}" /> with the specified error added.</returns>
    public new PagedResult<TValue> WithError<TError>()
        where TError : IResultError, new()
    {
        this.WithError(Activator.CreateInstance<TError>());
        this.success = false;

        return this;
    }

    /// <summary>
    ///     Adds multiple errors to the result and sets the success flag to false.
    /// </summary>
    /// <param name="errors">The collection of errors to add.</param>
    /// <returns>The current instance of PagedResult with added errors.</returns>
    public new PagedResult<TValue> WithErrors(IEnumerable<IResultError> errors)
    {
        if (errors is null)
        {
            return this;
        }

        foreach (var error in errors)
        {
            this.WithError(error);
        }

        return this;
    }
}