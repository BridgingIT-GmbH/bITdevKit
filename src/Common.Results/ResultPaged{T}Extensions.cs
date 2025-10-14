namespace BridgingIT.DevKit.Common;

using FluentValidation;
using FluentValidation.Internal;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents the result of an operation, which can either be a success or a failure.
/// Contains functional methods to better work with success and failure results and their values, as well as construct results from actions or tasks.
/// </summary>
public static class ResultPagedExtensions
{
    private static readonly EventId ResultLogEvent = new(10001, "Result");

    /// <summary>
    /// Throws a <see cref="ResultException"/> if the current result is a failure.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <returns>The current result if it is successful.</returns>
    /// <exception cref="ResultException">Thrown if the current result is a failure.</exception>
    public static ResultPaged<T> ThrowIfFailed<T>(this ResultPaged<T> result)
    {
        if (result.IsSuccess)
        {
            return result;
        }

        throw new ResultException(result);
    }

    /// <summary>
    /// Throws an exception of type TException if the result is a failure.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TException">The type of exception to throw.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <returns>The current Result if it indicates success.</returns>
    /// <exception>Thrown if the result indicates a failure.
    ///     <cref>TException</cref>
    /// </exception>
    public static ResultPaged<T> ThrowIfFailed<T, TException>(this ResultPaged<T> result)
        where TException : Exception
    {
        if (result.IsSuccess)
        {
            return result;
        }

        throw ((TException)Activator.CreateInstance(typeof(TException), result.Errors.FirstOrDefault()?.Message, result))!;
    }

    /// <summary>
    /// Maps the page collection to a new type while preserving pagination information.
    /// </summary>
    /// <example>
    /// var userDtos = pagedUsers.Map(users => users.Select(u => new UserDto(u)));
    /// // Preserves page count, current page, and page size
    /// </example>
    public static ResultPaged<TNew> Map<T, TNew>(this ResultPaged<T> result, Func<IEnumerable<T>, IEnumerable<TNew>> mapper)
    {
        if (!result.IsSuccess || mapper is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newValues = mapper(result.Value);
            return ResultPaged<TNew>.Success(
                    newValues,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously maps the page collection while preserving pagination information.
    /// </summary>
    /// <example>
    /// var userDtos = await pagedUsers.MapAsync(
    ///     async users => await Task.WhenAll(
    ///         users.Select(u => MapToUserDtoAsync(u))
    ///     ),
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<TNew>> MapAsync<T, TNew>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, CancellationToken, Task<IEnumerable<TNew>>> mapper,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || mapper is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newValues = await mapper(result.Value, cancellationToken);
            return ResultPaged<TNew>.Success(
                    newValues,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Binds the page collection to a new paged result while preserving metadata.
    /// </summary>
    /// <example>
    /// var activeUsers = pagedUsers.Bind(users =>
    ///     users.Any()
    ///         ? ResultPaged{User}.Success(
    ///             users.Where(u => u.IsActive),
    ///             totalCount: activeCount,
    ///             page: currentPage,
    ///             pageSize: pageSize)
    ///         : ResultPaged{User}.Failure("No active users found")
    /// );
    /// </example>
    public static ResultPaged<TNew> Bind<T, TNew>(this ResultPaged<T> result, Func<IEnumerable<T>, ResultPaged<TNew>> binder)
    {
        if (!result.IsSuccess || binder is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newResult = binder(result.Value);
            return newResult
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously binds the page collection to a new paged result.
    /// </summary>
    /// <example>
    /// var activeUsers = await pagedUsers.BindAsync(async (users, ct) =>
    ///     users.Any()
    ///         ? await ValidateAndFilterUsersAsync(users, ct)
    ///         : ResultPaged{User}.Failure("No users to process"),
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<TNew>> BindAsync<T, TNew>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, CancellationToken, Task<ResultPaged<TNew>>> binder,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || binder is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newResult = await binder(result.Value, cancellationToken);
            return newResult
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Filters the page collection based on a predicate, preserving pagination metadata.
    /// </summary>
    /// <example>
    /// var adultUsers = pagedUsers.Filter(
    ///     users => users.All(u => u.Age >= 18),
    ///     new ValidationError("All users must be adults")
    /// );
    /// // Maintains the same page info even if some items are filtered
    /// </example>
    public static ResultPaged<T> Filter<T>(this ResultPaged<T> result, Func<IEnumerable<T>, bool> predicate, IResultError error = null)
    {
        if (!result.IsSuccess || predicate is null)
        {
            return result;
        }

        try
        {
            return predicate(result.Value)
                ? result
                : ResultPaged<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously filters the page collection based on a predicate.
    /// </summary>
    /// <example>
    /// var validUsers = await pagedUsers.FilterAsync(
    ///     async (users, ct) => await ValidateAllUsersAsync(users, ct),
    ///     new ValidationError("Not all users are valid"),
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<T>> FilterAsync<T>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || predicate is null)
        {
            return result;
        }

        try
        {
            return await predicate(result.Value, cancellationToken)
                ? result
                : ResultPaged<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Filters items in a paged collection using a predicate while maintaining pagination metadata.
    /// </summary>
    /// <param name="predicate">The filtering function that returns true for items to keep</param>
    /// <returns>A new ResultPaged containing only the filtered items</returns>
    /// <example>
    /// <code>
    /// // Filter active users while preserving paging
    /// var activeUsers = pagedUsers.Filter(user => user.Status == UserStatus.Active);
    ///
    /// // Filter using complex conditions
    /// var eligibleUsers = pagedUsers.Filter(
    ///     user => user.Age >= 18 && user.IsEmailVerified
    /// );
    /// </code>
    /// </example>
    /// <remarks>
    /// Preserves the original error/message state. Returns the original result if input
    /// is in error state or predicate is null.
    /// </remarks>
    public static ResultPaged<T> Filter<T>(this ResultPaged<T> result, Func<T, bool> predicate)
    {
        if (!result.IsSuccess || predicate is null)
        {
            return result;
        }

        try
        {
            var filteredValues = new List<T>();
            foreach (var item in result.Value)
            {
                if (predicate(item))
                {
                    filteredValues.Add(item);
                }
            }

            return ResultPaged<T>.Success(filteredValues)
                    .WithErrors(result.Errors)
                    .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously filters items in a paged collection while maintaining pagination metadata.
    /// </summary>
    /// <param name="predicate">The async filtering function that returns true for items to keep</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A Task containing the filtered ResultPaged</returns>
    /// <example>
    /// <code>
    /// // Validate users against external service
    /// var validUsers = await pagedUsers.FilterAsync(
    ///     async (user, ct) => await userValidator.IsValidAsync(user, ct),
    ///     cancellationToken
    /// );
    ///
    /// // Check user permissions
    /// var authorizedUsers = await pagedUsers.FilterAsync(
    ///     async (user, ct) => await permissionService.HasAccessAsync(user.Id, ct)
    /// );
    /// </code>
    /// </example>
    /// <remarks>
    /// Handles cancellation and preserves error/message state. Returns original result
    /// if input is in error state or predicate is null.
    /// </remarks>
    public static async Task<ResultPaged<T>> FilterAsync<T>(
        this ResultPaged<T> result,
        Func<T, CancellationToken, Task<bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || predicate is null)
        {
            return result;
        }

        try
        {
            var filteredValues = new List<T>();
            foreach (var item in result.Value)
            {
                if (await predicate(item, cancellationToken))
                {
                    filteredValues.Add(item);
                }
            }

            return ResultPaged<T>.Success(filteredValues)
                    .WithErrors(result.Errors)
                    .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Ensures a condition is met for the page collection.
    /// </summary>
    /// <example>
    /// var result = pagedUsers.Ensure(
    ///     users => users.Count() <= maxPageSize,
    ///     new ValidationError($"Page size cannot exceed {maxPageSize}")
    /// );
    /// </example>
    public static ResultPaged<T> Ensure<T>(this ResultPaged<T> result, Func<IEnumerable<T>, bool> predicate, IResultError error)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (predicate is null)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(result.Messages);
        }

        try
        {
            return predicate(result.Value)
                ? result
                : ResultPaged<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Ensure condition not met"))
                    .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously ensures a condition is met for the page collection.
    /// </summary>
    /// <example>
    /// var result = await pagedUsers.EnsureAsync(
    ///     async (users, ct) => await ValidatePageSizeAsync(users, ct),
    ///     new ValidationError("Invalid page configuration"),
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<T>> EnsureAsync<T>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (predicate is null)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(result.Messages);
        }

        try
        {
            return await predicate(result.Value, cancellationToken)
                ? result
                : ResultPaged<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Ensure condition not met"))
                    .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Converts to failure if the predicate returns true for the page collection.
    /// </summary>
    /// <example>
    /// var result = pagedUsers.Unless(
    ///     users => users.Any(u => u.IsBlocked),
    ///     new ValidationError("Page contains blocked users")
    /// );
    /// </example>
    public static ResultPaged<T> Unless<T>(this ResultPaged<T> result, Func<IEnumerable<T>, bool> predicate, IResultError error)
    {
        if (!result.IsSuccess || predicate is null)
        {
            return result;
        }

        try
        {
            return predicate(result.Value)
                ? ResultPaged<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(result.Messages)
                : result;
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously converts to failure if the predicate returns true.
    /// </summary>
    /// <example>
    /// var result = await pagedUsers.UnlessAsync(
    ///     async (users, ct) => await ContainsBlockedUsersAsync(users, ct),
    ///     new ValidationError("Page contains blocked users"),
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<T>> UnlessAsync<T>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || predicate is null)
        {
            return result;
        }

        try
        {
            return await predicate(result.Value, cancellationToken)
                ? ResultPaged<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(result.Messages)
                : result;
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Executes a side-effect on the page collection without changing the result.
    /// </summary>
    /// <example>
    /// var result = pagedUsers.Tap(users =>
    ///     _logger.LogInformation(
    ///         "Retrieved {Count} users on page {Page}/{TotalPages}",
    ///         users.Count(),
    ///         CurrentPage,
    ///         TotalPages
    ///     )
    /// );
    /// </example>
    public static ResultPaged<T> Tap<T>(this ResultPaged<T> result, Action<IEnumerable<T>> operation)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            operation(result.Value);
            return result;
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously executes a side-effect on the page collection.
    /// </summary>
    /// <example>
    /// var result = await pagedUsers.TapAsync(
    ///     async (users, ct) => await _cache.StorePageAsync(
    ///         users,
    ///         CurrentPage,
    ///         ct
    ///     ),
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<T>> TapAsync<T>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            await operation(result.Value, cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Logs a <see cref="ResultPaged{T}"/> using structured logging with default levels
    /// (Debug on success, Warning on failure).
    /// </summary>
    /// <typeparam name="T">The type of the paged items.</typeparam>
    /// <param name="result">The paged result instance to log.</param>
    /// <param name="logger">
    /// The logger to write to. If null, the method is a no-op and returns the original result.
    /// </param>
    /// <param name="messageTemplate">
    /// Optional message template for structured logging (e.g., "Fetched page {Page} of products").
    /// </param>
    /// <param name="args">
    /// Optional structured logging arguments corresponding to <paramref name="messageTemplate"/>.
    /// </param>
    /// <remarks>
    /// This method never alters the business outcome or throws.
    /// It emits consistent fields: LogKey, Page, PageSize, TotalCount, TotalPages, HasNextPage,
    /// HasPreviousPage, Messages (count), Errors (count), and ErrorTypes for failures.
    /// </remarks>
    /// <returns>The original <paramref name="result"/> unchanged.</returns>
    /// <example>
    /// <code>
    /// result.Log(logger, "Fetched {Entity} page {Page}", "Product", result.CurrentPage);
    /// </code>
    /// </example>
    public static ResultPaged<T> Log<T>(
        this ResultPaged<T> result,
        ILogger logger,
        string messageTemplate = null,
        params object[] args)
    {
        return result.Log(
            logger,
            messageTemplate,
            successLevel: LogLevel.Debug,
            failureLevel: LogLevel.Warning,
            args);
    }

    /// <summary>
    /// Logs a <see cref="ResultPaged{T}"/> using structured logging with custom levels.
    /// </summary>
    /// <typeparam name="T">The type of the paged items.</typeparam>
    /// <param name="result">The paged result instance to log.</param>
    /// <param name="logger">
    /// The logger to write to. If null, the method is a no-op and returns the original result.
    /// </param>
    /// <param name="messageTemplate">
    /// Optional message template for structured logging (e.g., "Searched {Entity} page {Page}").
    /// </param>
    /// <param name="successLevel">The log level used when the result indicates success.</param>
    /// <param name="failureLevel">The log level used when the result indicates failure.</param>
    /// <param name="args">
    /// Optional structured logging arguments corresponding to <paramref name="messageTemplate"/>.
    /// </param>
    /// <remarks>
    /// Logging exceptions are swallowed and do not affect the returned <see cref="ResultPaged{T}"/>.
    /// </remarks>
    /// <returns>The original <paramref name="result"/> unchanged.</returns>
    /// <example>
    /// <code>
    /// result.Log(logger,
    ///     "Queried {Entity} page {Page} (size {Size})",
    ///     LogLevel.Information,
    ///     LogLevel.Error,
    ///     "Order",
    ///     result.CurrentPage,
    ///     result.PageSize);
    /// </code>
    /// </example>
    public static ResultPaged<T> Log<T>(
        this ResultPaged<T> result,
        ILogger logger,
        string messageTemplate,
        LogLevel successLevel,
        LogLevel failureLevel,
        params object[] args)
    {
        if (logger is null)
            return result;

        try
        {
            var isSuccess = result.IsSuccess;
            var messagesCount = result.Messages?.Count ?? 0;
            var errorsCount = result.Errors?.Count ?? 0;
            var errorTypes = result.Errors?.Select(e => e.GetType().Name).ToArray() ?? [];

            var page = result.CurrentPage;
            var pageSize = result.PageSize;
            var total = result.TotalCount;
            var totalPages = result.TotalPages;
            var hasNext = result.HasNextPage;
            var hasPrev = result.HasPreviousPage;

            if (isSuccess)
            {
                if (!string.IsNullOrWhiteSpace(messageTemplate))
                {
                    logger.Log(
                        successLevel,
                        ResultLogEvent,
                        "{LogKey} Success - Page={Page} Size={Size} Total={Total} TotalPages={TotalPages} HasNext={HasNext} HasPrev={HasPrev} Messages={Messages} Errors={Errors} | " + messageTemplate,
                        "RES", page, pageSize, total, totalPages, hasNext, hasPrev, messagesCount, errorsCount, args);
                }
                else
                {
                    logger.Log(
                        successLevel,
                        ResultLogEvent,
                        "{LogKey} Success - Page={Page} Size={Size} Total={Total} TotalPages={TotalPages} HasNext={HasNext} HasPrev={HasPrev} Messages={Messages} Errors={Errors}",
                        "RES", page, pageSize, total, totalPages, hasNext, hasPrev, messagesCount, errorsCount);
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(messageTemplate))
                {
                    logger.Log(
                        failureLevel,
                        ResultLogEvent,
                        "{LogKey} Failure - Page={Page} Size={Size} Total={Total} TotalPages={TotalPages} HasNext={HasNext} HasPrev={HasPrev} Messages={Messages} Errors={Errors} ErrorTypes={ErrorTypes} | " + messageTemplate,
                        "RES", page, pageSize, total, totalPages, hasNext, hasPrev, messagesCount, errorsCount, errorTypes, args);
                }
                else
                {
                    logger.Log(
                        failureLevel,
                        ResultLogEvent,
                        "{LogKey} Failure - Page={Page} Size={Size} Total={Total} TotalPages={TotalPages} HasNext={HasNext} HasPrev={HasPrev} Messages={Messages} Errors={Errors} ErrorTypes={ErrorTypes}",
                        "RES", page, pageSize, total, totalPages, hasNext, hasPrev, messagesCount, errorsCount, errorTypes);
                }
            }

            return result;
        }
        catch
        {
            // Never alter business outcome due to logging issues.
            return result;
        }
    }

    /// <summary>
    /// Logs a ResultPaged&lt;T&gt; using structured logging with default levels
    /// (Debug on success, Warning on failure). The arguments for the message template are
    /// produced from the current paged result via <paramref name="argsFactory"/>.
    /// </summary>
    /// <typeparam name="T">The type of the paged items.</typeparam>
    /// <param name="result">The paged result instance to log.</param>
    /// <param name="logger">The logger to write to. If null, the method is a no-op.</param>
    /// <param name="messageTemplate">The structured message template to log.</param>
    /// <param name="argsFactory">Factory building arguments from the current paged result.</param>
    /// <returns>The original <paramref name="result"/> unchanged.</returns>
    public static ResultPaged<T> Log<T>(
        this ResultPaged<T> result,
        ILogger logger,
        string messageTemplate,
        Func<ResultPaged<T>, object[]> argsFactory)
    {
        return result.Log(
            logger,
            messageTemplate,
            argsFactory,
            successLevel: LogLevel.Debug,
            failureLevel: LogLevel.Warning);
    }

    /// <summary>
    /// Logs a ResultPaged&lt;T&gt; using structured logging with custom levels.
    /// The arguments for the message template are produced from the current paged result via
    /// <paramref name="argsFactory"/>.
    /// </summary>
    /// <typeparam name="T">The type of the paged items.</typeparam>
    /// <param name="result">The paged result instance to log.</param>
    /// <param name="logger">The logger to write to. If null, the method is a no-op.</param>
    /// <param name="messageTemplate">The structured message template to log.</param>
    /// <param name="argsFactory">Factory building arguments from the current paged result.</param>
    /// <param name="successLevel">Log level on success.</param>
    /// <param name="failureLevel">Log level on failure.</param>
    /// <returns>The original <paramref name="result"/> unchanged.</returns>
    public static ResultPaged<T> Log<T>(
        this ResultPaged<T> result,
        ILogger logger,
        string messageTemplate,
        Func<ResultPaged<T>, object[]> argsFactory,
        LogLevel successLevel,
        LogLevel failureLevel)
    {
        if (logger is null)
            return result;

        try
        {
            var isSuccess = result.IsSuccess;
            var messagesCount = result.Messages?.Count ?? 0;
            var errorsCount = result.Errors?.Count ?? 0;
            var errorTypes = result.Errors?.Select(e => e.GetType().Name).ToArray() ?? [];

            var page = result.CurrentPage;
            var pageSize = result.PageSize;
            var total = result.TotalCount;
            var totalPages = result.TotalPages;
            var hasNext = result.HasNextPage;
            var hasPrev = result.HasPreviousPage;

            var args = argsFactory?.Invoke(result) ?? [];

            if (!string.IsNullOrWhiteSpace(messageTemplate))
            {
                if (isSuccess)
                {
                    logger.Log(
                        successLevel,
                        ResultLogEvent,
                        "{LogKey} Success - Page={Page} Size={Size} Total={Total} TotalPages={TotalPages} HasNext={HasNext} HasPrev={HasPrev} Messages={Messages} Errors={Errors} | " + messageTemplate,
                        "RES", page, pageSize, total, totalPages, hasNext, hasPrev, messagesCount, errorsCount, args);
                }
                else
                {
                    logger.Log(
                        failureLevel,
                        ResultLogEvent,
                        "{LogKey} Failure - Page={Page} Size={Size} Total={Total} TotalPages={TotalPages} HasNext={HasNext} HasPrev={HasPrev} Messages={Messages} Errors={Errors} ErrorTypes={ErrorTypes} | " + messageTemplate,
                        "RES", page, pageSize, total, totalPages, hasNext, hasPrev, messagesCount, errorsCount, errorTypes, args);
                }
            }
            else
            {
                if (isSuccess)
                {
                    logger.Log(
                        successLevel,
                        ResultLogEvent,
                        "{LogKey} Success - Page={Page} Size={Size} Total={Total} TotalPages={TotalPages} HasNext={HasNext} HasPrev={HasPrev} Messages={Messages} Errors={Errors}",
                        "RES", page, pageSize, total, totalPages, hasNext, hasPrev, messagesCount, errorsCount);
                }
                else
                {
                    logger.Log(
                        failureLevel,
                        ResultLogEvent,
                        "{LogKey} Failure - Page={Page} Size={Size} Total={Total} TotalPages={TotalPages} HasNext={HasNext} HasPrev={HasPrev} Messages={Messages} Errors={Errors} ErrorTypes={ErrorTypes}",
                        "RES", page, pageSize, total, totalPages, hasNext, hasPrev, messagesCount, errorsCount, errorTypes);
                }
            }

            return result;
        }
        catch
        {
            return result;
        }
    }

    /// <summary>
    /// Maps the page collection while performing a side-effect on the transformed values.
    /// </summary>
    /// <example>
    /// var userDtos = pagedUsers.TeeMap(
    ///     users => users.Select(u => u.ToDto()),
    ///     dtos => _logger.LogInformation(
    ///         "Mapped {Count} users to DTOs on page {Page}",
    ///         dtos.Count(),
    ///         CurrentPage
    ///     )
    /// );
    /// </example>
    public static ResultPaged<TNew> TeeMap<T, TNew>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, IEnumerable<TNew>> mapper,
        Action<IEnumerable<TNew>> operation)
    {
        if (!result.IsSuccess || mapper is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newValues = mapper(result.Value);
            operation?.Invoke(newValues);
            return ResultPaged<TNew>.Success(
                    newValues,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously maps the page collection while performing a side-effect.
    /// </summary>
    /// <example>
    /// var userDtos = await pagedUsers.TeeMapAsync(
    ///     users => users.Select(u => u.ToDto()),
    ///     async (dtos, ct) => await _cache.StorePagedDtosAsync(dtos, ct),
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<TNew>> TeeMapAsync<T, TNew>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, IEnumerable<TNew>> mapper,
        Func<IEnumerable<TNew>, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || mapper is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newValues = mapper(result.Value);
            if (operation is not null)
            {
                await operation(newValues, cancellationToken);
            }
            return ResultPaged<TNew>.Success(
                    newValues,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Maps both success and failure cases simultaneously while preserving pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the source result value.</typeparam>
    /// <typeparam name="TNew">The type of the new result value.</typeparam>
    /// <param name="result">The source result to map.</param>
    /// <param name="onSuccess">Function to transform the collection if successful.</param>
    /// <param name="onFailure">Function to transform the errors if failed.</param>
    /// <returns>A new ResultPaged with either the transformed collection or transformed errors.</returns>
    /// <example>
    /// <code>
    /// var result = pagedUsers
    ///     .BiMap(
    ///         users => users.Select(u => new UserDto(u)),
    ///         errors => errors.Select(e => new PublicError(e.Message))
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<TNew> BiMap<T, TNew>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, IEnumerable<TNew>> onSuccess,
        Func<IReadOnlyList<IResultError>, IEnumerable<IResultError>> onFailure)
    {
        if (result.IsSuccess)
        {
            if (onSuccess is null)
            {
                return ResultPaged<TNew>.Failure()
                    .WithErrors(result.Errors)
                    .WithError(new Error("Success mapper is null"))
                    .WithMessages(result.Messages);
            }

            try
            {
                return ResultPaged<TNew>.Success(
                        onSuccess(result.Value),
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(result.Errors)
                    .WithMessages(result.Messages);
            }
            catch (Exception ex)
            {
                return ResultPaged<TNew>.Failure()
                    .WithErrors(result.Errors)
                    .WithError(Result.Settings.ExceptionErrorFactory(ex))
                    .WithMessages(result.Messages);
            }
        }

        if (onFailure is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(onFailure(result.Errors))
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Creates a new ResultPaged by choosing elements from the current page.
    /// Preserves paging metadata with adjusted counts.
    /// </summary>
    /// <example>
    /// var adultUsers = pagedUsers.Choose(users =>
    ///     users.Any(u => u.Age >= 18)
    ///         ? new ResultChooseOption<IEnumerable<User>>(users.Where(u => u.Age >= 18))
    ///         : ResultChooseOption<IEnumerable<User>>.None()
    /// );
    /// </example>
    public static ResultPaged<TNew> Choose<T, TNew>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, ResultChooseOption<IEnumerable<TNew>>> operation)
    {
        if (!result.IsSuccess || operation is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var option = operation(result.Value);

            if (!option.TryGetValue(out var values))
            {
                return ResultPaged<TNew>.Failure()
                    .WithErrors(result.Errors)
                    .WithError(new Error("No values were chosen"))
                    .WithMessages(result.Messages);
            }

            return ResultPaged<TNew>.Success(
                    values,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously creates a new ResultPaged by choosing elements from the current page.
    /// </summary>
    /// <example>
    /// var activeUsers = await pagedUsers.ChooseAsync(
    ///     async (users, ct) => {
    ///         var activeStatuses = await GetActiveStatusesAsync(users, ct);
    ///         var activeUsers = users.Where(u => activeStatuses[u.Id]);
    ///         return activeUsers.Any()
    ///             ? new ResultChooseOption<IEnumerable<User>>(activeUsers)
    ///             : ResultChooseOption<IEnumerable<User>>.None();
    ///     },
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<TNew>> ChooseAsync<T, TNew>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, CancellationToken, Task<ResultChooseOption<IEnumerable<TNew>>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var option = await operation(result.Value, cancellationToken);

            if (!option.TryGetValue(out var values))
            {
                return ResultPaged<TNew>.Failure()
                    .WithErrors(result.Errors)
                    .WithError(new Error("No values were chosen"))
                    .WithMessages(result.Messages);
            }

            return ResultPaged<TNew>.Success(
                    values,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    public static ResultPaged<TOutput> Collect<T, TOutput>(this ResultPaged<T> result, Func<T, Result<TOutput>> operation)
    {
        if (!result.IsSuccess || operation is null)
        {
            return ResultPaged<TOutput>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        var results = new List<TOutput>();
        var errors = new List<IResultError>();
        var messages = new List<string>();

        foreach (var item in result.Value)
        {
            try
            {
                var newResult = operation(item);
                if (newResult.IsSuccess)
                {
                    results.Add(newResult.Value);
                }
                else
                {
                    errors.AddRange(newResult.Errors);
                }
                messages.AddRange(newResult.Messages);
            }
            catch (Exception ex)
            {
                errors.Add(Result.Settings.ExceptionErrorFactory(ex));
            }
        }

        return errors.Any()
            ? ResultPaged<TOutput>.Failure()
                .WithErrors(errors)
                .WithMessages(messages)
            : ResultPaged<TOutput>.Success(
                    results,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(result.Errors)
                .WithMessages(messages);
    }

    /// <summary>
    /// Asynchronously applies an operation to each element while maintaining pagination.
    /// </summary>
    /// <example>
    /// var userDtos = await pagedUsers.CollectAsync(
    ///     async (user, ct) => await ValidateAndMapUserAsync(user, ct),
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<TOutput>> CollectAsync<T, TOutput>(
        this ResultPaged<T> result,
        Func<T, CancellationToken, Task<Result<TOutput>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return ResultPaged<TOutput>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        var results = new List<TOutput>();
        var errors = new List<IResultError>();
        var messages = new List<string>();

        foreach (var item in result.Value)
        {
            try
            {
                var newResult = await operation(item, cancellationToken);
                if (newResult.IsSuccess)
                {
                    results.Add(newResult.Value);
                }
                else
                {
                    errors.AddRange(newResult.Errors);
                }
                messages.AddRange(newResult.Messages);
            }
            catch (OperationCanceledException)
            {
                errors.Add(new OperationCancelledError());
                break;
            }
            catch (Exception ex)
            {
                errors.Add(Result.Settings.ExceptionErrorFactory(ex));
            }
        }

        return errors.Any()
            ? ResultPaged<TOutput>.Failure()
                .WithErrors(errors)
                .WithMessages(messages)
            : ResultPaged<TOutput>.Success(
                    results,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithMessages(messages);
    }

    /// <summary>
    /// Chains a new operation if the current ResultPaged is successful.
    /// </summary>
    /// <example>
    /// var result = pagedUsers
    ///     .AndThen(users => ValidatePageSize(users))
    ///     .AndThen(users => ProcessUserPage(users, CurrentPage));
    /// </example>
    public static ResultPaged<T> AndThen<T>(this ResultPaged<T> result, Func<IEnumerable<T>, ResultPaged<T>> operation)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            var newResult = operation(result.Value);
            return newResult
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously chains a new operation maintaining paging context.
    /// </summary>
    /// <example>
    /// var result = await pagedUsers
    ///     .AndThenAsync(
    ///         async (users, ct) => await ValidateAndProcessPageAsync(
    ///             users,
    ///             CurrentPage,
    ///             ct
    ///         ),
    ///         cancellationToken
    ///     );
    /// </example>
    public static async Task<ResultPaged<T>> AndThenAsync<T>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, CancellationToken, Task<ResultPaged<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            var newResult = await operation(result.Value, cancellationToken);
            return newResult
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Provides a fallback page collection in case of failure.
    /// </summary>
    /// <example>
    /// var users = pagedUsers.OrElse(() => {
    ///     var fallbackUsers = _cache.GetUserPage(page, pageSize);
    ///     return ResultPaged{User}.Success(
    ///         fallbackUsers,
    ///         _cache.GetTotalUserCount(),
    ///         page,
    ///         pageSize
    ///     );
    /// });
    /// </example>
    public static ResultPaged<T> OrElse<T>(this ResultPaged<T> result, Func<ResultPaged<T>> fallback)
    {
        if (result.IsSuccess || fallback is null)
        {
            return result;
        }

        try
        {
            var newResult = fallback();
            return newResult
                //.WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously provides a fallback page collection in case of failure.
    /// </summary>
    /// <example>
    /// var users = await pagedUsers.OrElseAsync(
    ///     async ct => await GetFallbackPageAsync(
    ///         CurrentPage,
    ///         PageSize,
    ///         ct
    ///     ),
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<T>> OrElseAsync<T>(
        this ResultPaged<T> result,
        Func<CancellationToken, Task<ResultPaged<T>>> fallback,
        CancellationToken cancellationToken = default)
    {
        if (result.IsSuccess || fallback is null)
        {
            return result;
        }

        try
        {
            var newResult = await fallback(cancellationToken);
            return newResult
                //.WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Validates each item in the page collection using FluentValidation.
    /// </summary>
    /// <example>
    /// public class UserValidator : AbstractValidator{User} {
    ///     public UserValidator() {
    ///         RuleFor(user => user.Age)
    ///             .GreaterThanOrEqualTo(18)
    ///             .WithMessage("Users must be at least 18 years old");
    ///     }
    /// }
    ///
    /// var result = pagedUsers.ValidateEach(new UserValidator());
    /// </example>
    public static ResultPaged<T> Validate<T>(
        this ResultPaged<T> result,
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (validator is null)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new Error("Validator cannot be null"))
                .WithMessages(result.Messages);
        }

        try
        {
            var errors = new List<IResultError>();
            foreach (var item in result.Value)
            {
                var validationResult = options is null
                    ? validator.Validate(item)
                    : validator.Validate(item, options);

                if (!validationResult.IsValid)
                {
                    errors.Add(new FluentValidationError(validationResult));
                }
            }

            if (errors.Any())
            {
                return ResultPaged<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithErrors(errors)
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages)
                .WithMessage("Validation failed due to an error");
        }
    }

    /// <summary>
    /// Asynchronously validates each item in the page collection.
    /// </summary>
    /// <example>
    /// var result = await pagedUsers.ValidateEachAsync(
    ///     new UserValidator(),
    ///     options => options
    ///         .IncludeRuleSets("BasicValidation")
    ///         .IncludeProperties(x => x.Email),
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<ResultPaged<T>> ValidateAsync<T>(
        this ResultPaged<T> result,
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (validator is null)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new Error("Validator cannot be null"))
                .WithMessages(result.Messages);
        }

        try
        {
            var errors = new List<IResultError>();
            foreach (var item in result.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var validationResult = options is null
                    ? await validator.ValidateAsync(item, cancellationToken)
                    : await validator.ValidateAsync(item, opt => options(opt), cancellationToken);

                if (!validationResult.IsValid)
                {
                    errors.Add(new FluentValidationError(validationResult));
                }
            }

            if (errors.Any())
            {
                return ResultPaged<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithErrors(errors)
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage("Validation failed due to an error")
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Executes different functions based on the result's success state.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The ResultPaged to match.</param>
    /// <param name="onSuccess">Function to execute if the Result is successful, receiving the values.</param>
    /// <param name="onFailure">Function to execute if the Result failed, receiving the errors.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount: 100, page: 1, pageSize: 10);
    ///
    /// var message = result.Match(
    ///     onSuccess: r => $"Found {r.TotalCount} users across {r.TotalPages} pages",
    ///     onFailure: errors => $"Failed with {errors.Count} errors"
    /// );
    /// </code>
    /// </example>
    public static TResult Match<T, TResult>(
        this ResultPaged<T> result,
        Func<ResultPaged<T>, TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess
            ? onSuccess(result)
            : onFailure(result.Errors);
    }

    /// <summary>
    /// Returns different values based on the result's success state.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The ResultPaged to match.</param>
    /// <param name="success">Value to return if successful.</param>
    /// <param name="failure">Value to return if failed.</param>
    /// <returns>Either the success or failure value.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount: 100, page: 1, pageSize: 10);
    ///
    /// var status = result.Match(
    ///     success: "Users retrieved successfully",
    ///     failure: "Failed to retrieve users"
    /// );
    /// </code>
    /// </example>
    public static TResult Match<T, TResult>(
        this ResultPaged<T> result,
        TResult success,
        TResult failure)
    {
        return result.IsSuccess ? success : failure;
    }

    /// <summary>
    /// Asynchronously executes different functions based on the result's success state.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The ResultPaged to match.</param>
    /// <param name="onSuccess">Async function to execute if successful, receiving the values.</param>
    /// <param name="onFailure">Async function to execute if failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount: 100, page: 1, pageSize: 10);
    ///
    /// var status = await result.MatchAsync(
    ///     async (r, ct) => await FormatSuccessMessageAsync(r, ct),
    ///     async (errors, ct) => await FormatErrorMessageAsync(errors, ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this ResultPaged<T> result,
        Func<ResultPaged<T>, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess
            ? await onSuccess(result, cancellationToken)
            : await onFailure(result.Errors, cancellationToken);
    }

    /// <summary>
    /// Executes different actions based on the ResultPaged's success state.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The ResultPaged to handle.</param>
    /// <param name="onSuccess">Action to execute if the Result is successful, receiving the values.</param>
    /// <param name="onFailure">Action to execute if the Result failed, receiving the errors.</param>
    /// <returns>The original ResultPaged instance.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount: 100, page: 1, pageSize: 10);
    ///
    /// result.Handle(
    ///     onSuccess: users => {
    ///         Console.WriteLine($"Processing {users.Count()} users from page {result.CurrentPage}");
    ///         foreach(var user in users) {
    ///             Console.WriteLine($"User: {user.Name}");
    ///         }
    ///     },
    ///     onFailure: errors => Console.WriteLine($"Failed with {errors.Count} errors")
    /// );
    /// </code>
    /// </example>
    public static ResultPaged<T> Handle<T>(
        this ResultPaged<T> result,
        Action<IEnumerable<T>> onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            onSuccess(result.Value);
            return result;
        }
        else
        {
            onFailure(result.Errors);
            return result;
        }
    }

    /// <summary>
    /// Asynchronously executes different actions based on the ResultPaged's success state.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The ResultPaged to handle.</param>
    /// <param name="onSuccess">Async function to execute if the Result is successful, receiving the values.</param>
    /// <param name="onFailure">Async function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original ResultPaged instance.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount: 100, page: 1, pageSize: 10);
    ///
    /// await result.HandleAsync(
    ///     async (users, ct) => {
    ///         foreach(var user in users) {
    ///             await LogUserAccessAsync(user, ct);
    ///         }
    ///     },
    ///     async (errors, ct) => await LogErrorsAsync(errors, ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> HandleAsync<T>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, CancellationToken, Task> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            await onSuccess(result.Value, cancellationToken);
            return result;
        }
        else
        {
            await onFailure(result.Errors, cancellationToken);
            return result;
        }
    }

    /// <summary>
    /// Asynchronously executes a success function with a synchronous failure handler.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The ResultPaged to handle.</param>
    /// <param name="onSuccess">Async function to execute if the Result is successful, receiving the values.</param>
    /// <param name="onFailure">Synchronous function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original ResultPaged instance.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount: 100, page: 1, pageSize: 10);
    ///
    /// await result.HandleAsync(
    ///     async (users, ct) => {
    ///         foreach(var user in users) {
    ///             await ProcessUserAsync(user, ct);
    ///         }
    ///     },
    ///     errors => Console.WriteLine($"Failed with {errors.Count} errors"),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> HandleAsync<T>(
        this ResultPaged<T> result,
        Func<IEnumerable<T>, CancellationToken, Task> onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            await onSuccess(result.Value, cancellationToken);
            return result;
        }
        else
        {
            onFailure(result.Errors);
            return result;
        }
    }

    /// <summary>
    /// Executes a synchronous success function with an async failure handler.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The ResultPaged to handle.</param>
    /// <param name="onSuccess">Synchronous function to execute if the Result is successful, receiving the values.</param>
    /// <param name="onFailure">Async function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original ResultPaged instance.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount: 100, page: 1, pageSize: 10);
    ///
    /// await result.HandleAsync(
    ///     users => {
    ///         foreach(var user in users) {
    ///             Console.WriteLine($"Processing user: {user.Name}");
    ///         }
    ///     },
    ///     async (errors, ct) => await LogErrorsAsync(errors, ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> HandleAsync<T>(
        this ResultPaged<T> result,
        Action<IEnumerable<T>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            onSuccess(result.Value);
            return result;
        }
        else
        {
            await onFailure(result.Errors, cancellationToken);
            return result;
        }
    }
}