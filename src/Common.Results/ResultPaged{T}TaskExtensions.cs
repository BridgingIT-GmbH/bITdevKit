// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;
using FluentValidation.Internal;
using Microsoft.Extensions.Logging;

/// <summary>
///     Extension methods for Task<ResultPaged<T>> to enable proper chaining.
/// </summary>
public static partial class ResultPagedFunctionTaskExtensions
{
    /// <summary>
    /// Throws a <see cref="ResultException"/> if the ResultPaged task indicates failure.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to check.</param>
    /// <returns>The current ResultPaged if it is successful.</returns>
    /// <exception cref="ResultException">Thrown if the current ResultPaged is a failure.</exception>
    /// <example>
    /// <code>
    /// await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .ThrowIfFailed();
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> ThrowIfFailed<T>(this Task<ResultPaged<T>> resultTask)
    {
        try
        {
            var result = await resultTask;
            return result.ThrowIfFailed();
        }
        catch (Exception ex)
        {
            throw new ResultException("Error while checking result status", ex);
        }
    }

    /// <summary>
    /// Throws an exception of type TException if the ResultPaged task is a failure.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TException">The type of exception to throw.</typeparam>
    /// <param name="resultTask">The ResultPaged task to check.</param>
    /// <returns>The current ResultPaged if it indicates success.</returns>
    /// <exception cref="TException">Thrown if the ResultPaged indicates a failure.</exception>
    /// <example>
    /// <code>
    /// await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .ThrowIfFailed<InvalidOperationException>();
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> ThrowIfFailed<T, TException>(this Task<ResultPaged<T>> resultTask)
        where TException : Exception
    {
        try
        {
            var result = await resultTask;
            return result.ThrowIfFailed<T>();
        }
        catch (TException)
        {
            throw; // Propagate the specified exception
        }
        catch (Exception ex)
        {
            throw new ResultException("Error while checking result status", ex);
        }
    }

    /// <summary>
    /// Applies an operation to each item in a page collection while maintaining error context.
    /// Useful for scenarios where individual item operations might fail independently.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TOutput">The type of the output value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to process.</param>
    /// <param name="operation">The operation to apply to each item.</param>
    /// <returns>A new ResultPaged containing successful results or aggregated errors.</returns>
    /// <example>
    /// <code>
    /// // Process users with individual error handling
    /// var processedUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Collect(user =>
    ///     {
    ///         try
    ///         {
    ///             var processed = userProcessor.Process(user);
    ///             return Result<ProcessedUser>.Success(processed);
    ///         }
    ///         catch (QuotaExceededException)
    ///         {
    ///             return Result<ProcessedUser>.Failure()
    ///                 .WithError(new ValidationError(
    ///                     $"Quota exceeded for user {user.Id}",
    ///                     nameof(user.Quota)));
    ///         }
    ///         catch (Exception ex)
    ///         {
    ///             return Result<ProcessedUser>.Failure()
    ///                 .WithError(new Error($"Processing failed: {ex.Message}"));
    ///         }
    ///     });
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TOutput>> Collect<T, TOutput>(
        this Task<ResultPaged<T>> resultTask,
        Func<T, Result<TOutput>> operation)
    {
        if (operation is null)
        {
            return ResultPaged<TOutput>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return result.Collect(operation);
        }
        catch (Exception ex)
        {
            return ResultPaged<TOutput>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously applies an operation to each item in a page collection while maintaining error context.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TOutput">The type of the output value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to process.</param>
    /// <param name="operation">The async operation to apply to each item.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new ResultPaged containing successful results or aggregated errors.</returns>
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
    ///                 return Result<ProcessedUser>.Failure()
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
    ///             return Result<ProcessedUser>.Success(processed);
    ///         }
    ///         catch (ServiceException ex)
    ///         {
    ///             return Result<ProcessedUser>.Failure()
    ///                 .WithError(new ExternalServiceError(ex.Message));
    ///         }
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TOutput>> CollectAsync<T, TOutput>(
        this Task<ResultPaged<T>> resultTask,
        Func<T, CancellationToken, Task<Result<TOutput>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return ResultPaged<TOutput>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return await result.CollectAsync(operation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TOutput>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<TOutput>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Maps the successful value of a ResultPaged task using the provided mapping function.
    /// Preserves pagination metadata in the transformed result.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The ResultPaged task to map.</param>
    /// <param name="mapper">The function to map the collection.</param>
    /// <returns>A new ResultPaged containing the mapped collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Transform a page of users to DTOs
    /// var userDtos = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Map(users => users.Select(user => new UserDto(user.Id, user.Name)));
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TNew>> Map<T, TNew>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, IEnumerable<TNew>> mapper)
    {
        if (mapper is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Map(mapper);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Maps the successful value of a ResultPaged task using the provided asynchronous mapping function.
    /// Preserves pagination metadata in the transformed result.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The ResultPaged task to map.</param>
    /// <param name="mapper">The async function to map the collection.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new ResultPaged containing the mapped collection or the original errors.</returns>
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
    public static async Task<ResultPaged<TNew>> MapAsync<T, TNew>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<IEnumerable<TNew>>> mapper,
        CancellationToken cancellationToken = default)
    {
        if (mapper is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.MapAsync(mapper, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
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
    /// <param name="resultTask">The ResultPaged task to map.</param>
    /// <param name="mapper">The function to map the collection.</param>
    /// <param name="action">The action to perform on the mapped collection.</param>
    /// <returns>A new ResultPaged containing the mapped collection or the original errors.</returns>
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
    public static async Task<ResultPaged<TNew>> TeeMap<T, TNew>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, IEnumerable<TNew>> mapper,
        Action<IEnumerable<TNew>> action)
    {
        if (mapper is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.TeeMap(mapper, action);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
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
    /// <param name="resultTask">The ResultPaged task to map.</param>
    /// <param name="mapper">The function to map the collection.</param>
    /// <param name="action">The async action to perform on the mapped collection.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new ResultPaged containing the mapped collection or the original errors.</returns>
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
    public static async Task<ResultPaged<TNew>> TeeMapAsync<T, TNew>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, IEnumerable<TNew>> mapper,
        Func<IEnumerable<TNew>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (mapper is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.TeeMapAsync(mapper, action, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Maps both success and failure cases of a ResultPaged task simultaneously while preserving pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The ResultPaged task to transform.</param>
    /// <param name="onSuccess">Function to transform the collection if successful.</param>
    /// <param name="onFailure">Function to transform the errors if failed.</param>
    /// <returns>A new ResultPaged with either the transformed collection or transformed errors.</returns>
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
    public static async Task<ResultPaged<TNew>> BiMap<T, TNew>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, IEnumerable<TNew>> onSuccess,
        Func<IReadOnlyList<IResultError>, IEnumerable<IResultError>> onFailure)
    {
        if (onSuccess is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Success mapper cannot be null"));
        }

        if (onFailure is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Failure mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.BiMap(onSuccess, onFailure);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously maps both success and failure cases of a ResultPaged task while preserving pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The ResultPaged task to transform.</param>
    /// <param name="onSuccess">Async function to transform the collection if successful.</param>
    /// <param name="onFailure">Async function to transform the errors if failed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new ResultPaged with either the transformed collection or transformed errors.</returns>
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
    public static async Task<ResultPaged<TNew>> BiMapAsync<T, TNew>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<IEnumerable<TNew>>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<IEnumerable<IResultError>>> onFailure,
        CancellationToken cancellationToken = default)
    {
        if (onSuccess is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Success mapper cannot be null"));
        }

        if (onFailure is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Failure mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            if (result.IsSuccess)
            {
                var transformedValues = await onSuccess(result.Value, cancellationToken);

                return ResultPaged<TNew>.Success(
                        transformedValues,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithMessages(result.Messages);
            }

            var transformedErrors = await onFailure(result.Errors, cancellationToken);

            return ResultPaged<TNew>.Failure()
                .WithErrors(transformedErrors)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Binds the successful value of a ResultPaged task to another ResultPaged using the provided binding function.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new ResultPaged value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to bind.</param>
    /// <param name="binder">The function to bind the collection to a new ResultPaged.</param>
    /// <returns>The bound ResultPaged or a failure containing the original errors.</returns>
    /// <example>
    /// <code>
    /// // Filter active users and update pagination metadata
    /// var activeUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Bind(users =>
    ///     {
    ///         var active = users.Where(u => u.IsActive).ToList();
    ///         return active.Any()
    ///             ? ResultPaged{User}.Success(
    ///                 active,
    ///                 totalActiveUsers,
    ///                 pageNumber,
    ///                 pageSize)
    ///             : ResultPaged{User}.Failure("No active users found");
    ///     });
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TNew>> Bind<T, TNew>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, ResultPaged<TNew>> binder)
    {
        if (binder is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Binder cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Bind(binder);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Binds the successful value of a ResultPaged task to another ResultPaged using the provided async binding function.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new ResultPaged value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to bind.</param>
    /// <param name="binder">The async function to bind the collection to a new ResultPaged.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The bound ResultPaged or a failure containing the original errors.</returns>
    /// <example>
    /// <code>
    /// // Validate and enrich users asynchronously
    /// var enrichedUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .BindAsync(async (users, ct) =>
    ///     {
    ///         var validationResults = await ValidateUsersAsync(users, ct);
    ///         if (validationResults.HasErrors)
    ///             return ResultPaged{User}.Failure("Validation failed")
    ///                 .WithErrors(validationResults.Errors);
    ///
    ///         var enriched = await EnrichUsersAsync(users, ct);
    ///         return ResultPaged{User}.Success(
    ///             enriched,
    ///             totalCount,
    ///             pageNumber,
    ///             pageSize);
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TNew>> BindAsync<T, TNew>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<ResultPaged<TNew>>> binder,
        CancellationToken cancellationToken = default)
    {
        if (binder is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Binder cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.BindAsync(binder, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Chains a new operation on a successful ResultPaged task, preserving pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to chain from.</param>
    /// <param name="operation">The operation to execute if successful.</param>
    /// <returns>A new ResultPaged with the operation result or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Process users with multiple validations
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .AndThen(users => ValidateUserPermissions(users))
    ///     .AndThen(users => EnsureUserQuota(users))
    ///     .AndThen(users => UpdateUserStatus(users));
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> AndThen<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, ResultPaged<T>> operation)
    {
        if (operation is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.AndThen(operation);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Chains a new async operation on a successful ResultPaged task, preserving pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to chain from.</param>
    /// <param name="operation">The async operation to execute if successful.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new ResultPaged with the operation result or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Process users with async validations and updates
    /// var result = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .AndThenAsync(
    ///         async (users, ct) =>
    ///         {
    ///             var validationResult = await ValidateUsersAsync(users, ct);
    ///             return validationResult.IsValid
    ///                 ? ResultPaged{User}.Success(users)
    ///                 : ResultPaged{User}.Failure("Validation failed")
    ///                     .WithErrors(validationResult.Errors);
    ///         },
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> AndThenAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<ResultPaged<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.AndThenAsync(operation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Provides a fallback ResultPaged if the task fails, maintaining pagination context where possible.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task.</param>
    /// <param name="fallback">The function providing the fallback ResultPaged.</param>
    /// <returns>The original ResultPaged if successful; otherwise, the fallback result.</returns>
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
    public static async Task<ResultPaged<T>> OrElse<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<ResultPaged<T>> fallback)
    {
        if (fallback is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Fallback cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.OrElse(fallback);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Provides an async fallback ResultPaged if the task fails, maintaining pagination context where possible.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task.</param>
    /// <param name="fallback">The async function providing the fallback ResultPaged.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original ResultPaged if successful; otherwise, the fallback result.</returns>
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
    public static async Task<ResultPaged<T>> OrElseAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<CancellationToken, Task<ResultPaged<T>>> fallback,
        CancellationToken cancellationToken = default)
    {
        if (fallback is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Fallback cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.OrElseAsync(fallback, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Filters a ResultPaged task based on a predicate, maintaining pagination metadata.
    /// Returns failure if the predicate isn't satisfied.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to filter.</param>
    /// <param name="predicate">The condition that must be true for the entire page.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original ResultPaged if predicate is met; otherwise, a failure.</returns>
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
    public static async Task<ResultPaged<T>> Filter<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, bool> predicate,
        IResultError error)
    {
        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Filter(predicate, error);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Filters a ResultPaged task based on an async predicate, maintaining pagination metadata.
    /// Returns failure if the predicate isn't satisfied.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to filter.</param>
    /// <param name="predicate">The async condition that must be true for the entire page.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original ResultPaged if predicate is met; otherwise, a failure.</returns>
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
    public static async Task<ResultPaged<T>> FilterAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.FilterAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Filters a ResultPaged task based on a predicate, maintaining pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to filter.</param>
    /// <param name="predicate">The filtering condition for individual items.</param>
    /// <returns>ResultPaged containing filtered items.</returns>
    /// <example>
    /// <code>
    /// // Filter active users
    /// var activeUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Filter(user => user.IsActive);
    ///
    /// // Filter users by criteria
    /// var eligibleUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .Filter(user => user.Age >= 18 && user.HasValidProfile);
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> Filter<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<T, bool> predicate)
    {
        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Filter(predicate);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Filters a ResultPaged task based on an async predicate.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to filter.</param>
    /// <param name="predicate">The async filtering condition for individual items.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>ResultPaged containing filtered items.</returns>
    /// <example>
    /// <code>
    /// // Filter users with valid subscriptions
    /// var validUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    ///     .FilterAsync(
    ///         async (user, ct) => await subscriptionService.IsValidAsync(user.Id, ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> FilterAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<T, CancellationToken, Task<bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.FilterAsync(predicate, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Ensures a condition is met for the paged collection, maintaining pagination metadata.
    /// Similar to Filter but with more semantic meaning for validation scenarios.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to check.</param>
    /// <param name="predicate">The condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original ResultPaged if condition is met; otherwise, a failure.</returns>
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
    public static async Task<ResultPaged<T>> Ensure<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, bool> predicate,
        IResultError error)
    {
        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Ensure(predicate, error);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously ensures a condition is met for the paged collection.
    /// Similar to FilterAsync but with more semantic meaning for validation scenarios.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to check.</param>
    /// <param name="predicate">The async condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original ResultPaged if condition is met; otherwise, a failure.</returns>
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
    public static async Task<ResultPaged<T>> EnsureAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.EnsureAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Converts a successful ResultPaged to a failure if a condition is met.
    /// Useful for negative validation scenarios while maintaining pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to check.</param>
    /// <param name="predicate">The condition that must be false.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <returns>The original ResultPaged if condition is false; otherwise, a failure.</returns>
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
    public static async Task<ResultPaged<T>> Unless<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, bool> predicate,
        IResultError error)
    {
        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Unless(predicate, error);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously converts a successful ResultPaged to a failure if a condition is met.
    /// Useful for negative validation scenarios while maintaining pagination metadata.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to check.</param>
    /// <param name="predicate">The async condition that must be false.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original ResultPaged if condition is false; otherwise, a failure.</returns>
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
    public static async Task<ResultPaged<T>> UnlessAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.UnlessAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Validates each item in a ResultPaged task using FluentValidation.
    /// Maintains pagination metadata while accumulating validation errors.
    /// </summary>
    /// <typeparam name="T">The type of items to validate.</typeparam>
    /// <param name="resultTask">The ResultPaged task containing items to validate.</param>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Optional validation strategy options to apply.</param>
    /// <returns>The original ResultPaged if all validations succeed; otherwise, a failure with validation errors.</returns>
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
    public static async Task<ResultPaged<T>> Validate<T>(
        this Task<ResultPaged<T>> resultTask,
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null)
    {
        if (validator is null)
        {
            return ResultPaged<T>.Failure()
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
                return ResultPaged<T>.Failure(result.Value)
                    .WithErrors(errors)
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage("Validation failed due to an error");
        }
    }

    /// <summary>
    /// Asynchronously validates each item in a ResultPaged task using FluentValidation.
    /// Maintains pagination metadata while accumulating validation errors.
    /// </summary>
    /// <typeparam name="T">The type of items to validate.</typeparam>
    /// <param name="resultTask">The ResultPaged task containing items to validate.</param>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Optional validation strategy options to apply.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original ResultPaged if all validations succeed; otherwise, a failure with validation errors.</returns>
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
    public static async Task<ResultPaged<T>> ValidateAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null,
        CancellationToken cancellationToken = default)
    {
        if (validator is null)
        {
            return ResultPaged<T>.Failure()
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
                return ResultPaged<T>.Failure(result.Value)
                    .WithErrors(errors)
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage("Validation failed due to an error");
        }
    }

    /// <summary>
    /// Executes a side-effect on the successful page collection without changing the result.
    /// Useful for logging, metrics, and other non-transformative operations.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to tap into.</param>
    /// <param name="action">The action to execute with the page collection.</param>
    /// <returns>The original ResultPaged unchanged.</returns>
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
    public static async Task<ResultPaged<T>> Tap<T>(
        this Task<ResultPaged<T>> resultTask,
        Action<IEnumerable<T>> action)
    {
        if (action is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Tap(action);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Executes an async side-effect on the successful page collection without changing the result.
    /// Useful for logging, caching, and other async non-transformative operations.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to tap into.</param>
    /// <param name="action">The async action to execute with the page collection.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original ResultPaged unchanged.</returns>
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
    public static async Task<ResultPaged<T>> TapAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.TapAsync(action, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Logs an awaited <see cref="Task{ResultPaged}"/> using structured logging with default levels
    /// (Debug on success, Warning on failure).
    /// </summary>
    /// <typeparam name="T">The type of the paged items.</typeparam>
    /// <param name="resultTask">The task returning a paged result to log.</param>
    /// <param name="logger">
    /// The logger to write to. If null, the method awaits and returns the original result.
    /// </param>
    /// <param name="messageTemplate">
    /// Optional message template for structured logging (e.g., "Fetched page {Page} of {Entity}").
    /// </param>
    /// <param name="args">
    /// Optional structured logging arguments corresponding to <paramref name="messageTemplate"/>.
    /// </param>
    /// <remarks>
    /// This method awaits <paramref name="resultTask"/> and delegates to the synchronous overload.
    /// </remarks>
    /// <returns>The awaited <see cref="ResultPaged{T}"/> unchanged.</returns>
    /// <example>
    /// <code>
    /// var paged = await service.GetPagedAsync(q).Log(logger, "Fetched {Entity} page {Page}", "Product", q.Page);
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> Log<T>(
        this Task<ResultPaged<T>> resultTask,
        ILogger logger,
        string messageTemplate = null,
        params object[] args)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Log(logger, messageTemplate, args);
    }

    /// <summary>
    /// Logs an awaited <see cref="Task{ResultPaged}"/> using structured logging with custom levels.
    /// </summary>
    /// <typeparam name="T">The type of the paged items.</typeparam>
    /// <param name="resultTask">The task returning a paged result to log.</param>
    /// <param name="logger">
    /// The logger to write to. If null, the method awaits and returns the original result.
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
    /// This method awaits <paramref name="resultTask"/> and delegates to the synchronous overload.
    /// </remarks>
    /// <returns>The awaited <see cref="ResultPaged{T}"/> unchanged.</returns>
    /// <example>
    /// <code>
    /// var paged = await query.RunAsync().Log(
    ///     logger,
    ///     "Listed {Entity} page {Page} size {Size}",
    ///     LogLevel.Information,
    ///     LogLevel.Warning,
    ///     "User",
    ///     page,
    ///     size);
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> Log<T>(
        this Task<ResultPaged<T>> resultTask,
        ILogger logger,
        string messageTemplate,
        LogLevel successLevel,
        LogLevel failureLevel,
        params object[] args)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Log(logger, messageTemplate, successLevel, failureLevel, args);
    }

    /// <summary>
    /// Logs an awaited Task&lt;ResultPaged&lt;T&gt;&gt; using structured logging, allowing callers to include
    /// additional info derived from the resulting value. Uses default levels (Debug/Warning).
    /// </summary>
    /// <typeparam name="T">The type of the paged items.</typeparam>
    /// <typeparam name="TInfo">The type of the additional info to log.</typeparam>
    /// <param name="resultTask">The task returning a paged result to log.</param>
    /// <param name="logger">The logger to write to. If null, the method is a no-op.</param>
    /// <param name="infoSelector">
    /// A function that extracts safe, optional info from the awaited result for logging.
    /// </param>
    /// <param name="messageTemplate">
    /// Optional message template for structured logging (e.g., "Fetched {Entity} page {Page}").
    /// </param>
    /// <param name="args">Optional arguments for <paramref name="messageTemplate"/>.</param>
    /// <remarks>
    /// Awaits <paramref name="resultTask"/> and delegates to the synchronous overload.
    /// </remarks>
    /// <returns>The awaited paged result, unchanged.</returns>
    /// <example>
    /// <code>
    /// var paged = await query.RunAsync().Log(
    ///     logger,
    ///     r =&gt; r.Value?.Count,
    ///     "Fetched {Entity} page {Page}",
    ///     "Order",
    ///     page);
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> Log<T, TInfo>(
        this Task<ResultPaged<T>> resultTask,
        ILogger logger,
        Func<ResultPaged<T>, TInfo> infoSelector,
        string messageTemplate = null,
        params object[] args)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Log(logger, infoSelector, messageTemplate, args);
    }

    /// <summary>
    /// Logs an awaited Task&lt;ResultPaged&lt;T&gt;&gt; using structured logging with custom levels
    /// and an info selector to include additional info derived from the awaited result.
    /// </summary>
    /// <typeparam name="T">The type of the paged items.</typeparam>
    /// <typeparam name="TInfo">The type of the additional info to log.</typeparam>
    /// <param name="resultTask">The task returning a paged result to log.</param>
    /// <param name="logger">The logger to write to. If null, the method is a no-op.</param>
    /// <param name="infoSelector">
    /// A function that extracts safe, optional info from the awaited result for logging.
    /// </param>
    /// <param name="messageTemplate">
    /// Optional message template for structured logging (e.g., "Searched {Entity} page {Page}").
    /// </param>
    /// <param name="successLevel">Log level when the result indicates success.</param>
    /// <param name="failureLevel">Log level when the result indicates failure.</param>
    /// <param name="args">Optional arguments for <paramref name="messageTemplate"/>.</param>
    /// <remarks>
    /// Awaits <paramref name="resultTask"/> and delegates to the synchronous overload.
    /// </remarks>
    /// <returns>The awaited paged result, unchanged.</returns>
    /// <example>
    /// <code>
    /// var paged = await service.GetPagedAsync(q).Log(
    ///     logger,
    ///     r =&gt; r.Value?.Count,
    ///     "Listed {Entity} page {Page} size {Size}",
    ///     LogLevel.Information,
    ///     LogLevel.Warning,
    ///     "User",
    ///     q.Page,
    ///     q.PageSize);
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> Log<T, TInfo>(
        this Task<ResultPaged<T>> resultTask,
        ILogger logger,
        Func<ResultPaged<T>, TInfo> infoSelector,
        string messageTemplate,
        LogLevel successLevel,
        LogLevel failureLevel,
        params object[] args)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Log(logger, infoSelector, messageTemplate, successLevel, failureLevel, args);
    }

    /// <summary>
    /// Executes a side-effect independent of the page collection without changing the result.
    /// Useful for general operations that don't need access to the page data.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The original ResultPaged unchanged.</returns>
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
    public static async Task<ResultPaged<T>> Do<T>(
        this Task<ResultPaged<T>> resultTask,
        Action action)
    {
        if (action is null)
        {
            return ResultPaged<T>.Failure()
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
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Executes an async side-effect independent of the page collection without changing the result.
    /// Useful for general async operations that don't need access to the page data.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The ResultPaged task.</param>
    /// <param name="action">The async action to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original ResultPaged unchanged.</returns>
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
    public static async Task<ResultPaged<T>> DoAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            return ResultPaged<T>.Failure()
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
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Attempts to perform a synchronous operation on a paged collection, returning a ResultPaged in a Task.
    /// This wrapper is useful for integrating synchronous `Try` operations into asynchronous workflows.
    /// </summary>
    /// <typeparam name="T">The type of items in the paged result.</typeparam>
    /// <param name="operation">The operation to execute, which returns a collection and a total count.</param>
    /// <returns>A Task-wrapped ResultPaged containing the operation's result or failure information.</returns>
    /// <example>
    /// <code>
    /// var result =await GetPagedUsersAsync(pageNumber, pageSize).Try(() =>
    /// {
    ///     var users = _repository.GetPagedUsers(page, pageSize);
    ///     return (users, totalCount: _repository.GetTotalCount());
    /// });
    /// </code>
    /// </example>
    public static Task<ResultPaged<T>> Try<T>(
        Func<(IEnumerable<T> Values, long TotalCount)> operation)
    {
        if (operation is null)
        {
            return Task.FromResult(ResultPaged<T>.Failure()
                .WithError(new Error("Operation cannot be null")));
        }

        try
        {
            var (values, totalCount) = operation();

            return Task.FromResult(ResultPaged<T>.Success(values, totalCount));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ResultPaged<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message));
        }
    }

    /// <summary>
    /// Attempts to perform an asynchronous operation that returns a paged collection, wrapping the result in a ResultPaged.
    /// Use this method to handle potentially failing async operations and maintain consistent paging metadata.
    /// </summary>
    /// <typeparam name="T">The type of items in the paged result.</typeparam>
    /// <param name="operation">An asynchronous operation that returns a collection and total count.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A Task-wrapped ResultPaged containing the result or failure information.</returns>
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
    public static async Task<ResultPaged<T>> TryAsync<T>(
        Func<CancellationToken, Task<(IEnumerable<T> Values, long TotalCount)>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var (values, totalCount) = await operation(cancellationToken);

            return ResultPaged<T>.Success(values, totalCount);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError())
                .WithMessage("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
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
    /// <param name="resultTask">The ResultPaged task to transform.</param>
    /// <param name="chooser">Function that returns Some for items to keep, None for items to filter out.</param>
    /// <returns>A new ResultPaged containing the chosen values or a failure.</returns>
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
    public static async Task<ResultPaged<TNew>> Choose<T, TNew>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, ResultChooseOption<IEnumerable<TNew>>> chooser)
    {
        if (chooser is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Chooser cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Choose(chooser);
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously selectively transforms a page collection using an option-based approach.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new value.</typeparam>
    /// <param name="resultTask">The ResultPaged task to transform.</param>
    /// <param name="chooser">Async function that returns Some for items to keep, None for items to filter out.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new ResultPaged containing the chosen values or a failure.</returns>
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
    public static async Task<ResultPaged<TNew>> ChooseAsync<T, TNew>(
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task<ResultChooseOption<IEnumerable<TNew>>>> chooser,
        CancellationToken cancellationToken = default)
    {
        if (chooser is null)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new Error("Chooser cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.ChooseAsync(chooser, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Executes different functions based on the result's success state from a Task<ResultPaged{T}>.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="resultTask">The Task containing the ResultPaged to match.</param>
    /// <param name="onSuccess">Function to execute if the Result is successful, receiving the values.</param>
    /// <param name="onFailure">Function to execute if the Result failed, receiving the errors.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if an error occurs during pattern matching.</exception>
    /// <example>
    /// <code>
    /// var resultTask = GetPagedUsersAsync(pageNumber, pageSize);
    /// var message = await resultTask.Match(
    ///     onSuccess: r => $"Found {r.TotalCount} users across {r.TotalPages} pages",
    ///     onFailure: errors => $"Failed with {errors.Count} errors"
    /// );
    /// </code>
    /// </example>
    public static async Task<TResult> Match<T, TResult>(
        this Task<ResultPaged<T>> resultTask,
        Func<ResultPaged<T>, TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;
            return result.Match(onSuccess, onFailure);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during pattern matching", ex);
        }
    }

    /// <summary>
    /// Returns different values based on the result's success state from a Task<ResultPaged{T}>.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="resultTask">The Task containing the ResultPaged to match.</param>
    /// <param name="success">Value to return if successful.</param>
    /// <param name="failure">Value to return if failed.</param>
    /// <returns>Either the success or failure value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if an error occurs during pattern matching.</exception>
    /// <example>
    /// <code>
    /// var resultTask = GetPagedUsersAsync(pageNumber, pageSize);
    /// var status = await resultTask.Match(
    ///     success: "Users retrieved successfully",
    ///     failure: "Failed to retrieve users"
    /// );
    /// </code>
    /// </example>
    public static async Task<TResult> Match<T, TResult>(
        this Task<ResultPaged<T>> resultTask,
        TResult success,
        TResult failure)
    {
        try
        {
            var result = await resultTask;
            return result.Match(success, failure);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during pattern matching", ex);
        }
    }

    /// <summary>
    /// Asynchronously executes different functions based on the result's success state from a Task<ResultPaged{T}>.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="resultTask">The Task containing the ResultPaged to match.</param>
    /// <param name="onSuccess">Async function to execute if successful, receiving the values.</param>
    /// <param name="onFailure">Async function to execute if failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
    /// <example>
    /// <code>
    /// var resultTask = GetPagedUsersAsync(pageNumber, pageSize);
    /// var status = await resultTask.MatchAsync(
    ///     async (r, ct) => await FormatSuccessMessageAsync(r, ct),
    ///     async (errors, ct) => await FormatErrorMessageAsync(errors, ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<ResultPaged<T>> resultTask,
        Func<ResultPaged<T>, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;
            return await result.MatchAsync(onSuccess, onFailure, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during pattern matching", ex);
        }
    }

    /// <summary>
    /// Executes different actions based on the ResultPaged's success state from a Task<ResultPaged{T}>.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="resultTask">The Task containing the ResultPaged to handle.</param>
    /// <param name="onSuccess">Action to execute if the Result is successful, receiving the values.</param>
    /// <param name="onFailure">Action to execute if the Result failed, receiving the errors.</param>
    /// <returns>The original ResultPaged wrapped in a Task.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var resultTask = GetPagedUsersAsync(pageNumber, pageSize);
    /// await resultTask.Handle(
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
    public static async Task<ResultPaged<T>> Handle<T>(
        this Task<ResultPaged<T>> resultTask,
        Action<IEnumerable<T>> onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;
            if (result.IsSuccess)
            {
                onSuccess(result.Value);
            }
            else
            {
                onFailure(result.Errors);
            }
            return result;
        }
        catch (Exception ex)
        {
            onFailure(new[] { Result.Settings.ExceptionErrorFactory(ex) }.ToList().AsReadOnly());
            return await resultTask; // Return the original result despite the exception
        }
    }

    /// <summary>
    /// Asynchronously executes different actions based on the ResultPaged's success state from a Task<ResultPaged{T}>.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="resultTask">The Task containing the ResultPaged to handle.</param>
    /// <param name="onSuccess">Async function to execute if the Result is successful, receiving the values.</param>
    /// <param name="onFailure">Async function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original ResultPaged instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var resultTask = GetPagedUsersAsync(pageNumber, pageSize);
    /// await resultTask.HandleAsync(
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
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;
            if (result.IsSuccess)
            {
                await onSuccess(result.Value, cancellationToken);
            }
            else
            {
                await onFailure(result.Errors, cancellationToken);
            }
            return result;
        }
        catch (Exception ex)
        {
            await onFailure(new[] { Result.Settings.ExceptionErrorFactory(ex) }.ToList().AsReadOnly(), cancellationToken);
            return await resultTask; // Return the original result despite the exception
        }
    }

    /// <summary>
    /// Asynchronously executes a success function with a synchronous failure handler from a Task<ResultPaged{T}>.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="resultTask">The Task containing the ResultPaged to handle.</param>
    /// <param name="onSuccess">Async function to execute if the Result is successful, receiving the values.</param>
    /// <param name="onFailure">Synchronous function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original ResultPaged instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var resultTask = GetPagedUsersAsync(pageNumber, pageSize);
    /// await resultTask.HandleAsync(
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
        this Task<ResultPaged<T>> resultTask,
        Func<IEnumerable<T>, CancellationToken, Task> onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;
            if (result.IsSuccess)
            {
                await onSuccess(result.Value, cancellationToken);
            }
            else
            {
                onFailure(result.Errors);
            }
            return result;
        }
        catch (Exception ex)
        {
            onFailure(new[] { Result.Settings.ExceptionErrorFactory(ex) }.ToList().AsReadOnly());
            return await resultTask; // Return the original result despite the exception
        }
    }

    /// <summary>
    /// Executes a synchronous success function with an async failure handler from a Task<ResultPaged{T}>.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="resultTask">The Task containing the ResultPaged to handle.</param>
    /// <param name="onSuccess">Synchronous function to execute if the Result is successful, receiving the values.</param>
    /// <param name="onFailure">Async function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original ResultPaged instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var resultTask = GetPagedUsersAsync(pageNumber, pageSize);
    /// await resultTask.HandleAsync(
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
        this Task<ResultPaged<T>> resultTask,
        Action<IEnumerable<T>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;
            if (result.IsSuccess)
            {
                onSuccess(result.Value);
            }
            else
            {
                await onFailure(result.Errors, cancellationToken);
            }
            return result;
        }
        catch (Exception ex)
        {
            await onFailure(new[] { Result.Settings.ExceptionErrorFactory(ex) }.ToList().AsReadOnly(), cancellationToken);
            return await resultTask; // Return the original result despite the exception
        }
    }

    ///// <summary>
    ///// Applies an operation to each item in a page collection while maintaining error context.
    ///// Useful for scenarios where individual item operations might fail independently.
    ///// </summary>
    ///// <typeparam name="T">The type of the source value.</typeparam>
    ///// <typeparam name="TOutput">The type of the output value.</typeparam>
    ///// <param name="resultTask">The ResultPaged task to process.</param>
    ///// <param name="operation">The operation to apply to each item.</param>
    ///// <returns>A new ResultPaged containing successful results or aggregated errors.</returns>
    ///// <example>
    ///// <code>
    ///// // Process users with individual error handling
    ///// var processedUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    /////     .Collect(user =>
    /////     {
    /////         try
    /////         {
    /////             var processed = userProcessor.Process(user);
    /////             return Result{ProcessedUser}.Success(processed);
    /////         }
    /////         catch (QuotaExceededException)
    /////         {
    /////             return Result{ProcessedUser}.Failure()
    /////                 .WithError(new ValidationError(
    /////                     $"Quota exceeded for user {user.Id}",
    /////                     nameof(user.Quota)));
    /////         }
    /////         catch (Exception ex)
    /////         {
    /////             return Result{ProcessedUser}.Failure()
    /////                 .WithError(new Error($"Processing failed: {ex.Message}"));
    /////         }
    /////     });
    ///// </code>
    ///// </example>
    //public static async Task<ResultPaged<TOutput>> Collect<T, TOutput>(
    //    this Task<ResultPaged<T>> resultTask,
    //    Func<T, Result<TOutput>> operation)
    //{
    //    if (operation is null)
    //    {
    //        return ResultPaged<TOutput>.Failure()
    //            .WithError(new Error("Operation cannot be null"));
    //    }

    //    try
    //    {
    //        var result = await resultTask;

    //        return result.Collect(operation);
    //    }
    //    catch (Exception ex)
    //    {
    //        return ResultPaged<TOutput>.Failure()
    //            .WithError(Result.Settings.ExceptionErrorFactory(ex))
    //            .WithMessage(ex.Message);
    //    }
    //}

    ///// <summary>
    ///// Asynchronously applies an operation to each item in a page collection while maintaining error context.
    ///// </summary>
    ///// <typeparam name="T">The type of the source value.</typeparam>
    ///// <typeparam name="TOutput">The type of the output value.</typeparam>
    ///// <param name="resultTask">The ResultPaged task to process.</param>
    ///// <param name="operation">The async operation to apply to each item.</param>
    ///// <param name="cancellationToken">Token to cancel the operation.</param>
    ///// <returns>A new ResultPaged containing successful results or aggregated errors.</returns>
    ///// <example>
    ///// <code>
    ///// // Process users with external service integration
    ///// var processedUsers = await GetPagedUsersAsync(pageNumber, pageSize)
    /////     .CollectAsync(async (user, ct) =>
    /////     {
    /////         try
    /////         {
    /////             // Verify user permissions
    /////             var permissions = await permissionService
    /////                 .GetPermissionsAsync(user.Id, ct);
    /////
    /////             if (!permissions.Contains("process"))
    /////                 return Result{ProcessedUser}.Failure()
    /////                     .WithError(new ValidationError(
    /////                         "User lacks processing permission",
    /////                         nameof(user.Permissions)));
    /////
    /////             // Process user with external service
    /////             var enrichedData = await externalService
    /////                 .EnrichUserDataAsync(user, ct);
    /////
    /////             var processed = await userProcessor
    /////                 .ProcessAsync(user, enrichedData, ct);
    /////
    /////             return Result{ProcessedUser}.Success(processed);
    /////         }
    /////         catch (ServiceException ex)
    /////         {
    /////             return Result{ProcessedUser}.Failure()
    /////                 .WithError(new ExternalServiceError(ex.Message));
    /////         }
    /////     },
    /////     cancellationToken);
    ///// </code>
    ///// </example>
    //public static async Task<ResultPaged<TOutput>> CollectAsync<T, TOutput>(
    //    this Task<ResultPaged<T>> resultTask,
    //    Func<T, CancellationToken, Task<Result<TOutput>>> operation,
    //    CancellationToken cancellationToken = default)
    //{
    //    if (operation is null)
    //    {
    //        return ResultPaged<TOutput>.Failure()
    //            .WithError(new Error("Operation cannot be null"));
    //    }

    //    try
    //    {
    //        var result = await resultTask;

    //        return await result.CollectAsync(operation, cancellationToken);
    //    }
    //    catch (OperationCanceledException)
    //    {
    //        return ResultPaged<TOutput>.Failure()
    //            .WithError(new OperationCancelledError());
    //    }
    //    catch (Exception ex)
    //    {
    //        return ResultPaged<TOutput>.Failure()
    //            .WithError(Result.Settings.ExceptionErrorFactory(ex))
    //            .WithMessage(ex.Message);
    //    }
    //}
}