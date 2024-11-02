namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a paged result containing a collection of values with pagination details.
/// </summary>
public class PagedResult<TValue> : Result<IEnumerable<TValue>>
{
    /// <summary>
    /// Initializes a new instance of the PagedResult class.
    /// </summary>
    public PagedResult() { } // needs to be public for mapster

    /// <summary>
    /// Initializes a new instance of the PagedResult class with pagination details.
    /// </summary>
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
    /// Gets the current page number.
    /// </summary>
    public int CurrentPage { get; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages { get; }

    /// <summary>
    /// Gets the total count of items across all pages.
    /// </summary>
    public long TotalCount { get; }

    /// <summary>
    /// Gets the number of items per page.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets a value indicating whether there is a previous page available.
    /// </summary>
    public bool HasPreviousPage => this.CurrentPage > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page available.
    /// </summary>
    public bool HasNextPage => this.CurrentPage < this.TotalPages;

    /// <summary>
    /// Creates a successful paged result.
    /// </summary>
    public static PagedResult<TValue> Success(
        IEnumerable<TValue> values,
        long count = 0,
        int page = 1,
        int pageSize = 10)
    {
        return new PagedResult<TValue>(values, count, page, pageSize);
    }

    /// <summary>
    /// Creates a successful paged result with a message.
    /// </summary>
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
    /// Creates a successful paged result with multiple messages.
    /// </summary>
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
    /// Creates a PagedResult based on a success condition.
    /// </summary>
    public static PagedResult<TValue> SuccessIf(
        bool isSuccess,
        IEnumerable<TValue> values,
        long count = 0,
        int page = 1,
        int pageSize = 10,
        IResultError error = null)
    {
        return isSuccess
            ? Success(values, count, page, pageSize)
            : Failure().WithError(error);
    }

    /// <summary>
    /// Creates a PagedResult based on a predicate.
    /// </summary>
    public static PagedResult<TValue> SuccessIf(
        Func<IEnumerable<TValue>, bool> predicate,
        IEnumerable<TValue> values,
        long count = 0,
        int page = 1,
        int pageSize = 10,
        IResultError error = null)
    {
        try
        {
            if (predicate == null)
            {
                return Success(values, count, page, pageSize);
            }

            var isSuccess = predicate(values);
            return SuccessIf(isSuccess, values, count, page, pageSize, error);
        }
        catch (Exception ex)
        {
            return Failure().WithError(new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static new PagedResult<TValue> Failure()
    {
        return new PagedResult<TValue>(default) { success = false };
    }

    /// <summary>
    /// Creates a failure result with a specific error type.
    /// </summary>
    public static new PagedResult<TValue> Failure<TError>()
        where TError : IResultError, new()
    {
        return new PagedResult<TValue>(default) { success = false }
            .WithError<TError>();
    }

    /// <summary>
    /// Creates a failure result with a message and optional error.
    /// </summary>
    public static new PagedResult<TValue> Failure(string message, IResultError error = null)
    {
        return new PagedResult<TValue>(default) { success = false }
            .WithMessage(message).WithError(error);
    }

    /// <summary>
    /// Creates a failure result with messages and errors.
    /// </summary>
    public static new PagedResult<TValue> Failure(IEnumerable<string> messages, IEnumerable<IResultError> errors)
    {
        return new PagedResult<TValue>(default) { success = false }
            .WithMessages(messages).WithErrors(errors);
    }

    /// <summary>
    /// Creates a failure result with a specific error type and message.
    /// </summary>
    public static new PagedResult<TValue> Failure<TError>(string message)
        where TError : IResultError, new()
    {
        return new PagedResult<TValue>(default) { success = false }
            .WithMessage(message).WithError<TError>();
    }

    /// <summary>
    /// Creates a failure result with a specific error type and messages.
    /// </summary>
    public static new PagedResult<TValue> Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new()
    {
        return new PagedResult<TValue>(default) { success = false }
            .WithMessages(messages).WithError<TError>();
    }

    /// <summary>
    /// Creates a PagedResult based on a failure condition.
    /// </summary>
    public static PagedResult<TValue> FailureIf(
        bool isFailure,
        IEnumerable<TValue> values,
        long count = 0,
        int page = 1,
        int pageSize = 10,
        IResultError error = null)
    {
        return isFailure
            ? Failure().WithError(error)
            : Success(values, count, page, pageSize);
    }

    /// <summary>
    /// Creates a PagedResult based on a failure predicate.
    /// </summary>
    public static PagedResult<TValue> FailureIf(
        Func<IEnumerable<TValue>, bool> predicate,
        IEnumerable<TValue> values,
        long count = 0,
        int page = 1,
        int pageSize = 10,
        IResultError error = null)
    {
        try
        {
            if (predicate == null)
            {
                return Success(values, count, page, pageSize);
            }

            var isFailure = predicate(values);
            return FailureIf(isFailure, values, count, page, pageSize, error);
        }
        catch (Exception ex)
        {
            return Failure().WithError(new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Creates a PagedResult from an operation.
    /// </summary>
    public static PagedResult<TValue> For(
        Func<(IEnumerable<TValue> Values, long Count)> operation,
        int page = 1,
        int pageSize = 10)
    {
        try
        {
            if (operation == null)
            {
                return Success(Array.Empty<TValue>());
            }

            var (values, count) = operation();
            return Success(values, count, page, pageSize);
        }
        catch (Exception ex)
        {
            return Failure().WithError(new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Creates a PagedResult from an async operation.
    /// </summary>
    public static async Task<PagedResult<TValue>> ForAsync(
        Func<CancellationToken, Task<(IEnumerable<TValue> Values, long Count)>> operation,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (operation == null)
            {
                return Success(Array.Empty<TValue>());
            }

            var (values, count) = await operation(cancellationToken);
            return Success(values, count, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            return Failure().WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Failure().WithError(new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Converts to a regular Result.
    /// </summary>
    public new Result For()
    {
        return this.Match(
            _ => Result.Success().WithMessages(this.Messages).WithErrors(this.Errors),
            _ => Result.Failure().WithMessages(this.Messages).WithErrors(this.Errors));
    }

    /// <summary>
    /// Converts to a different PagedResult type.
    /// </summary>
    public new PagedResult<TOutput> For<TOutput>()
    {
        return this.Match(
            _ => PagedResult<TOutput>.Success(
                    Array.Empty<TOutput>(),
                    this.TotalCount,
                    this.CurrentPage,
                    this.PageSize)
                .WithMessages(this.Messages)
                .WithErrors(this.Errors),
            _ => PagedResult<TOutput>.Failure()
                .WithMessages(this.Messages)
                .WithErrors(this.Errors));
    }

    /// <summary>
    /// Converts to a different PagedResult type with provided values.
    /// </summary>
    public PagedResult<TOutput> For<TOutput>(IEnumerable<TOutput> values)
    {
        return this.Match(
            _ => PagedResult<TOutput>.Success(
                    values,
                    this.TotalCount,
                    this.CurrentPage,
                    this.PageSize)
                .WithMessages(this.Messages)
                .WithErrors(this.Errors),
            _ => PagedResult<TOutput>.Failure()
                .WithMessages(this.Messages)
                .WithErrors(this.Errors));
    }

    /// <summary>
    /// Adds a message to the result.
    /// </summary>
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
    /// Adds multiple messages to the result.
    /// </summary>
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
    /// Adds an error to the result.
    /// </summary>
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
    /// Adds a specific error type to the result.
    /// </summary>
    public new PagedResult<TValue> WithError<TError>()
        where TError : IResultError, new()
    {
        this.WithError(Activator.CreateInstance<TError>());
        this.success = false;
        return this;
    }

    /// <summary>
    /// Adds multiple errors to the result.
    /// </summary>
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

    /// <summary>
    /// Converts PagedResult to boolean.
    /// </summary>
    public static implicit operator bool(PagedResult<TValue> result) =>
        result?.Match(true, false) ?? false;

    /// <summary>
    /// Converts to base Result type.
    /// </summary>
    public static implicit operator Result(PagedResult<TValue> result) =>
        result?.For() ?? Result.Failure();

    // /// <summary>
    // /// Converts to Result{IEnumerable{TValue}}.
    // /// </summary>
    // public static implicit operator Result<IEnumerable<TValue>>(PagedResult<TValue> result) =>
    //     result?.Match(
    //         _ => Result<IEnumerable<TValue>>.Success(result.Value)
    //             .WithMessages(result.Messages)
    //             .WithErrors(result.Errors),
    //         _ => Result<IEnumerable<TValue>>.Failure()
    //             .WithMessages(result.Messages)
    //             .WithErrors(result.Errors))
    //     ?? Result<IEnumerable<TValue>>.Failure();
    //
}