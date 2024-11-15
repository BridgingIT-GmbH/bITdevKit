namespace BridgingIT.DevKit.Common;

using System.Collections;

/// <summary>
/// Represents a paged result containing a collection of values with pagination details.
/// Implements value semantics and immutable behavior for thread-safety.
/// </summary>
/// <example>
/// // Creating a successful paged result
/// var items = new[] { 1, 2, 3, 4, 5 };
/// var result = PagedResult{int}.Success(items, count: 100, page: 1, pageSize: 5);
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
public readonly partial struct PagedResult<T> : IResult<IEnumerable<T>>
{
    private readonly bool success;
    private readonly ValueList<string> messages;
    private readonly ValueList<IResultError> errors;
    private readonly IEnumerable<T> value;
    private readonly long totalCount;
    private readonly int currentPage;
    private readonly int pageSize;

    /// <summary>
    /// Initializes a new instance of the PagedResult class with pagination details.
    /// </summary>
    private PagedResult(
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
    public IEnumerable<T> Value => this.value ?? Array.Empty<T>();

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
    /// Creates a successful paged result with the specified values and pagination details.
    /// </summary>
    /// <example>
    /// var items = await dbContext.Users.Skip(0).Take(10).ToListAsync();
    /// var totalCount = await dbContext.Users.LongCountAsync();
    /// var result = PagedResult{User}.Success(
    ///     values: items,
    ///     count: totalCount,
    ///     page: 1,
    ///     pageSize: 10
    /// );
    /// </example>
    public static PagedResult<T> Success(
        IEnumerable<T> values,
        long count = 0,
        int page = 1,
        int pageSize = 10)
    {
        return new PagedResult<T>(values, true, count, page, pageSize);
    }

    /// <summary>
    /// Creates a successful paged result with a message.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.Success(
    ///     values: users,
    ///     message: "Successfully retrieved users",
    ///     count: totalUsers,
    ///     page: currentPage,
    ///     pageSize: 20
    /// );
    /// </example>
    public static PagedResult<T> Success(
        IEnumerable<T> values,
        string message,
        long count = 0,
        int page = 1,
        int pageSize = 10)
    {
        return new PagedResult<T>(values, true, count, page, pageSize)
            .WithMessage(message);
    }

    /// <summary>
    /// Creates a successful paged result with multiple messages.
    /// </summary>
    /// <example>
    /// var messages = new[] {
    ///     "Users retrieved",
    ///     "Applied active status filter"
    /// };
    /// var result = PagedResult{User}.Success(
    ///     values: activeUsers,
    ///     messages: messages,
    ///     count: totalActive,
    ///     page: 1,
    ///     pageSize: 10
    /// );
    /// </example>
    public static PagedResult<T> Success(
        IEnumerable<T> values,
        IEnumerable<string> messages,
        long count = 0,
        int page = 1,
        int pageSize = 10)
    {
        return new PagedResult<T>(values, true, count, page, pageSize)
            .WithMessages(messages);
    }

    /// <summary>
    /// Creates a failed paged result.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.Failure();
    /// if (result.IsFailure)
    /// {
    ///     Console.WriteLine("Failed to retrieve users");
    /// }
    /// </example>
    public static PagedResult<T> Failure()
    {
        return new PagedResult<T>(Array.Empty<T>(), false);
    }

    /// <summary>
    /// Creates a failed PagedResult{T} with the specified values.
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
    public static PagedResult<T> Failure(IEnumerable<T> values)
    {
        return new PagedResult<T>(values, false);
    }

    /// <summary>
    /// Creates a failed paged result with a specific error type.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.Failure{DatabaseError}();
    /// if (result.HasError{DatabaseError}())
    /// {
    ///     Console.WriteLine("Database error occurred");
    /// }
    /// </example>
    public static PagedResult<T> Failure<TError>()
        where TError : IResultError, new()
    {
        return new PagedResult<T>(Array.Empty<T>(), false)
            .WithError<TError>();
    }

    /// <summary>
    /// Creates a failed paged result with a message and optional error.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.Failure(
    ///     message: "Failed to retrieve users",
    ///     error: new DatabaseError("Connection timeout")
    /// );
    /// </example>
    public static PagedResult<T> Failure(string message, IResultError error = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var result = new PagedResult<T>(Array.Empty<T>(), false)
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
    /// var result = PagedResult{User}.Failure(messages, errors);
    /// </example>
    public static PagedResult<T> Failure(
        IEnumerable<string> messages,
        IEnumerable<IResultError> errors = null)
    {
        return new PagedResult<T>(Array.Empty<T>(), false)
            .WithMessages(messages)
            .WithErrors(errors);
    }

    /// <summary>
    /// Creates a failed paged result with a specific error type and message.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.Failure{ValidationError}(
    ///     "Invalid page parameters"
    /// );
    /// </example>
    public static PagedResult<T> Failure<TError>(string message)
        where TError : IResultError, new()
    {
        ArgumentNullException.ThrowIfNull(message);

        return new PagedResult<T>(Array.Empty<T>(), false)
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
    /// var result = PagedResult{User}.Failure{ValidationError}(messages);
    /// </example>
    public static PagedResult<T> Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new()
    {
        return new PagedResult<T>(Array.Empty<T>(), false)
            .WithMessages(messages)
            .WithError<TError>();
    }

      /// <summary>
    /// Creates a PagedResult based on a success condition.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.SuccessIf(
    ///     isSuccess: users.Any(),
    ///     values: users,
    ///     count: totalCount,
    ///     error: new ValidationError("No users found")
    /// );
    /// </example>
    public static PagedResult<T> SuccessIf(
        bool isSuccess,
        IEnumerable<T> values,
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
    /// Creates a PagedResult based on a predicate function.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.SuccessIf(
    ///     predicate: users => users.All(u => u.IsActive),
    ///     values: users,
    ///     count: totalCount,
    ///     error: new ValidationError("Inactive users found")
    /// );
    /// </example>
    public static PagedResult<T> SuccessIf(
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
    /// Creates a PagedResult based on a failure condition.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.FailureIf(
    ///     isFailure: page > totalPages,
    ///     values: users,
    ///     count: totalCount,
    ///     error: new ValidationError("Page number exceeds total pages")
    /// );
    /// </example>
    public static PagedResult<T> FailureIf(
        bool isFailure,
        IEnumerable<T> values,
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
    /// Creates a PagedResult based on a failure predicate function.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.FailureIf(
    ///     predicate: users => !users.Any(),
    ///     values: users,
    ///     count: totalCount,
    ///     error: new ValidationError("No users found")
    /// );
    /// </example>
    public static PagedResult<T> FailureIf(
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
    /// var result = PagedResult{User}.Success(users)
    ///     .WithMessage("Retrieved active users")
    ///     .WithMessage("Applied department filter");
    /// </example>
    public PagedResult<T> WithMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return this;
        }

        return new PagedResult<T>(
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
    /// var result = PagedResult{User}.Success(users)
    ///     .WithMessages(messages);
    /// </example>
    public PagedResult<T> WithMessages(IEnumerable<string> messages)
    {
        if (messages is null)
        {
            return this;
        }

        return new PagedResult<T>(
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
    /// var result = PagedResult{User}.Success(users)
    ///     .WithError(new ValidationError("Page size too large"));
    /// </example>
    public PagedResult<T> WithError(IResultError error)
    {
        if (error is null)
        {
            return this;
        }

        return new PagedResult<T>(
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
    /// var result = PagedResult{User}.Success(users)
    ///     .WithErrors(errors);
    /// </example>
    public PagedResult<T> WithErrors(IEnumerable<IResultError> errors)
    {
        if (errors is null || !errors.Any())
        {
            return this;
        }

        return new PagedResult<T>(
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
    /// var result = PagedResult{User}.Success(users)
    ///     .WithError{ValidationError}();
    /// </example>
    public PagedResult<T> WithError<TError>()
        where TError : IResultError, new()
    {
        return this.WithError(Activator.CreateInstance<TError>());
    }

      /// <summary>
    /// Executes different functions based on the result's success state.
    /// </summary>
    /// <example>
    /// var message = result.Match(
    ///     onSuccess: r => $"Found {r.TotalCount} users across {r.TotalPages} pages",
    ///     onFailure: errors => $"Failed with {errors.Count} errors"
    /// );
    /// </example>
    public TResult Match<TResult>(
        Func<PagedResult<T>, TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this.IsSuccess
            ? onSuccess(this)
            : onFailure(this.Errors);
    }

    /// <summary>
    /// Returns different values based on the result's success state.
    /// </summary>
    /// <example>
    /// var status = result.Match(
    ///     success: "Users retrieved successfully",
    ///     failure: "Failed to retrieve users"
    /// );
    /// </example>
    public TResult Match<TResult>(TResult success, TResult failure)
    {
        return this.IsSuccess ? success : failure;
    }

    /// <summary>
    /// Asynchronously executes different functions based on the result's success state.
    /// </summary>
    /// <example>
    /// var status = await result.MatchAsync(
    ///     async (r, ct) => await FormatSuccessMessageAsync(r, ct),
    ///     async (errors, ct) => await FormatErrorMessageAsync(errors, ct),
    ///     cancellationToken
    /// );
    /// </example>
    public Task<TResult> MatchAsync<TResult>(
        Func<PagedResult<T>, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this.IsSuccess
            ? onSuccess(this, cancellationToken)
            : onFailure(this.Errors, cancellationToken);
    }

    /// <summary>
    /// Converts to a regular Result.
    /// </summary>
    /// <example>
    /// var result = pagedResult.For();
    /// if (result.IsSuccess)
    /// {
    ///     Console.WriteLine("Operation successful");
    /// }
    /// </example>
    public Result For()
    {
        var result = this;

        return this.Match(
            _ => Result.Success().WithMessages(result.Messages).WithErrors(result.Errors),
            _ => Result.Failure().WithMessages(result.Messages).WithErrors(result.Errors));
    }

    /// <summary>
    /// Converts to a different PagedResult type.
    /// </summary>
    /// <example>
    /// var userDtos = pagedUsers.For{UserDto}();
    /// </example>
    public PagedResult<TOutput> For<TOutput>()
    {
        var result = this;

        return this.Match(
            _ => PagedResult<TOutput>.Success(
                    Array.Empty<TOutput>(),
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithMessages(result.Messages)
                .WithErrors(result.Errors),
            _ => PagedResult<TOutput>.Failure()
                .WithMessages(result.Messages)
                .WithErrors(result.Errors));
    }

    /// <summary>
    /// Converts to a different PagedResult type with provided values.
    /// </summary>
    /// <example>
    /// var dtos = users.Select(u => u.ToDto());
    /// var userDtos = pagedUsers.For(dtos);
    /// </example>
    public PagedResult<TOutput> For<TOutput>(IEnumerable<TOutput> values)
    {
        var result = this;

        return this.Match(
            _ => PagedResult<TOutput>.Success(
                    values,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithMessages(result.Messages)
                .WithErrors(result.Errors),
            _ => PagedResult<TOutput>.Failure()
                .WithMessages(result.Messages)
                .WithErrors(result.Errors));
    }

    /// <summary>
    /// Implicit conversion to boolean based on success state.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.Success(users);
    /// if (result) // Implicitly converts to true
    /// {
    ///     Console.WriteLine("Success!");
    /// }
    /// </example>
    public static implicit operator bool(PagedResult<T> result) =>
        result.IsSuccess;

    /// <summary>
    /// Implicit conversion to base Result type.
    /// </summary>
    /// <example>
    /// Result baseResult = pagedResult; // Implicitly converts
    /// </example>
    public static implicit operator Result(PagedResult<T> result) =>
        result.For();

    /// <summary>
    /// Implicit conversion to Result{IEnumerable{TValue}}.
    /// </summary>
    /// <example>
    /// Result{IEnumerable{User}} enumerable = pagedResult; // Implicitly converts
    /// </example>
    public static implicit operator Result<IEnumerable<T>>(PagedResult<T> result) =>
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
    /// var result = PagedResult{User}.Failure()
    ///     .WithError(new ValidationError("Invalid page"));
    /// if (result.HasError())
    /// {
    ///     Console.WriteLine("Errors found");
    /// }
    /// </example>
    public bool HasError()
    {
        return !this.errors.IsEmpty;
    }

    /// <summary>
    /// Checks if the result contains an error of a specific type.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.Failure()
    ///     .WithError(new ValidationError("Invalid page size"));
    /// if (result.HasError{ValidationError}())
    /// {
    ///     Console.WriteLine("Validation error found");
    /// }
    /// </example>
    public bool HasError<TError>() where TError : IResultError
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
    public bool TryGetError<TError>(out IResultError error) where TError : IResultError
    {
        error = this.errors.AsEnumerable()
            .FirstOrDefault(e => e.GetType() == typeof(TError));

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
    public bool TryGetErrors<TError>(out IEnumerable<IResultError> errors) where TError : IResultError
    {
        var errorType = typeof(TError);
        errors = this.errors.AsEnumerable()
            .Where(e => e.GetType() == errorType)
            .ToList();

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
    public IResultError GetError<TError>() where TError : IResultError
    {
        return this.errors.AsEnumerable()
            .FirstOrDefault(e => e.GetType() == typeof(TError));
    }

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
    public IEnumerable<IResultError> GetErrors<TError>() where TError : IResultError
    {
        var errorType = typeof(TError);
        return this.errors.AsEnumerable()
            .Where(e => e.GetType() == errorType)
            .ToList();
    }

    /// <summary>
    /// Creates a PagedResult from an operation that returns both values and count.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.For(
    ///     () => {
    ///         var users = _repository.GetUsers(page, pageSize);
    ///         var count = _repository.GetTotalCount();
    ///         return (users, count);
    ///     },
    ///     page: 1,
    ///     pageSize: 10
    /// );
    /// </example>
    public static PagedResult<T> For(
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
    /// Creates a PagedResult from an async operation that returns both values and count.
    /// </summary>
    /// <example>
    /// var result = await PagedResult{User}.ForAsync(
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
    public static async Task<PagedResult<T>> ForAsync(
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

}