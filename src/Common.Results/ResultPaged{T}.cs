// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a paged result containing a collection of values with pagination details.
/// Implements value semantics and immutable behavior for thread-safety.
/// </summary>
/// <example>
/// // Creating a successful paged result
/// var items = new[] { 1, 2, 3, 4, 5 };
/// var result = ResultPaged{int}.Success(items, count: 100, page: 1, pageSize: 5);
///
/// // Checking pagination info
/// Console.WriteLine($"Page {result.CurrentPage} of {result.TotalPages}");
/// Console.WriteLine($"Total items: {result.TotalCount}");
///
/// // Handling results
/// if (result.IsSuccess)
/// {
///     foreach(var item in result.Value)
///     {
///         Console.WriteLine(item);
///     }
/// }
/// </example>
[DebuggerDisplay("{IsSuccess ? \"✓\" : \"✗\"} {messages.Count}Msg {errors.Count}Err, Page {CurrentPage}/{TotalPages} {FirstMessageOrError}")]
public readonly struct ResultPaged<T> : IResultPaged<T>
{
    private const int DefaultPageSize = 10;

    private readonly bool success;
    private readonly ValueList<string> messages;
    private readonly ValueList<IResultError> errors;
    private readonly IEnumerable<T> value; // Null for outcomes that carry only state and diagnostics.
    private readonly long totalCount;
    private readonly int currentPage;
    private readonly int pageSize;

    /// <summary>
    /// Initializes a new instance of the ResultPaged class with pagination details.
    /// </summary>
    private ResultPaged(
        IEnumerable<T> value,
        bool success,
        long count = 0,
        int page = 1,
        int pageSize = DefaultPageSize,
        ValueList<string> messages = default,
        ValueList<IResultError> errors = default)
    {
        this.success = success;
        this.value = value;
        this.totalCount = Math.Max(0, count);
        this.currentPage = page < 1 ? 1 : page;
        this.pageSize = pageSize < 1 ? DefaultPageSize : pageSize;
        this.messages = messages;
        this.errors = errors;
    }

    private string FirstMessageOrError =>
        !this.messages.IsEmpty ? $" | {this.messages.AsEnumerable().First()}" :
        !this.errors.IsEmpty ? $" | {this.errors.AsEnumerable().First().GetType().Name}" :
        string.Empty;

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int CurrentPage => this.currentPage == 0 ? 1 : this.currentPage;

    /// <summary>
    /// Gets the total number of pages based on total count and page size, capped at <see cref="int.MaxValue"/>.
    /// </summary>
    public int TotalPages => this.totalCount == 0 ? 0 : (int)Math.Min(int.MaxValue, 1 + ((this.totalCount - 1) / this.PageSize));

    /// <summary>
    /// Gets the total count of items across all pages.
    /// </summary>
    public long TotalCount => this.totalCount;

    /// <summary>
    /// Gets the number of items per page.
    /// </summary>
    public int PageSize => this.pageSize == 0 ? DefaultPageSize : this.pageSize;

    /// <summary>
    /// Gets a value indicating whether there is a previous page available.
    /// </summary>
    public bool HasPreviousPage => this.CurrentPage > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page available.
    /// </summary>
    public bool HasNextPage => this.CurrentPage < this.TotalPages;

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = Result{IEnumerable{User}}.Success(user);
    /// if (result.IsSuccess)
    /// {
    ///     Console.WriteLine($"User {result.Value.Name} retrieved successfully");
    /// }
    /// </code>
    /// </example>
    public bool IsSuccess => this.success;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = Result{IEnumerable{User}}.Failure()
    ///     .WithError(new ValidationError("Invalid data"));
    /// if (result.IsFailure)
    /// {
    ///     Console.WriteLine("Operation failed");
    /// }
    /// </code>
    /// </example>
    public bool IsFailure => !this.success;

    /// <summary>
    /// Gets the collection of values for the current page.
    /// </summary>
    public IEnumerable<T> Value => this.value ?? [];

    internal bool HasPage => this.value is not null;

    /// <summary>
    /// Gets the collection of messages associated with the result.
    /// </summary>
    public IReadOnlyList<string> Messages =>
        this.messages.AsEnumerable().ToList().AsReadOnly();

    /// <summary>
    /// Gets the collection of errors associated with the result.
    /// </summary>
    public IReadOnlyList<IResultError> Errors =>
        this.errors.AsEnumerable().ToList().AsReadOnly();

    /// <summary>
    /// Creates a Result from an operation, handling any exceptions that occur.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Bind(() => {
    ///     var users = _repository.GetPagedUsers(page, pageSize);
    ///     return (users, totalCount: _repository.GetTotalCount());
    /// });
    /// </example>
    public static ResultPaged<T> Bind(
        Func<(IEnumerable<T> Values, long TotalCount)> operation)
    {
        if (operation is null)
        {
            return Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            return FromBoundPage(operation());
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Creates a Result from an async operation, handling any exceptions that occur.
    /// </summary>
    /// <example>
    /// var result = await ResultPaged{User}.BindAsync(
    ///     async ct => {
    ///         var users = await _repository.GetPagedUsersAsync(page, pageSize, ct);
    ///         var count = await _repository.GetTotalCountAsync(ct);
    ///         return (users, count);
    ///     },
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<T>> BindAsync(
        Func<CancellationToken, Task<(IEnumerable<T> Values, long TotalCount)>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            return FromBoundPage(await operation(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessage("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    private static ResultPaged<T> FromBoundPage((IEnumerable<T> Values, long TotalCount) page)
    {
        var values = page.Values.ToArray();

        return Success(values, count: page.TotalCount, pageSize: values.Length);
    }

    internal static ResultPaged<T> FromOutcome(
        bool isSuccess, ValueList<string> messages, ValueList<IResultError> errors) =>
        new(null, isSuccess, messages: messages, errors: errors);

    /// <summary>
    /// Creates a successful paged result with the specified values and pagination details.
    /// </summary>
    /// <example>
    /// var items = await dbContext.Users.Skip(0).Take(10).ToListAsync();
    /// var totalCount = await dbContext.Users.LongCountAsync();
    /// var result = ResultPaged{User}.Success(
    ///     values: items,
    ///     count: totalCount,
    ///     page: 1,
    ///     pageSize: 10
    /// );
    /// </example>
    public static ResultPaged<T> Success(IEnumerable<T> values, long count = 0, int page = 1, int pageSize = DefaultPageSize) =>
        new(values ?? [], true, count, page, pageSize);

    /// <summary>
    /// Creates a successful paged result with a message.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Success(
    ///     values: users,
    ///     message: "Successfully retrieved users",
    ///     count: totalUsers,
    ///     page: currentPage,
    ///     pageSize: 20
    /// );
    /// </example>
    public static ResultPaged<T> Success(
        IEnumerable<T> values,
        string message,
        long count = 0,
        int page = 1,
        int pageSize = DefaultPageSize) => Success(values, count, page, pageSize).WithMessage(message);

    /// <summary>
    /// Creates a successful paged result with multiple messages.
    /// </summary>
    /// <example>
    /// var messages = new[] {
    ///     "Users retrieved",
    ///     "Applied active status filter"
    /// };
    /// var result = ResultPaged{User}.Success(
    ///     values: activeUsers,
    ///     messages: messages,
    ///     count: totalActive,
    ///     page: 1,
    ///     pageSize: 10
    /// );
    /// </example>
    public static ResultPaged<T> Success(
        IEnumerable<T> values,
        IEnumerable<string> messages,
        long count = 0,
        int page = 1,
        int pageSize = DefaultPageSize) => Success(values, count, page, pageSize).WithMessages(messages);

    /// <summary>
    /// Creates a failed paged result.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Failure();
    /// if (result.IsFailure)
    /// {
    ///     Console.WriteLine("Failed to retrieve users");
    /// }
    /// </example>
    public static ResultPaged<T> Failure() => new(null, false);

    /// <summary>
    /// Creates a failed ResultPaged{T} with the specified values.
    /// </summary>
    /// <param name="values">The values to be contained in the failed result.</param>
    /// <returns>A new failed Result{T} containing the specified value.</returns>
    /// <example>
    /// <code>
    /// var invalidUser = new User(); // Invalid user state
    /// var result = PagesResult{User}.Failure([invalidUser])
    ///     .WithError(new ValidationError("Invalid user data"));
    /// </code>
    /// </example>
    public static ResultPaged<T> Failure(IEnumerable<T> values) => new(values, false);

    /// <summary>
    /// Creates a failed paged result with a specific error type.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Failure{DatabaseError}();
    /// if (result.HasError{DatabaseError}())
    /// {
    ///     Console.WriteLine("Database error occurred");
    /// }
    /// </example>
    public static ResultPaged<T> Failure<TError>()
        where TError : IResultError, new() => Failure().WithError<TError>();

    /// <summary>
    /// Creates a failed paged result with a specific error instance.
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    public static ResultPaged<T> Failure(IResultError error) => Failure().WithError(error);

    /// <summary>
    /// Creates a failed paged result with a message and optional error.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Failure(
    ///     message: "Failed to retrieve users",
    ///     error: new DatabaseError("Connection timeout")
    /// );
    /// </example>
    public static ResultPaged<T> Failure(string message, IResultError error = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        return Failure().WithMessage(message).WithError(error);
    }

    /// <summary>
    /// Creates a failed paged result with messages and errors.
    /// </summary>
    /// <example>
    /// var messages = new[] { "Failed to connect", "Retry attempted" };
    /// var errors = new IResultError[] {
    ///     new ConnectionError(),
    ///     new TimeoutError()
    /// };
    /// var result = ResultPaged{User}.Failure(messages, errors);
    /// </example>
    public static ResultPaged<T> Failure(
        IEnumerable<string> messages,
        IEnumerable<IResultError> errors = null) => Failure()
            .WithMessages(messages)
            .WithErrors(errors);

    /// <summary>
    /// Creates a failed paged result with a specific error type and message.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Failure{ValidationError}(
    ///     "Invalid page parameters"
    /// );
    /// </example>
    public static ResultPaged<T> Failure<TError>(string message)
        where TError : IResultError, new()
    {
        ArgumentNullException.ThrowIfNull(message);

        return Failure()
            .WithMessage(message)
            .WithError<TError>();
    }

    /// <summary>
    /// Creates a failed paged result with a specific error type and messages.
    /// </summary>
    /// <example>
    /// var messages = new[] {
    ///     "Validation failed",
    ///     "Page size must be positive"
    /// };
    /// var result = ResultPaged{User}.Failure{ValidationError}(messages);
    /// </example>
    public static ResultPaged<T> Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new() => Failure()
            .WithMessages(messages)
            .WithError<TError>();

    /// <summary>
    /// Creates a ResultPaged based on a success condition.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.SuccessIf(
    ///     isSuccess: users.Any(),
    ///     values: users,
    ///     count: totalCount,
    ///     error: new ValidationError("No users found")
    /// );
    /// </example>
    public static ResultPaged<T> SuccessIf(
        bool isSuccess,
        IEnumerable<T> values,
        long count = 0,
        int page = 1,
        int pageSize = 10,
        IResultError error = null) => isSuccess
            ? Success(values, count, page, pageSize)
            : Failure().WithError(error);

    /// <summary>
    /// Creates a ResultPaged based on a predicate function.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.SuccessIf(
    ///     predicate: users => users.All(u => u.IsActive),
    ///     values: users,
    ///     count: totalCount,
    ///     error: new ValidationError("Inactive users found")
    /// );
    /// </example>
    public static ResultPaged<T> SuccessIf(
        Func<IEnumerable<T>, bool> predicate,
        IEnumerable<T> values,
        long count = 0,
        int page = 1,
        int pageSize = 10,
        IResultError error = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        try
        {
            var isSuccess = predicate(values);
            return SuccessIf(isSuccess, values, count, page, pageSize, error);
        }
        catch (Exception ex)
        {
            return Failure().WithError(Result.Settings.ExceptionErrorFactory(ex));
        }
    }

    /// <summary>
    /// Creates a ResultPaged based on a failure condition.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.FailureIf(
    ///     isFailure: page > totalPages,
    ///     values: users,
    ///     count: totalCount,
    ///     error: new ValidationError("Page number exceeds total pages")
    /// );
    /// </example>
    public static ResultPaged<T> FailureIf(
        bool isFailure,
        IEnumerable<T> values,
        long count = 0,
        int page = 1,
        int pageSize = 10,
        IResultError error = null) => isFailure
            ? Failure().WithError(error)
            : Success(values, count, page, pageSize);

    /// <summary>
    /// Creates a ResultPaged based on a failure predicate function.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.FailureIf(
    ///     predicate: users => !users.Any(),
    ///     values: users,
    ///     count: totalCount,
    ///     error: new ValidationError("No users found")
    /// );
    /// </example>
    public static ResultPaged<T> FailureIf(
        Func<IEnumerable<T>, bool> predicate,
        IEnumerable<T> values,
        long count = 0,
        int page = 1,
        int pageSize = 10,
        IResultError error = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        try
        {
            var isFailure = predicate(values);
            return FailureIf(isFailure, values, count, page, pageSize, error);
        }
        catch (Exception ex)
        {
            return Failure().WithError(Result.Settings.ExceptionErrorFactory(ex));
        }
    }

    /// <summary>
    /// Adds a message to the result.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Success(users)
    ///     .WithMessage("Retrieved active users")
    ///     .WithMessage("Applied department filter");
    /// </example>
    public ResultPaged<T> WithMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return this;
        }

        return new ResultPaged<T>(
            this.value,
            this.success,
            this.totalCount,
            this.currentPage,
            this.pageSize,
            this.messages.Add(message),
            this.errors);
    }

    /// <summary>
    /// Adds multiple messages to the result.
    /// </summary>
    /// <example>
    /// var messages = new[] { "Page 1 of 5", "Filtered by department" };
    /// var result = ResultPaged{User}.Success(users)
    ///     .WithMessages(messages);
    /// </example>
    public ResultPaged<T> WithMessages(IEnumerable<string> messages)
    {
        if (messages is null)
        {
            return this;
        }

        return new ResultPaged<T>(
                this.value,
                this.success,
                this.totalCount,
                this.currentPage,
                this.pageSize,
                this.messages.AddRange(messages),
                this.errors);
    }

    /// <summary>
    /// Adds an error to the result.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Success(users)
    ///     .WithError(new ValidationError("Page size too large"));
    /// </example>
    public ResultPaged<T> WithError(IResultError error)
    {
        if (error is null)
        {
            return this;
        }

        return new ResultPaged<T>(
            this.value,
            false, // Set success to false when adding error
            this.totalCount,
            this.currentPage,
            this.pageSize,
            this.messages,
            this.errors.Add(error));
    }

    /// <summary>Adds an error with the supplied message and marks the paged result as failed.</summary>
    /// <param name="errorMessage">The message stored in the appended <see cref="Error"/>.</param>
    /// <returns>A failed paged result that preserves paging metadata, values, messages, and existing errors.</returns>
    public ResultPaged<T> WithError(string errorMessage)
        => this.WithError(new Error(errorMessage));

    /// <summary>Adds an exception error while preserving the current values and paging metadata.</summary>
    /// <param name="exception">The exception to wrap as an error. A null exception leaves the result unchanged.</param>
    /// <returns>The failed paged result if an exception was supplied, or the current result otherwise.</returns>
    /// <example>
    /// <code>
    /// var failedPage = page.WithError(new InvalidOperationException("Page processing failed."));
    /// </code>
    /// </example>
    public ResultPaged<T> WithError(Exception exception)
        => exception is null ? this : this.WithError(new ExceptionError(exception));

    /// <summary>
    /// Adds multiple errors to the result.
    /// </summary>
    /// <example>
    /// var errors = new IResultError[] {
    ///     new ValidationError("Invalid page"),
    ///     new ValidationError("Invalid size")
    /// };
    /// var result = ResultPaged{User}.Success(users)
    ///     .WithErrors(errors);
    /// </example>
    public ResultPaged<T> WithErrors(IEnumerable<IResultError> errors)
    {
        var combined = Result.AppendErrors(this.errors, errors);

        return combined.Count == this.errors.Count
            ? this
            : new ResultPaged<T>(this.value, false, this.totalCount, this.currentPage, this.pageSize, this.messages, combined);
    }

    /// <summary>Appends another outcome's diagnostics and combines its success state with this page's state.</summary>
    /// <param name="outcome">The outcome whose success state and diagnostics to include.</param>
    /// <returns>A result that preserves the current values and paging metadata. Either failure makes the result fail.</returns>
    /// <example>
    /// <code>
    /// var page = ResultPaged&lt;int&gt;.Success([21], count: 37, page: 3, pageSize: 10);
    /// var result = page.WithOutcome(Result.Failure("Could not verify the page."));
    /// </code>
    /// </example>
    public ResultPaged<T> WithOutcome(IResult outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var combinedErrors = Result.AppendErrors(this.errors, outcome.Errors);

        return new ResultPaged<T>(
            this.value,
            this.IsSuccess && outcome.IsSuccess && combinedErrors.IsEmpty,
            this.totalCount,
            this.currentPage,
            this.pageSize,
            this.messages.AddRange(outcome.Messages),
            combinedErrors);
    }

    internal ResultPaged<T> WithPrependedOutcome<TSource>(ResultPaged<TSource> source) =>
        new(
            this.value,
            this.IsSuccess && source.IsSuccess,
            this.totalCount,
            this.currentPage,
            this.pageSize,
            source.messages.AddRange(this.messages.AsEnumerable()),
            source.errors.AddRange(this.errors.AsEnumerable()));

    internal ResultPaged<T> AsFailure() =>
        this.IsSuccess
            ? new(this.value, false, this.totalCount, this.currentPage, this.pageSize, this.messages, this.errors)
            : this;

    internal ResultPaged<T> WithExceptionFailure(Exception exception) =>
        this.AsFailure().WithError(Result.Settings.ExceptionErrorFactory(exception));

    /// <summary>
    /// Adds a specific error type to the result.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Success(users)
    ///     .WithError{ValidationError}();
    /// </example>
    public ResultPaged<T> WithError<TError>()
        where TError : IResultError, new() => this.WithError(Activator.CreateInstance<TError>());

    /// <summary>
    /// Converts a generic ResultPaged{T} to a non-generic Result.
    /// </summary>
    /// <example>
    /// var result = resultPaged.For();
    /// if (result.IsSuccess)
    /// {
    ///     Console.WriteLine("Operation successful");
    /// }
    /// </example>
    public Result Unwrap()
    {
        var result = this;

        return this.Match(
            _ => Result.Success().WithMessages(result.Messages).WithErrors(result.Errors),
            _ => Result.Failure().WithMessages(result.Messages).WithErrors(result.Errors));
    }

    /// <summary>
    /// Converts to a different page type with an empty value sequence, preserving metadata and diagnostics.
    /// </summary>
    /// <example>
    /// var userDtos = pagedUsers.For{UserDto}();
    /// </example>
    public ResultPaged<TOutput> For<TOutput>() =>
        new(this.value is null ? null : [], this.success, this.totalCount, this.currentPage, this.pageSize, this.messages, this.errors);

    /// <summary>
    /// Converts to a different page type, preserving metadata and diagnostics. The provided values replace a successful page's values.
    /// </summary>
    /// <example>
    /// var dtos = users.Select(u => u.ToDto());
    /// var userDtos = pagedUsers.For(dtos);
    /// </example>
    public ResultPaged<TOutput> For<TOutput>(IEnumerable<TOutput> values) =>
        this.success && (this.HasPage || values is not null)
            ? new ResultPaged<TOutput>(values ?? [], true, this.totalCount, this.currentPage, this.pageSize, this.messages, this.errors)
            : this.For<TOutput>();

    internal ResultPaged<TOutput> CreateItemProjection<TOutput>(IEnumerable<TOutput> values) =>
        this.HasPage
            ? ResultPaged<TOutput>.Success(values, this.TotalCount, this.CurrentPage, this.PageSize)
            : ResultPaged<TOutput>.FromOutcome(true, default, default);

    /// <summary>
    /// Implicit conversion to boolean based on success state.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Success(users);
    /// if (result) // Implicitly converts to true
    /// {
    ///     Console.WriteLine("Success!");
    /// }
    /// </example>
    public static implicit operator bool(ResultPaged<T> result) =>
        result.IsSuccess;

    /// <summary>
    /// Implicit conversion to base Result type.
    /// </summary>
    /// <example>
    /// Result baseResult = resultPaged; // Implicitly converts
    /// </example>
    public static implicit operator Result(ResultPaged<T> result) => result.Unwrap();

    /// <summary>
    /// Implicit conversion to Result{IEnumerable{TValue}}.
    /// </summary>
    /// <example>
    /// Result{IEnumerable{User}} enumerable = resultPaged; // Implicitly converts
    /// </example>
    public static implicit operator Result<IEnumerable<T>>(ResultPaged<T> result) =>
        result.Match(
            _ => Result<IEnumerable<T>>.Success(result.Value)
                .WithMessages(result.Messages)
                .WithErrors(result.Errors),
            _ => Result<IEnumerable<T>>.Failure()
                .WithMessages(result.Messages)
                .WithErrors(result.Errors));

    /// <summary>
    /// Checks if the result contains any errors.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Failure()
    ///     .WithError(new ValidationError("Invalid page"));
    /// if (result.HasError())
    /// {
    ///     Console.WriteLine("Errors found");
    /// }
    /// </example>
    public bool HasError() => !this.errors.IsEmpty;

    /// <summary>
    /// Checks if the result contains an error of a specific type.
    /// </summary>
    /// <remarks>Matches errors assignable to <typeparamref name="TError"/>.</remarks>
    /// <example>
    /// var result = ResultPaged{User}.Failure()
    ///     .WithError(new ValidationError("Invalid page size"));
    /// if (result.HasError{ValidationError}())
    /// {
    ///     Console.WriteLine("Validation error found");
    /// }
    /// </example>
    public bool HasError<TError>()
        where TError : class, IResultError => this.GetError<TError>() is not null;

    /// <summary>
    /// Attempts to get the first error of a specific type.
    /// </summary>
    /// <remarks>Matches errors assignable to <typeparamref name="TError"/>.</remarks>
    /// <example>
    /// if (result.TryGetError{ValidationError}(out var error))
    /// {
    ///     Console.WriteLine($"Validation failed: {error.Message}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("No validation errors found");
    /// }
    /// </example>
    public bool TryGetError<TError>(out TError error)
        where TError : class, IResultError
    {
        error = this.GetError<TError>();

        return error is not null;
    }

    /// <summary>
    /// Attempts to get all errors of a specific type.
    /// </summary>
    /// <remarks>Matches errors assignable to <typeparamref name="TError"/>.</remarks>
    /// <example>
    /// if (result.TryGetErrors{ValidationError}(out var errors))
    /// {
    ///     foreach (var error in errors)
    ///     {
    ///         Console.WriteLine($"Validation error: {error.Message}");
    ///     }
    /// }
    /// </example>
    public bool TryGetErrors<TError>(out IEnumerable<TError> errors)
        where TError : class, IResultError
    {
        errors = this.GetErrors<TError>();

        return errors.Any();
    }

    /// <summary>
    /// Gets the first error of the specified type.
    /// </summary>
    /// <remarks>Matches errors assignable to <typeparamref name="TError"/>.</remarks>
    /// <example>
    /// var error = result.GetError{ValidationError}();
    /// if (error is not null)
    /// {
    ///     Console.WriteLine($"Found error: {error.Message}");
    /// }
    /// </example>
    public TError GetError<TError>()
        where TError : class, IResultError =>
        this.errors.AsEnumerable().FirstOrDefault(static error => error is TError) as TError;

    /// <summary>
    /// Gets all errors of the specified type.
    /// </summary>
    /// <remarks>Matches errors assignable to <typeparamref name="TError"/>.</remarks>
    /// <example>
    /// var validationErrors = result.GetErrors{ValidationError}();
    /// foreach (var error in validationErrors)
    /// {
    ///     Console.WriteLine($"Validation error: {error.Message}");
    /// }
    /// </example>
    public IEnumerable<TError> GetErrors<TError>()
        where TError : class, IResultError => this.errors.AsEnumerable().OfType<TError>();

    /// <summary>
    /// Creates a ResultPaged from an operation that returns both values and count.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.For(
    ///     () => {
    ///         var users = _repository.GetUsers(page, pageSize);
    ///         var count = _repository.GetTotalCount();
    ///         return (users, count);
    ///     },
    ///     page: 1,
    ///     pageSize: 10
    /// );
    /// </example>
    public static ResultPaged<T> For(
        Func<(IEnumerable<T> Values, long Count)> operation,
        int page = 1,
        int pageSize = 10)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            var (values, count) = operation();
            return Success(values, count, page, pageSize);
        }
        catch (Exception ex)
        {
            return Failure().WithError(Result.Settings.ExceptionErrorFactory(ex));
        }
    }

    /// <summary>
    /// Creates a ResultPaged from an async operation that returns both values and count.
    /// </summary>
    /// <example>
    /// var result = await ResultPaged{User}.ForAsync(
    ///     async ct => {
    ///         var users = await _repository.GetUsersAsync(page, pageSize, ct);
    ///         var count = await _repository.GetTotalCountAsync(ct);
    ///         return (users, count);
    ///     },
    ///     page: 1,
    ///     pageSize: 10,
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<T>> ForAsync(
        Func<CancellationToken, Task<(IEnumerable<T> Values, long Count)>> operation,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            var (values, count) = await operation(cancellationToken);
            return Success(values, count, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            return Failure().WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Failure().WithError(Result.Settings.ExceptionErrorFactory(ex));
        }
    }

    /// <summary>
    /// Combines paged results in input order. The combined result succeeds only if every input succeeds.
    /// </summary>
    /// <param name="results">The pages and any status-only outcomes to combine.</param>
    /// <returns>A new result with materialized values and the combined diagnostics.</returns>
    /// <remarks>
    /// Status-only outcomes contribute state and diagnostics, without supplying paging metadata.
    /// A single page keeps its metadata. For multiple pages, the total count is the number of
    /// combined values, and the page number and page size are the maximum values supplied by those pages.
    /// </remarks>
    /// <example>
    /// <code>
    /// var validation = Result.Success("Query validated").ToResultPaged&lt;int&gt;();
    /// var page = ResultPaged&lt;int&gt;.Success([21], count: 37, page: 3, pageSize: 10);
    /// var result = ResultPaged&lt;int&gt;.Merge(validation, page);
    /// </code>
    /// </example>
    public static ResultPaged<T> Merge(params ResultPaged<T>[] results)
    {
        if (results is null || results.Length == 0)
        {
            return new ResultPaged<T>(null, true);
        }

        var isSuccess = true;
        var values = new List<T>();
        var pageCount = 0;
        var totalCount = 0L;
        var currentPage = 1;
        var pageSize = 0;

        foreach (var result in results)
        {
            if (result.value is not null)
            {
                values.AddRange(result.value);
                pageCount++;
                totalCount = result.TotalCount;
                currentPage = Math.Max(currentPage, result.CurrentPage);
                pageSize = Math.Max(pageSize, result.PageSize);
            }

            if (result.IsFailure)
            {
                isSuccess = false;
            }
        }

        return new ResultPaged<T>(
            pageCount == 0 ? null : values,
            isSuccess,
            pageCount > 1 ? values.Count : totalCount,
            currentPage,
            pageSize,
            new ValueList<string>().AddRange(results.SelectMany(static result => result.messages.AsEnumerable())),
            new ValueList<IResultError>().AddRange(results.SelectMany(static result => result.errors.AsEnumerable())));
    }

    /// <summary>
    /// Returns a compact, single-line string representation of this result,
    /// including distinct messages and errors when present.
    /// </summary>
    /// <remarks>
    /// - No newlines are included.
    /// - Messages are deduplicated (ordinal).
    /// - Errors are formatted as "[Type] Message" and deduplicated.
    /// - Sections "messages" and "errors" appear only if non-empty.
    /// </remarks>
    /// <example>
    /// var result = Result.Failure()
    ///     .WithMessage("Operation completed with warnings")
    ///     .WithMessage("First step ok")
    ///     .WithMessage("First step ok") // duplicate, will be removed
    ///     .WithError(new ValidationError("Invalid input"))
    ///     .WithError(new ExceptionError(new InvalidOperationException("Invalid op")));
    ///
    /// Console.WriteLine(result.ToString());
    /// // Output (single line):
    /// // Result failed ✗ | messages: Operation completed with warnings; First step ok | errors: [ValidationError] Invalid input; [InvalidOperationException] Invalid op
    /// </example>
    public override string ToString() => this.ToString(string.Empty);

    /// <summary>
    /// Builds a single-line, dense summary of this result.
    /// </summary>
    /// <param name="message">
    /// An optional message to include. If non-empty, it is merged with the
    /// result's own messages. Duplicates are removed.
    /// </param>
    /// <returns>
    /// A compact, single-line string that starts with the success/failure marker,
    /// followed by distinct messages and errors (if present).
    /// Example:
    /// Result failed ✗ | messages: First step ok; Cleanup done | errors: [ValidationError] Email is required
    /// </returns>
    /// <remarks>
    /// - No newlines are included in the output.
    /// - Messages are deduplicated (case-sensitive, ordinal comparison).
    /// - Errors are rendered as "[Type] Message" and deduplicated.
    /// - Sections "messages" and "errors" are included only when non-empty.
    /// </remarks>
    public string ToString(string message)
    {
        // Collect distinct messages (input + result messages)
        var messageSet = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(message))
        {
            messageSet.Add(message.Trim());
        }

        if (!this.messages.IsEmpty)
        {
            foreach (var m in this.messages.AsEnumerable())
            {
                if (!string.IsNullOrWhiteSpace(m))
                {
                    messageSet.Add(m.Trim());
                }
            }
        }

        // Collect distinct error strings as "[Type] Message"
        var errorSet = new HashSet<string>(StringComparer.Ordinal);
        if (!this.errors.IsEmpty)
        {
            foreach (var e in this.errors.AsEnumerable())
            {
                var errorType = e?.GetType().Name ?? "Error";
                var msg = (e?.Message ?? string.Empty).Trim();
                var composed = string.IsNullOrEmpty(msg) ? $"[{errorType}]" : $"[{errorType}] {msg}";
                if (!string.IsNullOrWhiteSpace(composed))
                {
                    errorSet.Add(composed);
                }
            }
        }

        // Build single-line output
        var sb = new StringBuilder();
        var type = typeof(T).PrettyName();
        sb.Append(this.IsSuccess ? $"ResultPaged<{type}> succeeded" : $"ResultPaged<{type}> failed");

        if (messageSet.Count > 0)
        {
            sb.Append(" - messages: ").Append(string.Join("; ", messageSet));
        }

        if (errorSet.Count > 0)
        {
            sb.Append(" - errors: ").Append(string.Join("; ", errorSet));
        }

        return sb.ToString();
    }
}
