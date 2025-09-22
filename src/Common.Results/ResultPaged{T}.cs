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
public readonly partial struct ResultPaged<T> : IResultPaged<T>
{
    private readonly bool success;
    private readonly ValueList<string> messages;
    private readonly ValueList<IResultError> errors;
    private readonly IEnumerable<T> value;
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
        int pageSize = 10,
        ValueList<string> messages = default, ValueList<IResultError> errors = default)
    {
        this.success = success;
        this.value = value;
        this.totalCount = count;
        this.currentPage = page < 1 ? 1 : page;
        this.pageSize = pageSize < 1 ? 10 : pageSize;
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
    public int CurrentPage => this.currentPage;

    /// <summary>
    /// Gets the total number of pages based on total count and page size.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling(this.totalCount / (double)this.pageSize);

    /// <summary>
    /// Gets the total count of items across all pages.
    /// </summary>
    public long TotalCount => this.totalCount;

    /// <summary>
    /// Gets the number of items per page.
    /// </summary>
    public int PageSize => this.pageSize;

    /// <summary>
    /// Gets a value indicating whether there is a previous page available.
    /// </summary>
    public bool HasPreviousPage => this.currentPage > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page available.
    /// </summary>
    public bool HasNextPage => this.currentPage < this.TotalPages;

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
    /// Tries to perform an operation on the current page collection.
    /// </summary>
    /// <example>
    /// var result = ResultPaged{User}.Try(() => {
    ///     var users = _repository.GetPagedUsers(page, pageSize);
    ///     return (users, totalCount: _repository.GetTotalCount());
    /// });
    /// </example>
    public static ResultPaged<T> From(
        Func<(IEnumerable<T> Values, long TotalCount)> operation)
    {
        if (operation is null)
        {
            return Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var (values, totalCount) = operation();
            return Success(values, totalCount, 1, values.Count());
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously tries to perform an operation that returns a paged collection.
    /// </summary>
    /// <example>
    /// var result = await ResultPaged{User}.TryAsync(
    ///     async ct => {
    ///         var users = await _repository.GetPagedUsersAsync(page, pageSize, ct);
    ///         var count = await _repository.GetTotalCountAsync(ct);
    ///         return (users, count);
    ///     },
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<T>> FromAsync(
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
            var (values, totalCount) = await operation(cancellationToken);
            return Success(values, 1, values.Count());
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
    public static ResultPaged<T> Success(IEnumerable<T> values, long count = 0, int page = 1, int pageSize = 10) => new ResultPaged<T>(values, true, count, page, pageSize);

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
        int pageSize = 10) => new ResultPaged<T>(values, true, count, page, pageSize)
            .WithMessage(message);

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
        int pageSize = 10) => new ResultPaged<T>(values, true, count, page, pageSize)
            .WithMessages(messages);

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
    public static ResultPaged<T> Failure() => new ResultPaged<T>([], false);

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
    public static ResultPaged<T> Failure(IEnumerable<T> values) => new ResultPaged<T>(values, false);

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
        where TError : IResultError, new() => new ResultPaged<T>([], false).WithError<TError>();

    /// <summary>
    /// Creates a failed paged result with a specific error instance.
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    public static ResultPaged<T> Failure(IResultError error)
         => new ResultPaged<T>([], false).WithError(error);

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

        var result = new ResultPaged<T>([], false)
            .WithMessage(message);

        return error is null ? result : result.WithError(error);
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
        IEnumerable<IResultError> errors = null) => new ResultPaged<T>([], false)
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

        return new ResultPaged<T>([], false)
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
        where TError : IResultError, new() => new ResultPaged<T>([], false)
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
        if (errors?.Any() != true)
        {
            return this;
        }

        return new ResultPaged<T>(
                this.value,
                false, // Set success to false when adding errors
                this.totalCount,
                this.currentPage,
                this.pageSize,
                this.messages,
                this.errors.AddRange(errors));
    }

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
    /// Converts to a different ResultPaged type.
    /// </summary>
    /// <example>
    /// var userDtos = pagedUsers.For{UserDto}();
    /// </example>
    public ResultPaged<TOutput> For<TOutput>()
    {
        var result = this;

        return this.Match(
            _ => ResultPaged<TOutput>.Success(
                    [],
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithMessages(result.Messages)
                .WithErrors(result.Errors),
            _ => ResultPaged<TOutput>.Failure()
                .WithMessages(result.Messages)
                .WithErrors(result.Errors));
    }

    /// <summary>
    /// Converts to a different ResultPaged type with provided values.
    /// </summary>
    /// <example>
    /// var dtos = users.Select(u => u.ToDto());
    /// var userDtos = pagedUsers.For(dtos);
    /// </example>
    public ResultPaged<TOutput> For<TOutput>(IEnumerable<TOutput> values)
    {
        var result = this;

        return this.Match(
            _ => ResultPaged<TOutput>.Success(
                    values,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithMessages(result.Messages)
                .WithErrors(result.Errors),
            _ => ResultPaged<TOutput>.Failure()
                .WithMessages(result.Messages)
                .WithErrors(result.Errors));
    }

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
    public static implicit operator Result(ResultPaged<T> result) =>
        result.Match(
            _ => Result.Success().WithMessages(result.Messages).WithErrors(result.Errors),
            _ => Result.Failure().WithMessages(result.Messages).WithErrors(result.Errors));

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
    /// <example>
    /// var result = ResultPaged{User}.Failure()
    ///     .WithError(new ValidationError("Invalid page size"));
    /// if (result.HasError{ValidationError}())
    /// {
    ///     Console.WriteLine("Validation error found");
    /// }
    /// </example>
    public bool HasError<TError>()
        where TError : class, IResultError
    {
        var errorType = typeof(TError);

        return this.errors.AsEnumerable().Any(e => e.GetType() == errorType);
    }

    /// <summary>
    /// Attempts to get the first error of a specific type.
    /// </summary>
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
        error = this.errors.AsEnumerable()
            .FirstOrDefault(e => e.GetType() == typeof(TError)) as TError;

        return error is not null;
    }

    /// <summary>
    /// Attempts to get all errors of a specific type.
    /// </summary>
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
        var errorType = typeof(TError);
        errors = [.. this.errors.AsEnumerable()
            .Where(e => e.GetType() == errorType)
            .Cast<TError>()];

        return errors.Any();
    }

    /// <summary>
    /// Gets the first error of the specified type.
    /// </summary>
    /// <example>
    /// var error = result.GetError{ValidationError}();
    /// if (error is not null)
    /// {
    ///     Console.WriteLine($"Found error: {error.Message}");
    /// }
    /// </example>
    public TError GetError<TError>()
        where TError : class, IResultError => this.errors.AsEnumerable()
            .FirstOrDefault(e => e.GetType() == typeof(TError)) as TError;

    /// <summary>
    /// Gets all errors of the specified type.
    /// </summary>
    /// <example>
    /// var validationErrors = result.GetErrors{ValidationError}();
    /// foreach (var error in validationErrors)
    /// {
    ///     Console.WriteLine($"Validation error: {error.Message}");
    /// }
    /// </example>
    public IEnumerable<TError> GetErrors<TError>()
        where TError : class, IResultError
    {
        var errorType = typeof(TError);
        return [.. this.errors.AsEnumerable()
            .Where(e => e.GetType() == errorType)
            .Cast<TError>()];
    }

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
    /// Returns a string representation of the Result.
    /// </summary>
    /// <example>
    /// var result = Result.Success("Operation completed")
    ///     .WithError(new ValidationError("Invalid input"));
    /// Console.WriteLine(result.ToResultString());
    /// // Output:
    /// // Success: False
    /// // Messages:
    /// // - Operation completed
    /// // Errors:
    /// // - [ValidationError] Invalid input
    /// </example>
    public override string ToString() => this.ToString(string.Empty);

    public string ToString(string message)
    {
        var sb = new StringBuilder();

        // Append success or failure message
        if (this.IsSuccess)
        {
            sb.Append("Result succeeded ✓");
        }
        else
        {
            sb.Append("Result failed ✗");
        }

        // Append type name
        sb.Append(" [").Append(typeof(T).Name).Append(']');

        // Append message only if not null or empty
        if (!string.IsNullOrEmpty(message))
        {
            sb.Append(' ').Append(message);
        }

        // Remove trailing spaces and add a single newline
        while (sb.Length > 0 && sb[^1] == ' ')
        {
            sb.Length--;
        }
        sb.AppendLine();

        // Append messages if any
        if (!this.messages.IsEmpty)
        {
            sb.AppendLine("  messages:");
            foreach (var m in this.messages.AsEnumerable())
            {
                sb.Append("  - ").AppendLine(m);
            }
        }

        // Append errors if any
        if (!this.errors.IsEmpty)
        {
            sb.AppendLine("  errors:");
            foreach (var e in this.errors.AsEnumerable())
            {
                sb.Append("  - [").Append(e.GetType().Name).Append("] ").AppendLine(e.Message);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Combines multiple Results into a single Result.
    /// The combined Result is successful only if all Results are successful.
    /// </summary>
    /// <example>
    /// var result1 = Result.Success("First operation");
    /// var result2 = Result.Success("Second operation");
    /// var result3 = Result.Failure("Third operation failed");
    ///
    /// var combined = Result.Combine(result1, result2, result3);
    /// // combined.IsFailure == true
    /// // combined.Messages contains all three messages
    /// </example>
    public static ResultPaged<T> Merge(params ResultPaged<T>[] results)
    {
        if (results is null || results.Length == 0)
        {
            return ResultPaged<T>.Success([], 0, 0, 0);
        }

        var isSuccess = true;
        var values = new List<T>();

        var combinedMessages = new ValueList<string>();
        var combinedErrors = new ValueList<IResultError>();

        foreach (var result in results)
        {
            if (result.value?.Count() > 0)
            {
                values.AddRange(result.Value);
            }

            if (result.IsFailure)
            {
                isSuccess = false;
            }

            if (!result.messages.IsEmpty)
            {
                combinedMessages = combinedMessages.AddRange(result.messages.AsEnumerable());
            }

            if (!result.errors.IsEmpty)
            {
                combinedErrors = combinedErrors.AddRange(result.errors.AsEnumerable());
            }
        }

        return new ResultPaged<T>(values, isSuccess, values.Count, results.Max(r => r.CurrentPage), results.Max(r => r.PageSize), combinedMessages, combinedErrors);
    }
}