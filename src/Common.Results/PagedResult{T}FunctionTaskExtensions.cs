// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;
using FluentValidation.Internal;

/// <summary>
///     Extension methods for Task<PagedPagedResult<T>> to enable proper chaining.
/// </summary>
public static partial class PagedResultFunctionTaskExtensions
{
    /// <summary>
    /// Maps the successful value of a PagedResult task using the provided mapping function.
    /// Preserves pagination metadata in the transformed result.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The PagedResult task to map.</param>
    /// <param name="mapper">The function to map the collection.</param>
    /// <returns>A new PagedResult containing the mapped collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Transform a page of users to DTOs
    /// var userDtos = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Map(users => users.Select(user => new UserDto(user.Id, user.Name)));
    /// </code>
    /// </example>
    public static async Task<PagedResult<TNew>> Map<T, TNew>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, IEnumerable<TNew>> mapper)
    {
        if (mapper is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Map(mapper);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Maps the successful value of a PagedResult task using the provided asynchronous mapping function.
    /// Preserves pagination metadata in the transformed result.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The PagedResult task to map.</param>
    /// <param name="mapper">The async function to map the collection.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new PagedResult containing the mapped collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Transform a page of users to DTOs with async enrichment
    /// var enrichedDtos = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .MapAsync(async (users, ct) =>
    ///     {
    ///         var preferences = await LoadUserPreferencesAsync(users.Select(u => u.Id), ct);
    ///         return users.Select(u => new UserDto(u, preferences[u.Id]));
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<PagedResult<TNew>> MapAsync<T, TNew>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<IEnumerable<TNew>>> mapper,
        CancellationToken cancellationToken = default)
    {
        if (mapper is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.MapAsync(mapper, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Maps the page collection while performing a side-effect on the transformed values.
    /// Preserves pagination metadata in the transformed result.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The PagedResult task to map.</param>
    /// <param name="mapper">The function to map the collection.</param>
    /// <param name="action">The action to perform on the mapped collection.</param>
    /// <returns>A new PagedResult containing the mapped collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Transform users to DTOs and log the transformation
    /// var userDtos = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .TeeMap(
    ///         users => users.Select(u => new UserDto(u.Id, u.Name)),
    ///         dtos => logger.LogInformation($"Transformed {dtos.Count()} users to DTOs")
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<TNew>> TeeMap<T, TNew>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, IEnumerable<TNew>> mapper,
        Action<IEnumerable<TNew>> action)
    {
        if (mapper is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.TeeMap(mapper, action);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Maps the page collection while performing an asynchronous side-effect on the transformed values.
    /// Preserves pagination metadata in the transformed result.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The PagedResult task to map.</param>
    /// <param name="mapper">The function to map the collection.</param>
    /// <param name="action">The async action to perform on the mapped collection.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new PagedResult containing the mapped collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Transform users to DTOs and cache the results
    /// var userDtos = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .TeeMapAsync(
    ///         users => users.Select(u => new UserDto(u.Id, u.Name)),
    ///         async (dtos, ct) => await cacheService.StorePageAsync(dtos, pageNumber, ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<TNew>> TeeMapAsync<T, TNew>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, IEnumerable<TNew>> mapper,
        Func<IEnumerable<TNew>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (mapper is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.TeeMapAsync(mapper, action, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Maps both success and failure cases of a PagedResult task simultaneously while preserving pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The PagedResult task to transform.</param>
    /// <param name="onSuccess">Function to transform the collection if successful.</param>
    /// <param name="onFailure">Function to transform the errors if failed.</param>
    /// <returns>A new PagedResult with either the transformed collection or transformed errors.</returns>
    /// <example>
    /// <code>
    /// // Transform users to DTOs with custom error handling
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .BiMap(
    ///         users => users.Select(u => new UserDto(u.Id, u.Name)),
    ///         errors => errors.Select(e => new PublicError(
    ///             code: "USER.FETCH_ERROR",
    ///             message: e.Message
    ///         ))
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<TNew>> BiMap<T, TNew>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, IEnumerable<TNew>> onSuccess,
        Func<IReadOnlyList<IResultError>, IEnumerable<IResultError>> onFailure)
    {
        if (onSuccess is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Success mapper cannot be null"));
        }

        if (onFailure is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Failure mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.BiMap(onSuccess, onFailure);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously maps both success and failure cases of a PagedResult task while preserving pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The PagedResult task to transform.</param>
    /// <param name="onSuccess">Async function to transform the collection if successful.</param>
    /// <param name="onFailure">Async function to transform the errors if failed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new PagedResult with either the transformed collection or transformed errors.</returns>
    /// <example>
    /// <code>
    /// // Transform users to DTOs with async error processing
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .BiMapAsync(
    ///         async (users, ct) => await TransformToDtosAsync(users, ct),
    ///         async (errors, ct) => await ProcessErrorsAsync(errors, ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<TNew>> BiMapAsync<T, TNew>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<IEnumerable<TNew>>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<IEnumerable<IResultError>>> onFailure,
        CancellationToken cancellationToken = default)
    {
        if (onSuccess is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Success mapper cannot be null"));
        }

        if (onFailure is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Failure mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            if (result.IsSuccess)
            {
                var transformedValues = await onSuccess(result.Value, cancellationToken);

                return PagedResult<TNew>.Success(
                        transformedValues,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithMessages(result.Messages);
            }

            var transformedErrors = await onFailure(result.Errors, cancellationToken);

            return PagedResult<TNew>.Failure()
                .WithErrors(transformedErrors)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Binds the successful value of a PagedResult task to another PagedResult using the provided binding function.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new PagedResult value.</typeparam>
    /// <param name="resultTask">The PagedResult task to bind.</param>
    /// <param name="binder">The function to bind the collection to a new PagedResult.</param>
    /// <returns>The bound PagedResult or a failure containing the original errors.</returns>
    /// <example>
    /// <code>
    /// // Filter active users and update pagination metadata
    /// var activeUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Bind(users =>
    ///     {
    ///         var active = users.Where(u => u.IsActive).ToList();
    ///         return active.Any()
    ///             ? PagedResult{User}.Success(
    ///                 active,
    ///                 totalActiveUsers,
    ///                 pageNumber,
    ///                 pageSize)
    ///             : PagedResult{User}.Failure("No active users found");
    ///     });
    /// </code>
    /// </example>
    public static async Task<PagedResult<TNew>> Bind<T, TNew>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, PagedResult<TNew>> binder)
    {
        if (binder is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Binder cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Bind(binder);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Binds the successful value of a PagedResult task to another PagedResult using the provided async binding function.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new PagedResult value.</typeparam>
    /// <param name="resultTask">The PagedResult task to bind.</param>
    /// <param name="binder">The async function to bind the collection to a new PagedResult.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The bound PagedResult or a failure containing the original errors.</returns>
    /// <example>
    /// <code>
    /// // Validate and enrich users asynchronously
    /// var enrichedUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .BindAsync(async (users, ct) =>
    ///     {
    ///         var validationResults = await ValidateUsersAsync(users, ct);
    ///         if (validationResults.HasErrors)
    ///             return PagedResult{User}.Failure("Validation failed")
    ///                 .WithErrors(validationResults.Errors);
    ///
    ///         var enriched = await EnrichUsersAsync(users, ct);
    ///         return PagedResult{User}.Success(
    ///             enriched,
    ///             totalCount,
    ///             pageNumber,
    ///             pageSize);
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<PagedResult<TNew>> BindAsync<T, TNew>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<PagedResult<TNew>>> binder,
        CancellationToken cancellationToken = default)
    {
        if (binder is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Binder cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.BindAsync(binder, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Chains a new operation on a successful PagedResult task, preserving pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task to chain from.</param>
    /// <param name="operation">The operation to execute if successful.</param>
    /// <returns>A new PagedResult with the operation result or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Process users with multiple validations
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .AndThen(users => ValidateUserPermissions(users))
    ///     .AndThen(users => EnsureUserQuota(users))
    ///     .AndThen(users => UpdateUserStatus(users));
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> AndThen<T>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, PagedResult<T>> operation)
    {
        if (operation is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.AndThen(operation);
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Chains a new async operation on a successful PagedResult task, preserving pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task to chain from.</param>
    /// <param name="operation">The async operation to execute if successful.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new PagedResult with the operation result or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Process users with async validations and updates
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .AndThenAsync(
    ///         async (users, ct) =>
    ///         {
    ///             var validationResult = await ValidateUsersAsync(users, ct);
    ///             return validationResult.IsValid
    ///                 ? PagedResult{User}.Success(users)
    ///                 : PagedResult{User}.Failure("Validation failed")
    ///                     .WithErrors(validationResult.Errors);
    ///         },
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> AndThenAsync<T>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<PagedResult<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.AndThenAsync(operation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Provides a fallback PagedResult if the task fails, maintaining pagination context where possible.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task.</param>
    /// <param name="fallback">The function providing the fallback PagedResult.</param>
    /// <returns>The original PagedResult if successful; otherwise, the fallback result.</returns>
    /// <example>
    /// <code>
    /// // Attempt to get users from cache with database fallback
    /// var users = await GetPagedUsersFromCacheAsync(pageNumber, pageSize)
    ///     .OrElse(() =>
    ///     {
    ///         logger.LogWarning("Cache miss, fetching from database");
    ///         return GetPagedUsersFromDatabaseAsync(pageNumber, pageSize);
    ///     });
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> OrElse<T>(
        this Task<PagedResult<T>> resultTask,
        Func<PagedResult<T>> fallback)
    {
        if (fallback is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Fallback cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.OrElse(fallback);
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Provides an async fallback PagedResult if the task fails, maintaining pagination context where possible.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task.</param>
    /// <param name="fallback">The async function providing the fallback PagedResult.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original PagedResult if successful; otherwise, the fallback result.</returns>
    /// <example>
    /// <code>
    /// // Try primary database with fallback to secondary
    /// var users = await GetPagedUsersFromPrimaryAsync(pageNumber, pageSize)
    ///     .OrElseAsync(
    ///         async ct =>
    ///         {
    ///             logger.LogWarning("Primary database unavailable");
    ///             await NotifyOperationsTeamAsync(ct);
    ///             return await GetPagedUsersFromSecondaryAsync(pageNumber, pageSize, ct);
    ///         },
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> OrElseAsync<T>(
        this Task<PagedResult<T>> resultTask,
        Func<CancellationToken, Task<PagedResult<T>>> fallback,
        CancellationToken cancellationToken = default)
    {
        if (fallback is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Fallback cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.OrElseAsync(fallback, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Filters a PagedResult task based on a predicate, maintaining pagination metadata.
    /// Returns failure if the predicate isn't satisfied.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task to filter.</param>
    /// <param name="predicate">The condition that must be true for the entire page.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original PagedResult if predicate is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// // Ensure all users in the page are active
    /// var activeUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Filter(
    ///         users => users.All(u => u.IsActive),
    ///         new ValidationError("Page contains inactive users")
    ///     );
    ///
    /// // Ensure page size is within limits
    /// var validPage = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Filter(
    ///         users => users.Count() <= maxPageSize,
    ///         new ValidationError($"Page size exceeds maximum of {maxPageSize}")
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> Filter<T>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, bool> predicate,
        IResultError error)
    {
        if (predicate is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Filter(predicate, error);
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Filters a PagedResult task based on an async predicate, maintaining pagination metadata.
    /// Returns failure if the predicate isn't satisfied.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task to filter.</param>
    /// <param name="predicate">The async condition that must be true for the entire page.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original PagedResult if predicate is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// // Ensure all users have valid subscriptions
    /// var validUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .FilterAsync(
    ///         async (users, ct) =>
    ///         {
    ///             var subscriptions = await GetSubscriptionsAsync(
    ///                 users.Select(u => u.Id),
    ///                 ct);
    ///             return users.All(u =>
    ///                 subscriptions.TryGetValue(u.Id, out var sub)
    ///                 && sub.IsValid);
    ///         },
    ///         new ValidationError("Some users have invalid subscriptions"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> FilterAsync<T>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.FilterAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Ensures a condition is met for the paged collection, maintaining pagination metadata.
    /// Similar to Filter but with more semantic meaning for validation scenarios.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task to check.</param>
    /// <param name="predicate">The condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original PagedResult if condition is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// // Validate page consistency
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Ensure(
    ///         users => users.Count() == pageSize ||
    ///                 (pageNumber * pageSize >= totalCount),
    ///         new ValidationError("Incomplete page detected")
    ///     )
    ///     .Ensure(
    ///         users => users.Select(u => u.Department).Distinct().Count() == 1,
    ///         new ValidationError("Users from multiple departments in single page")
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> Ensure<T>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, bool> predicate,
        IResultError error)
    {
        if (predicate is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Ensure(predicate, error);
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously ensures a condition is met for the paged collection.
    /// Similar to FilterAsync but with more semantic meaning for validation scenarios.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task to check.</param>
    /// <param name="predicate">The async condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original PagedResult if condition is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// // Validate page with external service checks
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .EnsureAsync(
    ///         async (users, ct) =>
    ///         {
    ///             var permissions = await permissionService
    ///                 .ValidateUsersAsync(users, ct);
    ///             var quotas = await quotaService
    ///                 .CheckQuotasAsync(users, ct);
    ///
    ///             return permissions.AllValid && quotas.AllWithinLimits;
    ///         },
    ///         new ValidationError("Users failed security validation"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> EnsureAsync<T>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.EnsureAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Converts a successful PagedResult to a failure if a condition is met.
    /// Useful for negative validation scenarios while maintaining pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task to check.</param>
    /// <param name="predicate">The condition that must be false.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <returns>The original PagedResult if condition is false; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// // Ensure page doesn't contain blocked users
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Unless(
    ///         users => users.Any(u => u.IsBlocked),
    ///         new ValidationError("Page contains blocked users")
    ///     );
    ///
    /// // Ensure no duplicate emails in page
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Unless(
    ///         users => users.Select(u => u.Email)
    ///             .Distinct()
    ///             .Count() != users.Count(),
    ///         new ValidationError("Duplicate email addresses detected")
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> Unless<T>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, bool> predicate,
        IResultError error)
    {
        if (predicate is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Unless(predicate, error);
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously converts a successful PagedResult to a failure if a condition is met.
    /// Useful for negative validation scenarios while maintaining pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task to check.</param>
    /// <param name="predicate">The async condition that must be false.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original PagedResult if condition is false; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// // Ensure no blacklisted users in page
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .UnlessAsync(
    ///         async (users, ct) =>
    ///         {
    ///             var blacklist = await securityService
    ///                 .GetBlacklistedUsersAsync(users.Select(u => u.Id), ct);
    ///             return users.Any(u => blacklist.Contains(u.Id));
    ///         },
    ///         new ValidationError("Page contains blacklisted users"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> UnlessAsync<T>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.UnlessAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Validates each item in a PagedResult task using FluentValidation.
    /// Maintains pagination metadata while accumulating validation errors.
    /// </summary>
    /// <typeparam name="T">The type of items to validate.</typeparam>
    /// <param name="resultTask">The PagedResult task containing items to validate.</param>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Optional validation strategy options to apply.</param>
    /// <returns>The original PagedResult if all validations succeed; otherwise, a failure with validation errors.</returns>
    /// <example>
    /// <code>
    /// public class UserValidator : AbstractValidator{User}
    /// {
    ///     public UserValidator()
    ///     {
    ///         RuleFor(u => u.Email)
    ///             .NotEmpty()
    ///             .EmailAddress()
    ///             .WithMessage("Invalid email format");
    ///
    ///         RuleFor(u => u.Age)
    ///             .GreaterThanOrEqualTo(18)
    ///             .WithMessage("User must be at least 18 years old");
    ///
    ///         RuleFor(u => u.Department)
    ///             .NotEmpty()
    ///             .Must(d => validDepartments.Contains(d))
    ///             .WithMessage("Invalid department");
    ///     }
    /// }
    ///
    /// // Validate all users in the page
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Validate(
    ///         new UserValidator(),
    ///         options => options
    ///             .IncludeRuleSets("BasicValidation")
    ///             .ThrowOnFailures(false)
    ///     );
    ///
    /// if (!result.IsSuccess)
    /// {
    ///     // Handle validation errors, grouped by property
    ///     var errorsByProperty = result.Errors
    ///         .OfType{ValidationError}()
    ///         .GroupBy(e => e.PropertyName);
    /// }
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> Validate<T>(
        this Task<PagedResult<T>> resultTask,
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null)
    {
        if (validator is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Validator cannot be null"));
        }

        try
        {
            var result = await resultTask;

            if (!result.IsSuccess)
            {
                return result;
            }

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
                return PagedResult<T>.Failure(result.Value)
                    .WithErrors(errors)
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage("Validation failed due to an error");
        }
    }

    /// <summary>
    /// Asynchronously validates each item in a PagedResult task using FluentValidation.
    /// Maintains pagination metadata while accumulating validation errors.
    /// </summary>
    /// <typeparam name="T">The type of items to validate.</typeparam>
    /// <param name="resultTask">The PagedResult task containing items to validate.</param>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Optional validation strategy options to apply.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original PagedResult if all validations succeed; otherwise, a failure with validation errors.</returns>
    /// <example>
    /// <code>
    /// public class UserValidator : AbstractValidator{User}
    /// {
    ///     public UserValidator(IUserService userService)
    ///     {
    ///         RuleFor(u => u.Username)
    ///             .NotEmpty()
    ///             .MustAsync(async (username, ct) =>
    ///                 await userService.IsUsernameUniqueAsync(username, ct))
    ///             .WithMessage("Username must be unique");
    ///
    ///         RuleFor(u => u.Email)
    ///             .NotEmpty()
    ///             .EmailAddress()
    ///             .MustAsync(async (email, ct) =>
    ///                 await userService.IsEmailVerifiedAsync(email, ct))
    ///             .WithMessage("Email must be verified");
    ///     }
    /// }
    ///
    /// // Validate users with async rules
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .ValidateAsync(
    ///         new UserValidator(userService),
    ///         options => options
    ///             .IncludeRuleSets("RegistrationRules")
    ///             .IncludeProperties(u => new { u.Username, u.Email }),
    ///         cancellationToken
    ///     );
    ///
    /// if (!result.IsSuccess)
    /// {
    ///     // Handle validation errors with property context
    ///     var emailErrors = result.Errors
    ///         .OfType{ValidationError}()
    ///         .Where(e => e.PropertyName == nameof(User.Email))
    ///         .ToList();
    /// }
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> ValidateAsync<T>(
        this Task<PagedResult<T>> resultTask,
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null,
        CancellationToken cancellationToken = default)
    {
        if (validator is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Validator cannot be null"));
        }

        try
        {
            var result = await resultTask;

            if (!result.IsSuccess)
            {
                return result;
            }

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
                return PagedResult<T>.Failure(result.Value)
                    .WithErrors(errors)
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return PagedResult<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage("Validation failed due to an error");
        }
    }

    /// <summary>
    /// Executes a side-effect on the successful page collection without changing the result.
    /// Useful for logging, metrics, and other non-transformative operations.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task to tap into.</param>
    /// <param name="action">The action to execute with the page collection.</param>
    /// <returns>The original PagedResult unchanged.</returns>
    /// <example>
    /// <code>
    /// // Log page metrics and cache performance data
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Tap(users =>
    ///     {
    ///         logger.LogInformation(
    ///             "Retrieved page {Page} with {Count} users (Total: {Total})",
    ///             pageNumber,
    ///             users.Count(),
    ///             totalCount);
    ///
    ///         metrics.RecordPageSize(users.Count());
    ///         metrics.RecordPageLoadTime(stopwatch.Elapsed);
    ///     });
    ///
    /// // Monitor specific conditions
    /// var activeUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Tap(users =>
    ///     {
    ///         var inactiveCount = users.Count(u => !u.IsActive);
    ///         if (inactiveCount > 0)
    ///         {
    ///             alerts.Warn(
    ///                 "Page {Page} contains {Count} inactive users",
    ///                 pageNumber,
    ///                 inactiveCount);
    ///         }
    ///     });
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> Tap<T>(
        this Task<PagedResult<T>> resultTask,
        Action<IEnumerable<T>> action)
    {
        if (action is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Tap(action);
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Executes an async side-effect on the successful page collection without changing the result.
    /// Useful for logging, caching, and other async non-transformative operations.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task to tap into.</param>
    /// <param name="action">The async action to execute with the page collection.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original PagedResult unchanged.</returns>
    /// <example>
    /// <code>
    /// // Cache page results and update metrics
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .TapAsync(async (users, ct) =>
    ///     {
    ///         // Cache the page results
    ///         await cache.StorePageAsync(
    ///             cacheKey: $"users:page:{pageNumber}",
    ///             value: users,
    ///             expiry: TimeSpan.FromMinutes(5),
    ///             ct);
    ///
    ///         // Update user statistics
    ///         await metrics.UpdateUserStatsAsync(
    ///             new PageMetrics(
    ///                 pageSize: users.Count(),
    ///                 activeUsers: users.Count(u => u.IsActive),
    ///                 premiumUsers: users.Count(u => u.IsPremium)),
    ///             ct);
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> TapAsync<T>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.TapAsync(action, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Executes a side-effect independent of the page collection without changing the result.
    /// Useful for general operations that don't need access to the page data.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The original PagedResult unchanged.</returns>
    /// <example>
    /// <code>
    /// // Track page requests and update system metrics
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Do(() =>
    ///     {
    ///         metrics.IncrementCounter("user.page.requests");
    ///         diagnostics.RecordPageAccess(DateTime.UtcNow);
    ///     });
    ///
    /// // Monitor system resources during paging
    /// var users = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Do(() =>
    ///     {
    ///         var memoryUsage = GC.GetTotalMemory(false);
    ///         if (memoryUsage > warningThreshold)
    ///         {
    ///             logger.LogWarning(
    ///                 "High memory usage during page retrieval: {Memory}MB",
    ///                 memoryUsage / 1024 / 1024);
    ///         }
    ///     });
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> Do<T>(
        this Task<PagedResult<T>> resultTask,
        Action action)
    {
        if (action is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;
            action();

            return result;
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Executes an async side-effect independent of the page collection without changing the result.
    /// Useful for general async operations that don't need access to the page data.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The PagedResult task.</param>
    /// <param name="action">The async action to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original PagedResult unchanged.</returns>
    /// <example>
    /// <code>
    /// // Update external systems and monitoring
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .DoAsync(async ct =>
    ///     {
    ///         // Update system health metrics
    ///         await metrics.UpdateHealthCheckAsync(
    ///             new HealthStatus(
    ///                 component: "UserPaging",
    ///                 status: "OK",
    ///                 timestamp: DateTime.UtcNow),
    ///             ct);
    ///
    ///         // Notify monitoring system
    ///         await monitoring.RecordPageAccessAsync(
    ///             new PageAccess(
    ///                 page: pageNumber,
    ///                 timestamp: DateTime.UtcNow,
    ///                 requestId: currentContext.RequestId),
    ///             ct);
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> DoAsync<T>(
        this Task<PagedResult<T>> resultTask,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;
            await action(cancellationToken);

            return result;
        }
        catch (OperationCanceledException)
        {
            return PagedResult<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Attempts to perform a synchronous operation on a paged collection, returning a PagedResult in a Task.
    /// This wrapper is useful for integrating synchronous `Try` operations into asynchronous workflows.
    /// </summary>
    /// <typeparam name="T">The type of items in the paged result.</typeparam>
    /// <param name="operation">The operation to execute, which returns a collection and a total count.</param>
    /// <returns>A Task-wrapped PagedResult containing the operation's result or failure information.</returns>
    /// <example>
    /// <code>
    /// var result =await GetPagedUsersAsync(pageNumber, pageSize).Try(() =>
    /// {
    ///     var users = _repository.GetPagedUsers(page, pageSize);
    ///     return (users, totalCount: _repository.GetTotalCount());
    /// });
    /// </code>
    /// </example>
    public static Task<PagedResult<T>> Try<T>(
        Func<(IEnumerable<T> Values, long TotalCount)> operation)
    {
        if (operation is null)
        {
            return Task.FromResult(PagedResult<T>.Failure()
                .WithError(new Error("Operation cannot be null")));
        }

        try
        {
            var (values, totalCount) = operation();

            return Task.FromResult(PagedResult<T>.Success(values, totalCount));
        }
        catch (Exception ex)
        {
            return Task.FromResult(PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message));
        }
    }

    /// <summary>
    /// Attempts to perform an asynchronous operation that returns a paged collection, wrapping the result in a PagedResult.
    /// Use this method to handle potentially failing async operations and maintain consistent paging metadata.
    /// </summary>
    /// <typeparam name="T">The type of items in the paged result.</typeparam>
    /// <param name="operation">An asynchronous operation that returns a collection and total count.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A Task-wrapped PagedResult containing the result or failure information.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize).TryAsync(async ct =>
    /// {
    ///     var users = await _repository.GetPagedUsersAsync(page, pageSize, ct);
    ///     var count = await _repository.GetTotalCountAsync(ct);
    ///     return (users, count);
    /// }, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<PagedResult<T>> TryAsync<T>(
        Func<CancellationToken, Task<(IEnumerable<T> Values, long TotalCount)>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return PagedResult<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var (values, totalCount) = await operation(cancellationToken);

            return PagedResult<T>.Success(values, totalCount);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<T>.Failure()
                .WithError(new OperationCancelledError())
                .WithMessage("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return PagedResult<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Selectively transforms a page collection using an option-based approach.
    /// Useful for conditional transformations where some items might be filtered out.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new value.</typeparam>
    /// <param name="resultTask">The PagedResult task to transform.</param>
    /// <param name="chooser">Function that returns Some for items to keep, None for items to filter out.</param>
    /// <returns>A new PagedResult containing the chosen values or a failure.</returns>
    /// <example>
    /// <code>
    /// // Filter and transform active premium users
    /// var premiumUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Choose(users =>
    ///     {
    ///         var premium = users
    ///             .Where(u => u.IsActive && u.IsPremium)
    ///             .Select(u => new PremiumUserDto(u));
    ///
    ///         return premium.Any()
    ///             ? ResultChooseOption{IEnumerable{PremiumUserDto}}.Some(premium)
    ///             : ResultChooseOption{IEnumerable{PremiumUserDto}}.None();
    ///     });
    ///
    /// // Transform users with specific roles
    /// var adminUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Choose(users =>
    ///     {
    ///         if (!users.Any(u => u.Role == UserRole.Admin))
    ///             return ResultChooseOption{IEnumerable{AdminDto}}.None();
    ///
    ///         var admins = users
    ///             .Where(u => u.Role == UserRole.Admin)
    ///             .Select(u => new AdminDto(
    ///                 u.Id,
    ///                 u.Name,
    ///                 u.Permissions));
    ///
    ///         return ResultChooseOption{IEnumerable{AdminDto}}.Some(admins);
    ///     });
    /// </code>
    /// </example>
    public static async Task<PagedResult<TNew>> Choose<T, TNew>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, ResultChooseOption<IEnumerable<TNew>>> chooser)
    {
        if (chooser is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Chooser cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Choose(chooser);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously selectively transforms a page collection using an option-based approach.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new value.</typeparam>
    /// <param name="resultTask">The PagedResult task to transform.</param>
    /// <param name="chooser">Async function that returns Some for items to keep, None for items to filter out.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new PagedResult containing the chosen values or a failure.</returns>
    /// <example>
    /// <code>
    /// // Filter and transform users based on subscription status
    /// var activeSubscribers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .ChooseAsync(async (users, ct) =>
    ///     {
    ///         var subscriptions = await subscriptionService
    ///             .GetSubscriptionsAsync(
    ///                 users.Select(u => u.Id),
    ///                 ct);
    ///
    ///         var activeUsers = users
    ///             .Where(u => subscriptions
    ///                 .TryGetValue(u.Id, out var sub) && sub.IsActive)
    ///             .Select(u => new SubscriberDto(u, subscriptions[u.Id]));
    ///
    ///         return activeUsers.Any()
    ///             ? ResultChooseOption{IEnumerable{SubscriberDto}}.Some(activeUsers)
    ///             : ResultChooseOption{IEnumerable{SubscriberDto}}.None();
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<PagedResult<TNew>> ChooseAsync<T, TNew>(
        this Task<PagedResult<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<ResultChooseOption<IEnumerable<TNew>>>> chooser,
        CancellationToken cancellationToken = default)
    {
        if (chooser is null)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new Error("Chooser cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.ChooseAsync(chooser, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Applies an operation to each item in a page collection while maintaining error context.
    /// Useful for scenarios where individual item operations might fail independently.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TOutput">The type of the output value.</typeparam>
    /// <param name="resultTask">The PagedResult task to process.</param>
    /// <param name="operation">The operation to apply to each item.</param>
    /// <returns>A new PagedResult containing successful results or aggregated errors.</returns>
    /// <example>
    /// <code>
    /// // Process users with individual error handling
    /// var processedUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Collect(user =>
    ///     {
    ///         try
    ///         {
    ///             var processed = userProcessor.Process(user);
    ///             return Result{ProcessedUser}.Success(processed);
    ///         }
    ///         catch (QuotaExceededException)
    ///         {
    ///             return Result{ProcessedUser}.Failure()
    ///                 .WithError(new ValidationError(
    ///                     $"Quota exceeded for user {user.Id}",
    ///                     nameof(user.Quota)));
    ///         }
    ///         catch (Exception ex)
    ///         {
    ///             return Result{ProcessedUser}.Failure()
    ///                 .WithError(new Error($"Processing failed: {ex.Message}"));
    ///         }
    ///     });
    /// </code>
    /// </example>
    public static async Task<PagedResult<TOutput>> Collect<T, TOutput>(
        this Task<PagedResult<T>> resultTask,
        Func<T, Result<TOutput>> operation)
    {
        if (operation is null)
        {
            return PagedResult<TOutput>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Collect(operation);
        }
        catch (Exception ex)
        {
            return PagedResult<TOutput>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously applies an operation to each item in a page collection while maintaining error context.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TOutput">The type of the output value.</typeparam>
    /// <param name="resultTask">The PagedResult task to process.</param>
    /// <param name="operation">The async operation to apply to each item.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new PagedResult containing successful results or aggregated errors.</returns>
    /// <example>
    /// <code>
    /// // Process users with external service integration
    /// var processedUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .CollectAsync(async (user, ct) =>
    ///     {
    ///         try
    ///         {
    ///             // Verify user permissions
    ///             var permissions = await permissionService
    ///                 .GetPermissionsAsync(user.Id, ct);
    ///
    ///             if (!permissions.Contains("process"))
    ///                 return Result{ProcessedUser}.Failure()
    ///                     .WithError(new ValidationError(
    ///                         "User lacks processing permission",
    ///                         nameof(user.Permissions)));
    ///
    ///             // Process user with external service
    ///             var enrichedData = await externalService
    ///                 .EnrichUserDataAsync(user, ct);
    ///
    ///             var processed = await userProcessor
    ///                 .ProcessAsync(user, enrichedData, ct);
    ///
    ///             return Result{ProcessedUser}.Success(processed);
    ///         }
    ///         catch (ServiceException ex)
    ///         {
    ///             return Result{ProcessedUser}.Failure()
    ///                 .WithError(new ExternalServiceError(ex.Message));
    ///         }
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<PagedResult<TOutput>> CollectAsync<T, TOutput>(
        this Task<PagedResult<T>> resultTask,
        Func<T, CancellationToken, Task<Result<TOutput>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return PagedResult<TOutput>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.CollectAsync(operation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<TOutput>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return PagedResult<TOutput>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }
}