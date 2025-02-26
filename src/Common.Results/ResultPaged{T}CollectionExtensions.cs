// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;

/// <summary>
///     Extension methods for operations on ResultPaged{IEnumerable{T}} to enable proper collection handling.
/// </summary>
public static class ResultPagedCollectionExtensions
{
    #region Core Collection Filtering Operations

    /// <summary>
    ///     Filters elements in a paged Result based on a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <param name="result">The paged Result containing the collection to filter.</param>
    /// <param name="predicate">The function to test each element for a condition.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A new paged Result containing the filtered collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Basic filtering
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .FilterItems(user => user.IsActive);
    ///
    /// // With processing options
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .FilterItems(
    ///         user => user.IsActive,
    ///         ProcessingOptions.Strict
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<T> FilterItems<T>(
        this ResultPaged<T> result,
        Func<T, bool> predicate,
        ProcessingOptions options = null)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<T>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var filteredItems = new List<T>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    if (predicate(item))
                    {
                        filteredItems.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error filtering item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        filteredItems.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Filtering aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<T>.Success(
                        filteredItems,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<T>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Filters elements in a paged Result based on an asynchronous predicate.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <param name="result">The paged Result containing the collection to filter.</param>
    /// <param name="predicate">The async function to test each element for a condition.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new paged Result containing the filtered collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Basic async filtering
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .FilterItemsAsync(
    ///         async (user, ct) => await IsActiveAsync(user, ct),
    ///         cancellationToken: cancellationToken
    ///     );
    ///
    /// // With processing options
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .FilterItemsAsync(
    ///         async (user, ct) => await IsActiveAsync(user, ct),
    ///         new ProcessingOptions { IncludeFailedItems = true },
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> FilterItemsAsync<T>(
        this ResultPaged<T> result,
        Func<T, CancellationToken, Task<bool>> predicate,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<T>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var filteredItems = new List<T>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await predicate(item, cancellationToken))
                    {
                        filteredItems.Add(item);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error filtering item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        filteredItems.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Filtering aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<T>.Success(
                        filteredItems,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<T>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Filters elements in a paged Result task based on a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <param name="resultTask">The paged Result task containing the collection to filter.</param>
    /// <param name="predicate">The function to test each element for a condition.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A new paged Result containing the filtered collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Basic filtering of task result
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .FilterItems(user => user.IsActive);
    ///
    /// // With processing options
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .FilterItems(
    ///         user => user.IsActive,
    ///         ProcessingOptions.Default
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> FilterItems<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<T, bool> predicate,
        ProcessingOptions options = null)
    {
        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.FilterItems(predicate, options);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Filters elements in a paged Result task based on an asynchronous predicate.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <param name="resultTask">The paged Result task containing the collection to filter.</param>
    /// <param name="predicate">The async function to test each element for a condition.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new paged Result containing the filtered collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Basic async filtering of task result
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .FilterItemsAsync(
    ///         async (user, ct) => await IsActiveAsync(user, ct),
    ///         cancellationToken: cancellationToken
    ///     );
    ///
    /// // With processing options
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .FilterItemsAsync(
    ///         async (user, ct) => await IsActiveAsync(user, ct),
    ///         ProcessingOptions.Strict,
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> FilterItemsAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<T, CancellationToken, Task<bool>> predicate,
        ProcessingOptions options = null,
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

            return await result.FilterItemsAsync(predicate, options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    #endregion

    #region Collection Mapping Operations

    /// <summary>
    ///     Maps elements in a paged Result by applying a transform function to each element.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="result">The paged Result containing the collection to map.</param>
    /// <param name="mapper">The function to apply to each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A new paged Result containing the transformed collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Basic mapping
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .MapItems(user => new UserDto(user.Id, user.Name));
    ///
    /// // Mapping with strict options
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .MapItems(
    ///         user => new UserDto(user.Id, user.Name),
    ///         ProcessingOptions.Strict
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<TResult> MapItems<TSource, TResult>(
        this ResultPaged<TSource> result,
        Func<TSource, TResult> mapper,
        ProcessingOptions options = null)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<TResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (mapper is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var mappedItems = new List<TResult>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    var mappedItem = mapper(item);
                    mappedItems.Add(mappedItem);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error mapping item: {ex.Message}");

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Mapping aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = mappedItems.Count != 0 &&
                (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<TResult>.Success(
                        mappedItems,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<TResult>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps elements in a paged Result by applying an asynchronous transform function to each element.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="result">The paged Result containing the collection to map.</param>
    /// <param name="mapper">The async function to apply to each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new paged Result containing the transformed collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// // Basic async mapping
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .MapItemsAsync(
    ///         async (user, ct) => await CreateUserDtoAsync(user, ct),
    ///         cancellationToken: cancellationToken
    ///     );
    ///
    /// // Async mapping with options
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .MapItemsAsync(
    ///         async (user, ct) => await CreateUserDtoAsync(user, ct),
    ///         ProcessingOptions.Default,
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TResult>> MapItemsAsync<TSource, TResult>(
        this ResultPaged<TSource> result,
        Func<TSource, CancellationToken, Task<TResult>> mapper,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<TResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (mapper is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var mappedItems = new List<TResult>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var mappedItem = await mapper(item, cancellationToken);
                    mappedItems.Add(mappedItem);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error mapping item: {ex.Message}");

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Mapping aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = mappedItems.Count != 0 &&
                (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<TResult>.Success(
                        mappedItems,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<TResult>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps elements in a paged Result task by applying a transform function to each element.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="resultTask">The paged Result task containing the collection to map.</param>
    /// <param name="mapper">The function to apply to each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A new paged Result containing the transformed collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .MapItems(user => new UserDto(user.Id, user.Name));
    ///
    /// // With processing options
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .MapItems(
    ///         user => new UserDto(user.Id, user.Name),
    ///         new ProcessingOptions { MaxFailures = 5 }
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TResult>> MapItems<TSource, TResult>(
        this Task<ResultPaged<TSource>> resultTask,
        Func<TSource, TResult> mapper,
        ProcessingOptions options = null)
    {
        if (mapper is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.MapItems(mapper, options);
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps elements in a paged Result task by applying an asynchronous transform function to each element.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="resultTask">The paged Result task containing the collection to map.</param>
    /// <param name="mapper">The async function to apply to each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new paged Result containing the transformed collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .MapItemsAsync(
    ///         async (user, ct) => await CreateUserDtoAsync(user, ct),
    ///         cancellationToken: cancellationToken
    ///     );
    ///
    /// // With processing options
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .MapItemsAsync(
    ///         async (user, ct) => await CreateUserDtoAsync(user, ct),
    ///         ProcessingOptions.Strict,
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TResult>> MapItemsAsync<TSource, TResult>(
        this Task<ResultPaged<TSource>> resultTask,
        Func<TSource, CancellationToken, Task<TResult>> mapper,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (mapper is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.MapItemsAsync(mapper, options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    #endregion

    #region Collection Iteration Operations

    /// <summary>
    ///     Executes an action on each element in a paged Result without changing the Result.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result containing the collection.</param>
    /// <param name="action">The action to execute on each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>The original paged Result if all operations succeeded, or a failure Result if any operations failed beyond the tolerance specified in options.</returns>
    /// <example>
    /// <code>
    /// // Basic foreach
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .ForEachItems(user => Console.WriteLine($"Processing user: {user.Name}"));
    ///
    /// // With processing options
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .ForEachItems(
    ///         user => ProcessUser(user),
    ///         ProcessingOptions.Strict
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<T> ForEach<T>(
        this ResultPaged<T> result,
        Action<T> action,
        ProcessingOptions options = null)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (action is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;
            var processedItems = new List<T>();

            foreach (var item in result.Value)
            {
                try
                {
                    action(item);
                    processedItems.Add(item);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error executing action on item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        processedItems.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Processing aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            if (errors.Count != 0 && (!options.MaxFailures.HasValue || failureCount > options.MaxFailures.Value))
            {
                return ResultPaged<T>.Failure(processedItems)
                    .WithErrors(errors)
                    .WithMessages(messages);
            }

            return ResultPaged<T>.Success(
                    processedItems,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(errors)
                .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message)
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Executes an async action on each element in a paged Result without changing the Result.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result containing the collection.</param>
    /// <param name="action">The async action to execute on each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original paged Result if all operations succeeded, or a failure Result if any operations failed beyond the tolerance specified in options.</returns>
    /// <example>
    /// <code>
    /// // Basic async foreach
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .ForEachItemsAsync(
    ///         async (user, ct) => await LogUserAccessAsync(user, ct),
    ///         cancellationToken: cancellationToken
    ///     );
    ///
    /// // With processing options
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .ForEachItemsAsync(
    ///         async (user, ct) => await ProcessUserAsync(user, ct),
    ///         new ProcessingOptions { MaxFailures = 3 },
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> ForEachAsync<T>(
        this ResultPaged<T> result,
        Func<T, CancellationToken, Task> action,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (action is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;
            var processedItems = new List<T>();

            foreach (var item in result.Value)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await action(item, cancellationToken);
                    processedItems.Add(item);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error executing async action on item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        processedItems.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Processing aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            if (errors.Count != 0 && (!options.MaxFailures.HasValue || failureCount > options.MaxFailures.Value))
            {
                return ResultPaged<T>.Failure(processedItems)
                    .WithErrors(errors)
                    .WithMessages(messages);
            }

            return ResultPaged<T>.Success(
                    processedItems,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(errors)
                .WithMessages(messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message)
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Executes an action on each element in a paged Result task without changing the Result.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="resultTask">The paged Result task containing the collection.</param>
    /// <param name="action">The action to execute on each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>The original paged Result if all operations succeeded, or a failure Result if any operations failed beyond the tolerance specified in options.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .ForEachItems(user => Console.WriteLine($"Processing user: {user.Name}"));
    ///
    /// // With processing options
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .ForEachItems(
    ///         user => LogUserAccess(user),
    ///         ProcessingOptions.Default
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> ForEach<T>(
        this Task<ResultPaged<T>> resultTask,
        Action<T> action,
        ProcessingOptions options = null)
    {
        if (action is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.ForEach(action, options);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an async action on each element in a paged Result task without changing the Result.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="resultTask">The paged Result task containing the collection.</param>
    /// <param name="action">The async action to execute on each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original paged Result if all operations succeeded, or a failure Result if any operations failed beyond the tolerance specified in options.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .ForEachItemsAsync(
    ///         async (user, ct) => await LogUserAccessAsync(user, ct),
    ///         cancellationToken: cancellationToken
    ///     );
    ///
    /// // With processing options
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .ForEachItemsAsync(
    ///         async (user, ct) => await ProcessUserAsync(user, ct),
    ///         ProcessingOptions.Strict,
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> ForEachAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<T, CancellationToken, Task> action,
        ProcessingOptions options = null,
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

            return await result.ForEachAsync(action, options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    #endregion

    #region Collection Traverse Operations

    /// <summary>
    ///     Traverses a sequence from a paged Result by applying an operation to each element.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result containing the sequence to traverse.</param>
    /// <param name="operation">The operation to apply to each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A paged Result containing the processed sequence or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// var result = pagedRepository.FindAllResult()
    ///     .TraverseItems(
    ///         item => repository.ValidateItem(item),
    ///         ProcessingOptions.Default
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<T> Traverse<T>(
        this ResultPaged<T> result,
        Func<T, Result<T>> operation,
        ProcessingOptions options = null)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<T>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (operation is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var results = new List<T>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    var operationResult = operation(item);

                    if (operationResult.IsSuccess)
                    {
                        results.Add(operationResult.Value);
                        messages.AddRange(operationResult.Messages);
                    }
                    else
                    {
                        failureCount++;
                        errors.AddRange(operationResult.Errors);
                        messages.AddRange(operationResult.Messages);

                        if (options.IncludeFailedItems)
                        {
                            results.Add(item);
                        }

                        if (!options.ContinueOnItemFailure ||
                            (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                        {
                            messages.Add($"Processing aborted after {failureCount} failures");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error traversing item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        results.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Processing aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = results.Count != 0 &&
                (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<T>.Success(
                        results,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<T>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Traverses a sequence from a paged Result by applying an async operation to each element.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result containing the sequence to traverse.</param>
    /// <param name="operation">The async operation to apply to each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A paged Result containing the processed sequence or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// var result = await pagedRepository.FindAllResult()
    ///     .TraverseItemsAsync(
    ///         async (item, ct) => await repository.ValidateItemAsync(item),
    ///         ProcessingOptions.Default,
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> TraverseAsync<T>(
        this ResultPaged<T> result,
        Func<T, CancellationToken, Task<Result<T>>> operation,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<T>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (operation is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var results = new List<T>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var operationResult = await operation(item, cancellationToken);

                    if (operationResult.IsSuccess)
                    {
                        results.Add(operationResult.Value);
                        messages.AddRange(operationResult.Messages);
                    }
                    else
                    {
                        failureCount++;
                        errors.AddRange(operationResult.Errors);
                        messages.AddRange(operationResult.Messages);

                        if (options.IncludeFailedItems)
                        {
                            results.Add(item);
                        }

                        if (!options.ContinueOnItemFailure ||
                            (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                        {
                            messages.Add($"Processing aborted after {failureCount} failures");
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error traversing item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        results.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Processing aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = results.Count != 0 &&
                (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<T>.Success(
                        results,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<T>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Traverses a sequence from a paged Result task by applying an operation to each element.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="resultTask">The paged Result task containing the sequence to traverse.</param>
    /// <param name="operation">The operation to apply to each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A paged Result containing the processed sequence or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// var result = await pagedRepository.FindAllResultAsync()
    ///     .TraverseItems(
    ///         item => repository.ValidateItem(item),
    ///         ProcessingOptions.Default
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> Traverse<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<T, Result<T>> operation,
        ProcessingOptions options = null)
    {
        if (resultTask is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Result task cannot be null"));
        }

        if (operation is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Traverse(operation, options);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Traverses a sequence from a paged Result task by applying an async operation to each element.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="resultTask">The paged Result task containing the sequence to traverse.</param>
    /// <param name="operation">The async operation to apply to each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A paged Result containing the processed sequence or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// var result = await pagedRepository.FindAllResultAsync()
    ///     .TraverseItemsAsync(
    ///         async (item, ct) => await repository.ValidateItemAsync(item),
    ///         ProcessingOptions.Default,
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> TraverseAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<T, CancellationToken, Task<Result<T>>> operation,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (resultTask is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Result task cannot be null"));
        }

        if (operation is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.TraverseAsync(operation, options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    #endregion

    #region Collection Binding Operations

    /// <summary>
    ///     Binds a paged collection Result to another paged collection Result by applying a binding function to each element.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="result">The paged Result containing the source collection.</param>
    /// <param name="binder">The function to bind each element to a new Result.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A new paged Result containing the bound collection or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// // Binding collections
    /// var result = ResultPaged{Department}.Success(departments, totalCount, page, pageSize)
    ///     .BindItems(
    ///         department => GetEmployeesResult(department.Id),
    ///         ProcessingOptions.Default
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<TResult> BindItems<TSource, TResult>(
        this ResultPaged<TSource> result,
        Func<TSource, Result<IEnumerable<TResult>>> binder,
        ProcessingOptions options = null)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<TResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (binder is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Binder cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var boundItems = new List<TResult>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    var bindResult = binder(item);

                    if (bindResult.IsSuccess)
                    {
                        boundItems.AddRange(bindResult.Value);
                        messages.AddRange(bindResult.Messages);
                    }
                    else
                    {
                        failureCount++;
                        errors.AddRange(bindResult.Errors);
                        messages.AddRange(bindResult.Messages);

                        if (!options.ContinueOnItemFailure ||
                            (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                        {
                            messages.Add($"Binding aborted after {failureCount} failures");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error binding item: {ex.Message}");

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Binding aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = boundItems.Count != 0 &&
                (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<TResult>.Success(
                        boundItems,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<TResult>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Binds a paged collection Result to another paged collection Result asynchronously by applying a binding function to each element.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="result">The paged Result containing the source collection.</param>
    /// <param name="binder">The async function to bind each element to a new Result.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new paged Result containing the bound collection or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// // Asynchronous binding of collections
    /// var result = await ResultPaged{Department}.Success(departments, totalCount, page, pageSize)
    ///     .BindItemsAsync(
    ///         async (department, ct) => await GetEmployeesResultAsync(department.Id, ct),
    ///         ProcessingOptions.Default,
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TResult>> BindItemsAsync<TSource, TResult>(
        this ResultPaged<TSource> result,
        Func<TSource, CancellationToken, Task<Result<IEnumerable<TResult>>>> binder,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<TResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (binder is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Binder cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var boundItems = new List<TResult>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var bindResult = await binder(item, cancellationToken);

                    if (bindResult.IsSuccess)
                    {
                        boundItems.AddRange(bindResult.Value);
                        messages.AddRange(bindResult.Messages);
                    }
                    else
                    {
                        failureCount++;
                        errors.AddRange(bindResult.Errors);
                        messages.AddRange(bindResult.Messages);

                        if (!options.ContinueOnItemFailure ||
                            (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                        {
                            messages.Add($"Binding aborted after {failureCount} failures");
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error binding item: {ex.Message}");

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Binding aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = boundItems.Count != 0 &&
                (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<TResult>.Success(
                        boundItems,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<TResult>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Binds a paged collection Result task to another paged collection Result by applying a binding function to each element.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="resultTask">The paged Result task containing the source collection.</param>
    /// <param name="binder">The function to bind each element to a new Result.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A new paged Result containing the bound collection or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedDepartmentsAsync(page, pageSize)
    ///     .BindItems(
    ///         department => GetEmployeesResult(department.Id),
    ///         ProcessingOptions.Default
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TResult>> BindItems<TSource, TResult>(
        this Task<ResultPaged<TSource>> resultTask,
        Func<TSource, Result<IEnumerable<TResult>>> binder,
        ProcessingOptions options = null)
    {
        if (resultTask is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Result task cannot be null"));
        }

        if (binder is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Binder cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.BindItems(binder, options);
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Binds a paged collection Result task to another paged collection Result asynchronously by applying a binding function to each element.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="resultTask">The paged Result task containing the source collection.</param>
    /// <param name="binder">The async function to bind each element to a new Result.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new paged Result containing the bound collection or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedDepartmentsAsync(page, pageSize)
    ///     .BindItemsAsync(
    ///         async (department, ct) => await GetEmployeesResultAsync(department.Id, ct),
    ///         ProcessingOptions.Default,
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TResult>> BindItemsAsync<TSource, TResult>(
        this Task<ResultPaged<TSource>> resultTask,
        Func<TSource, CancellationToken, Task<Result<IEnumerable<TResult>>>> binder,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (resultTask is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Result task cannot be null"));
        }

        if (binder is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Binder cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.BindItemsAsync(binder, options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Flattens a collection of paged Result collections into a single paged Result collection.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="results">The collection of paged Result collections to flatten.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A new paged Result containing all elements from all Results or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// var departmentResults = departments.Select(d => GetPagedEmployeesResult(d.Id));
    /// var allEmployees = departmentResults.FlattenItems(ProcessingOptions.Default);
    /// </code>
    /// </example>
    public static ResultPaged<T> Flatten<T>(
        this IEnumerable<ResultPaged<T>> results,
        ProcessingOptions options = null)
    {
        if (results is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Results collection cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var flattenedItems = new List<T>();
            var errors = new List<IResultError>();
            var messages = new List<string>();
            var failureCount = 0;

            // For pagination metadata, we'll use values from the first successful result
            // or default values if there are no successful results
            long totalCount = 0;
            var currentPage = 1;
            var pageSize = 10;
            var metadataSet = false;

            foreach (var result in results)
            {
                if (result.IsSuccess)
                {
                    flattenedItems.AddRange(result.Value);
                    messages.AddRange(result.Messages);

                    // Set pagination metadata from the first successful result
                    if (!metadataSet)
                    {
                        totalCount = result.TotalCount;
                        currentPage = result.CurrentPage;
                        pageSize = result.PageSize;
                        metadataSet = true;
                    }
                    else
                    {
                        // Update total count as we process more results
                        totalCount += result.TotalCount;
                    }
                }
                else
                {
                    failureCount++;
                    errors.AddRange(result.Errors);
                    messages.AddRange(result.Messages);

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Flattening aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = flattenedItems.Count != 0 &&
                (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<T>.Success(
                        flattenedItems,
                        totalCount,
                        currentPage,
                        pageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<T>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Flattens a collection of paged Result collection tasks into a single paged Result collection.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="resultTasks">The collection of paged Result collection tasks to flatten.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new paged Result containing all elements from all Results or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// var departmentTasks = departments.Select(d => GetPagedEmployeesResultAsync(d.Id));
    /// var allEmployees = await departmentTasks.FlattenItemsAsync(ProcessingOptions.Default, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> FlattenAsync<T>(
        this IEnumerable<Task<ResultPaged<T>>> resultTasks,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (resultTasks is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Result tasks collection cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var results = new List<ResultPaged<T>>();

            // First await all tasks
            foreach (var task in resultTasks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await task;
                results.Add(result);
            }

            // Then flatten the results
            return results.Flatten(options);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Selects many elements from a paged collection Result by applying a selector function to each element.
    ///     This is equivalent to the LINQ SelectMany operation but maintains the Result context.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="result">The paged Result containing the source collection.</param>
    /// <param name="selector">The function to apply to each element returning a collection.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A new paged Result containing the flattened collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{Department}.Success(departments, totalCount, page, pageSize)
    ///     .SelectManyItems(
    ///         department => department.Employees,
    ///         ProcessingOptions.Default
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<TResult> SelectMany<TSource, TResult>(
        this ResultPaged<TSource> result,
        Func<TSource, IEnumerable<TResult>> selector,
        ProcessingOptions options = null)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<TResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (selector is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Selector cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var selectedItems = new List<TResult>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    var collection = selector(item);
                    if (collection != null)
                    {
                        selectedItems.AddRange(collection);
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error selecting from item: {ex.Message}");

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Selection aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<TResult>.Success(
                        selectedItems,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<TResult>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Selects many elements from a paged collection Result task by applying a selector function to each element.
    ///     This is equivalent to the LINQ SelectMany operation but maintains the Result context.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="resultTask">The paged Result task containing the source collection.</param>
    /// <param name="selector">The function to apply to each element returning a collection.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A new paged Result containing the flattened collection or the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedDepartmentsAsync(page, pageSize)
    ///     .SelectManyItems(
    ///         department => department.Employees,
    ///         ProcessingOptions.Default
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TResult>> SelectMany<TSource, TResult>(
        this Task<ResultPaged<TSource>> resultTask,
        Func<TSource, IEnumerable<TResult>> selector,
        ProcessingOptions options = null)
    {
        if (selector is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Selector cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.SelectMany(selector, options);
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    #endregion

    #region Collection Tap and Do Operations

    /// <summary>
    ///     Executes an action with each element in a paged collection Result without changing the Result.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result containing the collection.</param>
    /// <param name="action">The action to execute on each element.</param>
    /// <returns>The original paged Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .TapItems(user => _logger.LogInformation($"User: {user.Id}, {user.Email}"));
    /// </code>
    /// </example>
    public static ResultPaged<T> TapItems<T>(
        this ResultPaged<T> result,
        Action<T> action)
    {
        if (!result.IsSuccess || action is null)
        {
            return result;
        }

        try
        {
            foreach (var item in result.Value)
            {
                action(item);
            }

            return result;
        }
        catch
        {
            // Tap should never change the Result, so we ignore any exceptions
            return result;
        }
    }

    /// <summary>
    ///     Executes an async action with each element in a paged collection Result without changing the Result.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result containing the collection.</param>
    /// <param name="action">The async action to execute on each element.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original paged Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .TapItemsAsync(
    ///         async (user, ct) => await _logger.LogInformationAsync($"User: {user.Id}, {user.Email}", ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> TapItemsAsync<T>(
        this ResultPaged<T> result,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || action is null)
        {
            return result;
        }

        try
        {
            foreach (var item in result.Value)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await action(item, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Tap should never change the Result, so we ignore any exceptions
                }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Tap should never change the Result, so we ignore any exceptions
            return result;
        }
    }

    /// <summary>
    ///     Executes an action with each element in a paged collection Result task without changing the Result.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="resultTask">The paged Result task containing the collection.</param>
    /// <param name="action">The action to execute on each element.</param>
    /// <returns>The original paged Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .TapItems(user => _logger.LogInformation($"User: {user.Id}, {user.Email}"));
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> TapItems<T>(
        this Task<ResultPaged<T>> resultTask,
        Action<T> action)
    {
        try
        {
            var result = await resultTask;
            return result.TapItems(action);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an async action with each element in a paged collection Result task without changing the Result.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="resultTask">The paged Result task containing the collection.</param>
    /// <param name="action">The async action to execute on each element.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original paged Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .TapItemsAsync(
    ///         async (user, ct) => await _logger.LogInformationAsync($"User: {user.Id}, {user.Email}", ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> TapItemsAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await resultTask;
            return await result.TapItemsAsync(action, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps and taps simultaneously, applying a transform function and executing an action with the transformed value.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="result">The paged Result containing the collection to map.</param>
    /// <param name="mapper">The function to transform each element.</param>
    /// <param name="action">The action to execute with each transformed element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>A new paged Result containing the transformed collection.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .TeeMapItems(
    ///         user => new UserDto(user.Id, user.Name),
    ///         dto => _logger.LogInformation($"Created DTO for user: {dto.Id}"),
    ///         ProcessingOptions.Default
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<TResult> TeeMapItems<TSource, TResult>(
        this ResultPaged<TSource> result,
        Func<TSource, TResult> mapper,
        Action<TResult> action,
        ProcessingOptions options = null)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<TResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (mapper is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        if (action is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var mappedItems = new List<TResult>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    var mappedItem = mapper(item);
                    action(mappedItem);
                    mappedItems.Add(mappedItem);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error in TeeMap operation: {ex.Message}");

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"TeeMap aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = mappedItems.Count != 0 &&
                (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<TResult>.Success(
                        mappedItems,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<TResult>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps and taps simultaneously, applying a transform function and executing an async action with the transformed value.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    /// <param name="result">The paged Result containing the collection to map.</param>
    /// <param name="mapper">The function to transform each element.</param>
    /// <param name="action">The async action to execute with each transformed element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new paged Result containing the transformed collection.</returns>
    /// <example>
    /// <code>
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .TeeMapItemsAsync(
    ///         user => new UserDto(user.Id, user.Name),
    ///         async (dto, ct) => await _cache.StoreUserDtoAsync(dto, ct),
    ///         cancellationToken: cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TResult>> TeeMapItemsAsync<TSource, TResult>(
        this ResultPaged<TSource> result,
        Func<TSource, TResult> mapper,
        Func<TResult, CancellationToken, Task> action,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<TResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (mapper is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        if (action is null)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var mappedItems = new List<TResult>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var mappedItem = mapper(item);
                    await action(mappedItem, cancellationToken);
                    mappedItems.Add(mappedItem);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error in TeeMapAsync operation: {ex.Message}");

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"TeeMap aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            var isSuccess = mappedItems.Count != 0 &&
                (!options.MaxFailures.HasValue || failureCount <= options.MaxFailures.Value);

            return isSuccess
                ? ResultPaged<TResult>.Success(
                        mappedItems,
                        result.TotalCount,
                        result.CurrentPage,
                        result.PageSize)
                    .WithErrors(errors)
                    .WithMessages(messages)
                : ResultPaged<TResult>.Failure()
                    .WithErrors(errors)
                    .WithMessages(messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an action on the pagination details without touching the original paged Result.
    ///     Useful for logging or monitoring pagination information.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result.</param>
    /// <param name="action">The action to execute on the pagination details.</param>
    /// <returns>The original paged Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .TapPaginationItems(p => _logger.LogInformation(
    ///         $"Page {p.CurrentPage} of {p.TotalPages} ({p.TotalCount} total items)"
    ///     ));
    /// </code>
    /// </example>
    public static ResultPaged<T> TapItems<T>(
        this ResultPaged<T> result,
        Action<ResultPaged<T>> action)
    {
        if (action is null)
        {
            return result;
        }

        try
        {
            action(result);
            return result;
        }
        catch
        {
            // Tap should never change the Result, so we ignore any exceptions
            return result;
        }
    }

    /// <summary>
    ///     Executes an async action on the pagination details without touching the original paged Result.
    ///     Useful for async logging or monitoring pagination information.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result.</param>
    /// <param name="action">The async action to execute on the pagination details.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original paged Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .TapPaginationItemsAsync(
    ///         async (p, ct) => await _metrics.RecordPaginationAsync(
    ///             p.CurrentPage, p.TotalPages, p.TotalCount, ct
    ///         ),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> TapPaginationItemsAsync<T>(
        this ResultPaged<T> result,
        Func<ResultPaged<T>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            return result;
        }

        try
        {
            await action(result, cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Tap should never change the Result, so we ignore any exceptions
            return result;
        }
    }

    /// <summary>
    ///     Executes an action without touching the original paged collection Result.
    ///     Useful for executing side effects in a chain of operations.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The original paged Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .DoItems(() => _logger.LogInformation("Starting user processing"))
    ///     .MapItems(user => new UserDto(user));
    /// </code>
    /// </example>
    public static ResultPaged<T> DoItems<T>(
        this ResultPaged<T> result,
        Action action)
    {
        if (action is null)
        {
            return result;
        }

        try
        {
            action();
            return result;
        }
        catch
        {
            // Do should never change the Result, so we ignore any exceptions
            return result;
        }
    }

    /// <summary>
    ///     Executes an async action without touching the original paged collection Result.
    ///     Useful for executing async side effects in a chain of operations.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result.</param>
    /// <param name="action">The async action to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original paged Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .DoItemsAsync(
    ///         async (ct) => await _logger.LogInformationAsync("Starting user processing", ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> DoItemsAsync<T>(
        this ResultPaged<T> result,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            return result;
        }

        try
        {
            await action(cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Do should never change the Result, so we ignore any exceptions
            return result;
        }
    }

    /// <summary>
    ///     Executes an action without touching the original paged collection Result task.
    ///     Useful for executing side effects in a chain of operations.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="resultTask">The paged Result task.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The original paged Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .DoItems(() => _logger.LogInformation("Starting user processing"))
    ///     .MapItems(user => new UserDto(user));
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> DoItems<T>(
        this Task<ResultPaged<T>> resultTask,
        Action action)
    {
        try
        {
            var result = await resultTask;
            return result.DoItems(action);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an async action without touching the original paged collection Result task.
    ///     Useful for executing async side effects in a chain of operations.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="resultTask">The paged Result task.</param>
    /// <param name="action">The async action to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original paged Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPagedUsersAsync(page, pageSize)
    ///     .DoItemsAsync(
    ///         async (ct) => await _logger.LogInformationAsync("Starting user processing", ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> DoItemsAsync<T>(
        this Task<ResultPaged<T>> resultTask,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await resultTask;
            return await result.DoItemsAsync(action, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    #endregion

    #region Collection Validation Operations

    /// <summary>
    ///     Validates each item in a paged collection using a specified validator function.
    /// </summary>
    /// <typeparam name="T">The type of the elements to validate.</typeparam>
    /// <param name="result">The paged Result containing the collection to validate.</param>
    /// <param name="validator">The function to validate each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>The original paged Result if all validations succeed; otherwise, a failure with validation errors.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .ValidateItems(
    ///         user => user.Email.Contains("@")
    ///             ? Result{User}.Success(user)
    ///             : Result{User}.Failure().WithError(new ValidationError("Invalid email format")),
    ///         ProcessingOptions.Default
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<T> ValidateItems<T>(
        this ResultPaged<T> result,
        Func<T, Result<T>> validator,
        ProcessingOptions options = null)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (validator is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Validator cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var validItems = new List<T>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    var validationResult = validator(item);

                    if (validationResult.IsSuccess)
                    {
                        validItems.Add(validationResult.Value);
                        messages.AddRange(validationResult.Messages);
                    }
                    else
                    {
                        failureCount++;
                        errors.AddRange(validationResult.Errors);
                        messages.AddRange(validationResult.Messages);

                        if (options.IncludeFailedItems)
                        {
                            validItems.Add(item);
                        }

                        if (!options.ContinueOnItemFailure ||
                            (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                        {
                            messages.Add($"Validation aborted after {failureCount} failures");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error validating item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        validItems.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Validation aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            if (errors.Any() && (!options.MaxFailures.HasValue || failureCount > options.MaxFailures.Value))
            {
                return ResultPaged<T>.Failure(validItems)
                    .WithErrors(errors)
                    .WithMessages(messages);
            }

            return ResultPaged<T>.Success(
                    validItems,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(errors)
                .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message)
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Validates each item in a paged collection using a specified async validator function.
    /// </summary>
    /// <typeparam name="T">The type of the elements to validate.</typeparam>
    /// <param name="result">The paged Result containing the collection to validate.</param>
    /// <param name="validator">The async function to validate each element.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original paged Result if all validations succeed; otherwise, a failure with validation errors.</returns>
    /// <example>
    /// <code>
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .ValidateItemsAsync(
    ///         async (user, ct) => await ValidateUserAsync(user, ct),
    ///         ProcessingOptions.Default,
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> ValidateItemsAsync<T>(
        this ResultPaged<T> result,
        Func<T, CancellationToken, Task<Result<T>>> validator,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (validator is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Validator cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var validItems = new List<T>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var validationResult = await validator(item, cancellationToken);

                    if (validationResult.IsSuccess)
                    {
                        validItems.Add(validationResult.Value);
                        messages.AddRange(validationResult.Messages);
                    }
                    else
                    {
                        failureCount++;
                        errors.AddRange(validationResult.Errors);
                        messages.AddRange(validationResult.Messages);

                        if (options.IncludeFailedItems)
                        {
                            validItems.Add(item);
                        }

                        if (!options.ContinueOnItemFailure ||
                            (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                        {
                            messages.Add($"Validation aborted after {failureCount} failures");
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error validating item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        validItems.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Validation aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            if (errors.Any() && (!options.MaxFailures.HasValue || failureCount > options.MaxFailures.Value))
            {
                return ResultPaged<T>.Failure(validItems)
                    .WithErrors(errors)
                    .WithMessages(messages);
            }

            return ResultPaged<T>.Success(
                    validItems,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(errors)
                .WithMessages(messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message)
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Validates each item in a paged collection using a predicate function.
    /// </summary>
    /// <typeparam name="T">The type of the elements to validate.</typeparam>
    /// <param name="result">The paged Result containing the collection to validate.</param>
    /// <param name="predicate">The predicate function that must return true for valid items.</param>
    /// <param name="errorFactory">The function to create an error for invalid items. If null, a default error is created.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>The original paged Result if all validations succeed; otherwise, a failure with validation errors.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .ValidateItemsWhere(
    ///         user => user.Age >= 18,
    ///         user => new ValidationError($"User {user.Id} is under 18"),
    ///         ProcessingOptions.Default
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<T> ValidateItemsWhere<T>(
        this ResultPaged<T> result,
        Func<T, bool> predicate,
        Func<T, IResultError> errorFactory = null,
        ProcessingOptions options = null)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        errorFactory ??= item => new ValidationError($"Item failed validation: {item}");

        try
        {
            options ??= ProcessingOptions.Default;
            var validItems = new List<T>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    if (predicate(item))
                    {
                        validItems.Add(item);
                    }
                    else
                    {
                        failureCount++;
                        var error = errorFactory(item);
                        errors.Add(error);
                        messages.Add($"Validation failed: {error.Message}");

                        if (options.IncludeFailedItems)
                        {
                            validItems.Add(item);
                        }

                        if (!options.ContinueOnItemFailure ||
                            (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                        {
                            messages.Add($"Validation aborted after {failureCount} failures");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error validating item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        validItems.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Validation aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            if (errors.Any() && (!options.MaxFailures.HasValue || failureCount > options.MaxFailures.Value))
            {
                return ResultPaged<T>.Failure(validItems)
                    .WithErrors(errors)
                    .WithMessages(messages);
            }

            return ResultPaged<T>.Success(
                    validItems,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(errors)
                .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message)
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Validates each item in a paged collection using an async predicate function.
    /// </summary>
    /// <typeparam name="T">The type of the elements to validate.</typeparam>
    /// <param name="result">The paged Result containing the collection to validate.</param>
    /// <param name="predicate">The async predicate function that must return true for valid items.</param>
    /// <param name="errorFactory">The function to create an error for invalid items. If null, a default error is created.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original paged Result if all validations succeed; otherwise, a failure with validation errors.</returns>
    /// <example>
    /// <code>
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .ValidateItemsWhereAsync(
    ///         async (user, ct) => await IsUserEligibleAsync(user, ct),
    ///         user => new ValidationError($"User {user.Id} is not eligible"),
    ///         ProcessingOptions.Default,
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> ValidateItemsWhereAsync<T>(
        this ResultPaged<T> result,
        Func<T, CancellationToken, Task<bool>> predicate,
        Func<T, IResultError> errorFactory = null,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        errorFactory ??= item => new ValidationError($"Item failed validation: {item}");

        try
        {
            options ??= ProcessingOptions.Default;
            var validItems = new List<T>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await predicate(item, cancellationToken))
                    {
                        validItems.Add(item);
                    }
                    else
                    {
                        failureCount++;
                        var error = errorFactory(item);
                        errors.Add(error);
                        messages.Add($"Validation failed: {error.Message}");

                        if (options.IncludeFailedItems)
                        {
                            validItems.Add(item);
                        }

                        if (!options.ContinueOnItemFailure ||
                            (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                        {
                            messages.Add($"Validation aborted after {failureCount} failures");
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error validating item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        validItems.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Validation aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            if (errors.Any() && (!options.MaxFailures.HasValue || failureCount > options.MaxFailures.Value))
            {
                return ResultPaged<T>.Failure(validItems)
                    .WithErrors(errors)
                    .WithMessages(messages);
            }

            return ResultPaged<T>.Success(
                    validItems,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(errors)
                .WithMessages(messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message)
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Validates each item in a paged collection using a validator from the FluentValidation library.
    /// </summary>
    /// <typeparam name="T">The type of the elements to validate.</typeparam>
    /// <param name="result">The paged Result containing the collection to validate.</param>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <returns>The original paged Result if all validations succeed; otherwise, a failure with validation errors.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .ValidateItemsAll(new UserValidator());
    /// </code>
    /// </example>
    public static ResultPaged<T> ValidateItems<T>(
        this ResultPaged<T> result,
        IValidator<T> validator,
        ProcessingOptions options = null)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (validator is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Validator cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var validItems = new List<T>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    var validationResult = validator.Validate(item);

                    if (validationResult.IsValid)
                    {
                        validItems.Add(item);
                    }
                    else
                    {
                        failureCount++;
                        errors.Add(new FluentValidationError(validationResult));
                        messages.Add($"Validation failed for item");

                        if (options.IncludeFailedItems)
                        {
                            validItems.Add(item);
                        }

                        if (!options.ContinueOnItemFailure ||
                            (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                        {
                            messages.Add($"Validation aborted after {failureCount} failures");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error validating item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        validItems.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Validation aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            if (errors.Any() && (!options.MaxFailures.HasValue || failureCount > options.MaxFailures.Value))
            {
                return ResultPaged<T>.Failure(validItems)
                    .WithErrors(errors)
                    .WithMessages(messages);
            }

            return ResultPaged<T>.Success(
                    validItems,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(errors)
                .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message)
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Validates each item in a paged collection asynchronously using a validator from the FluentValidation library.
    /// </summary>
    /// <typeparam name="T">The type of the elements to validate.</typeparam>
    /// <param name="result">The paged Result containing the collection to validate.</param>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Options for handling partial success scenarios.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original paged Result if all validations succeed; otherwise, a failure with validation errors.</returns>
    /// <example>
    /// <code>
    /// var result = await ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .ValidateItemsAllAsync(new UserValidator(), cancellationToken: cancellationToken);
    /// </code>
    /// </example>
    public static async Task<ResultPaged<T>> ValidateItemsAsync<T>(
        this ResultPaged<T> result,
        IValidator<T> validator,
        ProcessingOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (validator is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Validator cannot be null"));
        }

        try
        {
            options ??= ProcessingOptions.Default;
            var validItems = new List<T>();
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);
            var failureCount = 0;

            foreach (var item in result.Value)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var validationResult = await validator.ValidateAsync(item, cancellationToken);

                    if (validationResult.IsValid)
                    {
                        validItems.Add(item);
                    }
                    else
                    {
                        failureCount++;
                        errors.Add(new FluentValidationError(validationResult));
                        messages.Add($"Validation failed for item");

                        if (options.IncludeFailedItems)
                        {
                            validItems.Add(item);
                        }

                        if (!options.ContinueOnItemFailure ||
                            (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                        {
                            messages.Add($"Validation aborted after {failureCount} failures");
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var error = Result.Settings.ExceptionErrorFactory(ex);
                    errors.Add(error);
                    messages.Add($"Error validating item: {ex.Message}");

                    if (options.IncludeFailedItems)
                    {
                        validItems.Add(item);
                    }

                    if (!options.ContinueOnItemFailure ||
                        (options.MaxFailures.HasValue && failureCount > options.MaxFailures.Value))
                    {
                        messages.Add($"Validation aborted after {failureCount} failures");
                        break;
                    }
                }
            }

            if (errors.Any() && (!options.MaxFailures.HasValue || failureCount > options.MaxFailures.Value))
            {
                return ResultPaged<T>.Failure(validItems)
                    .WithErrors(errors)
                    .WithMessages(messages);
            }

            return ResultPaged<T>.Success(
                    validItems,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithErrors(errors)
                .WithMessages(messages);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message)
                .WithMessages(result.Messages);
        }
    }

    #endregion

    #region Additional Collection Operations

    /// <summary>
    ///     Ensures that all elements in a paged collection satisfy a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result containing the collection to check.</param>
    /// <param name="predicate">The condition that must be true for all elements.</param>
    /// <param name="error">The error to return if the predicate fails for any element.</param>
    /// <returns>The original paged Result if all elements satisfy the predicate; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{Order}.Success(orders, totalCount, page, pageSize)
    ///     .EnsureAllItems(
    ///         order => order.Total > 0,
    ///         new ValidationError("All orders must have a positive total")
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<T> EnsureAll<T>(
        this ResultPaged<T> result,
        Func<T, bool> predicate,
        IResultError error)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            if (!result.Value.All(predicate))
            {
                return ResultPaged<T>.Failure(result.Value)
                    .WithError(error)
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Ensures that at least one element in a paged collection satisfies a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result containing the collection to check.</param>
    /// <param name="predicate">The condition that must be true for at least one element.</param>
    /// <param name="error">The error to return if the predicate fails for all elements.</param>
    /// <returns>The original paged Result if any element satisfies the predicate; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{PaymentMethod}.Success(paymentMethods, totalCount, page, pageSize)
    ///     .EnsureAnyItems(
    ///         method => method.IsActive,
    ///         new ValidationError("At least one active payment method is required")
    ///     );
    /// </code>
    /// </example>
    public static ResultPaged<T> EnsureAny<T>(
        this ResultPaged<T> result,
        Func<T, bool> predicate,
        IResultError error)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (predicate is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            if (!result.Value.Any(predicate))
            {
                return ResultPaged<T>.Failure(result.Value)
                    .WithError(error)
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Ensures that the paged collection is not empty.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result containing the collection to check.</param>
    /// <param name="error">The error to return if the collection is empty.</param>
    /// <returns>The original paged Result if the collection contains elements; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{Product}.Success(products, totalCount, page, pageSize)
    ///     .EnsureNotEmptyItems(new ValidationError("No products found"));
    /// </code>
    /// </example>
    public static ResultPaged<T> EnsureNotEmpty<T>(
        this ResultPaged<T> result,
        IResultError error)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        try
        {
            if (!result.Value.Any())
            {
                return ResultPaged<T>.Failure(result.Value)
                    .WithError(error)
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Ensures that the paged collection contains exactly a specified number of elements.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result containing the collection to check.</param>
    /// <param name="count">The exact number of expected elements.</param>
    /// <param name="error">The error to return if the count doesn't match.</param>
    /// <returns>The original paged Result if the collection contains exactly the specified number of elements; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{Seat}.Success(selectedSeats, totalCount, page, pageSize)
    ///     .EnsureCountItems(2, new ValidationError("Exactly 2 seats must be selected"));
    /// </code>
    /// </example>
    public static ResultPaged<T> EnsureCount<T>(
        this ResultPaged<T> result,
        int count,
        IResultError error)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        try
        {
            var actualCount = result.Value.Count();
            if (actualCount != count)
            {
                return ResultPaged<T>.Failure(result.Value)
                    .WithError(error)
                    .WithMessage($"Expected {count} items, but found {actualCount}")
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure(result.Value)
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Chunks a paged collection into smaller collections of a specified size.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="result">The paged Result containing the collection to chunk.</param>
    /// <param name="size">The size of each chunk.</param>
    /// <returns>A new paged Result containing chunks of the original collection.</returns>
    /// <example>
    /// <code>
    /// // Break a large paged collection into smaller batches for processing
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .ChunkItems(100);
    ///
    /// foreach (var batch in result.Value)
    /// {
    ///     ProcessUserBatch(batch);
    /// }
    /// </code>
    /// </example>
    public static ResultPaged<IEnumerable<T>> Chunk<T>(
        this ResultPaged<T> result,
        int size)
    {
        if (!result.IsSuccess)
        {
            return ResultPaged<IEnumerable<T>>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (size <= 0)
        {
            return ResultPaged<IEnumerable<T>>.Failure()
                .WithError(new Error("Chunk size must be greater than zero"));
        }

        try
        {
            var chunks = new List<List<T>>();
            var currentChunk = new List<T>(size);

            foreach (var item in result.Value)
            {
                if (currentChunk.Count == size)
                {
                    chunks.Add(currentChunk);
                    currentChunk = new List<T>(size);
                }

                currentChunk.Add(item);
            }

            if (currentChunk.Count > 0)
            {
                chunks.Add(currentChunk);
            }

            return ResultPaged<IEnumerable<T>>.Success(
                    chunks,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<IEnumerable<T>>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Retrieves distinct elements from a paged collection based on a specified key selector.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <typeparam name="TKey">The type of the key used for distinctness comparison.</typeparam>
    /// <param name="result">The paged Result containing the collection.</param>
    /// <param name="keySelector">The function to extract the key for each element.</param>
    /// <returns>A new paged Result containing the distinct elements from the original collection.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{Product}.Success(products, totalCount, page, pageSize)
    ///     .DistinctByItems(product => product.CategoryId);
    /// </code>
    /// </example>
    public static ResultPaged<T> DistinctBy<T, TKey>(
        this ResultPaged<T> result,
        Func<T, TKey> keySelector)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (keySelector is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Key selector cannot be null"));
        }

        try
        {
            var distinctItems = result.Value
                .GroupBy(keySelector)
                .Select(group => group.First())
                .ToList();

            return ResultPaged<T>.Success(
                    distinctItems,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Attempts to execute an operation for each item in a paged collection until one succeeds.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="result">The paged Result containing the collection to process.</param>
    /// <param name="operation">The operation to try for each element.</param>
    /// <returns>The first successful Result from any operation, or a failure with all collected errors.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{PaymentMethod}.Success(paymentMethods, totalCount, page, pageSize)
    ///     .FirstItems(method => ProcessPayment(method, amount));
    /// </code>
    /// </example>
    public static Result<TResult> First<T, TResult>(
        this ResultPaged<T> result,
        Func<T, Result<TResult>> operation)
    {
        if (!result.IsSuccess)
        {
            return Result<TResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (operation is null)
        {
            return Result<TResult>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);

            foreach (var item in result.Value)
            {
                var operationResult = operation(item);

                if (operationResult.IsSuccess)
                {
                    return operationResult.WithMessages(messages);
                }

                errors.AddRange(operationResult.Errors);
                messages.AddRange(operationResult.Messages);
            }

            return Result<TResult>.Failure()
                .WithErrors(errors)
                .WithMessages(messages);
        }
        catch (Exception ex)
        {
            return Result<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Attempts to execute an async operation for each item in a paged collection until one succeeds.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="result">The paged Result containing the collection to process.</param>
    /// <param name="operation">The async operation to try for each element.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The first successful Result from any operation, or a failure with all collected errors.</returns>
    /// <example>
    /// <code>
    /// var result = await ResultPaged{PaymentMethod}.Success(paymentMethods, totalCount, page, pageSize)
    ///     .FirstItemsAsync(
    ///         async (method, ct) => await ProcessPaymentAsync(method, amount, ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<TResult>> FirstAsync<T, TResult>(
        this ResultPaged<T> result,
        Func<T, CancellationToken, Task<Result<TResult>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return Result<TResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (operation is null)
        {
            return Result<TResult>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var errors = new List<IResultError>();
            var messages = new List<string>(result.Messages);

            foreach (var item in result.Value)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var operationResult = await operation(item, cancellationToken);

                    if (operationResult.IsSuccess)
                    {
                        return operationResult.WithMessages(messages);
                    }

                    errors.AddRange(operationResult.Errors);
                    messages.AddRange(operationResult.Messages);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add(Result.Settings.ExceptionErrorFactory(ex));
                    messages.Add($"Error processing item: {ex.Message}");
                }
            }

            return Result<TResult>.Failure()
                .WithErrors(errors)
                .WithMessages(messages);
        }
        catch (OperationCanceledException)
        {
            return Result<TResult>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<TResult>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    ///// <summary>
    /////     Groups elements in a paged collection by a specified key selector.
    ///// </summary>
    ///// <typeparam name="T">The type of the elements.</typeparam>
    ///// <typeparam name="TKey">The type of the grouping key.</typeparam>
    ///// <param name="result">The paged Result containing the collection to group.</param>
    ///// <param name="keySelector">The function to extract the key for each element.</param>
    ///// <returns>A new paged Result containing the grouped elements.</returns>
    ///// <example>
    ///// <code>
    ///// var result = ResultPaged{Order}.Success(orders, totalCount, page, pageSize)
    /////     .GroupByItems(order => order.CustomerId);
    /////
    ///// foreach (var customerGroup in result.Value)
    ///// {
    /////     Console.WriteLine($"Customer {customerGroup.Key} has {customerGroup.Count()} orders");
    ///// }
    ///// </code>
    ///// </example>
    //public static ResultPaged<IEnumerable<IGrouping<TKey, T>>> GroupByItems<T, TKey>(
    //this ResultPaged<T> result,
    //Func<T, TKey> keySelector)
    //{
    //    if (!result.IsSuccess)
    //    {
    //        return ResultPaged<IEnumerable<IGrouping<TKey, T>>>.Failure()
    //            .WithErrors(result.Errors)
    //            .WithMessages(result.Messages);
    //    }

    //    if (keySelector is null)
    //    {
    //        return ResultPaged<IEnumerable<IGrouping<TKey, T>>>.Failure()
    //            .WithError(new Error("Key selector cannot be null"));
    //    }

    //    try
    //    {
    //        var groupedItems = result.Value.GroupBy(keySelector).ToList();

    //        return ResultPaged<IEnumerable<IGrouping<TKey, T>>>.Success(
    //                groupedItems,
    //                result.TotalCount,
    //                result.CurrentPage,
    //                result.PageSize)
    //            .WithMessages(result.Messages);
    //    }
    //    catch (Exception ex)
    //    {
    //        return ResultPaged<IEnumerable<IGrouping<TKey, T>>>.Failure()
    //            .WithError(new ExceptionError(ex))
    //            .WithMessage(ex.Message);
    //    }
    //}

    /// <summary>
    ///     Aggregates the elements of a paged collection using a specified aggregation function.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <typeparam name="TAccumulate">The type of the accumulator value.</typeparam>
    /// <param name="result">The paged Result containing the collection to aggregate.</param>
    /// <param name="seed">The initial accumulator value.</param>
    /// <param name="func">The function to apply to each element.</param>
    /// <returns>A new Result containing the aggregated value.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{OrderLine}.Success(orderLines, totalCount, page, pageSize)
    ///     .AggregateItems(
    ///         0.0m,
    ///         (total, line) => total + line.Quantity * line.UnitPrice
    ///     );
    /// </code>
    /// </example>
    public static Result<TAccumulate> Aggregate<T, TAccumulate>(
        this ResultPaged<T> result,
        TAccumulate seed,
        Func<TAccumulate, T, TAccumulate> func)
    {
        if (!result.IsSuccess)
        {
            return Result<TAccumulate>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (func is null)
        {
            return Result<TAccumulate>.Failure()
                .WithError(new Error("Aggregation function cannot be null"));
        }

        try
        {
            var aggregatedValue = result.Value.Aggregate(seed, func);

            return Result<TAccumulate>.Success(aggregatedValue)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<TAccumulate>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Orders the elements of a paged collection according to a key.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <typeparam name="TKey">The type of the ordering key.</typeparam>
    /// <param name="result">The paged Result containing the collection to order.</param>
    /// <param name="keySelector">The function to extract the key for each element.</param>
    /// <returns>A new paged Result containing the ordered collection.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{User}.Success(users, totalCount, page, pageSize)
    ///     .OrderByItems(user => user.LastName);
    /// </code>
    /// </example>
    public static ResultPaged<T> OrderBy<T, TKey>(
        this ResultPaged<T> result,
        Func<T, TKey> keySelector)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (keySelector is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Key selector cannot be null"));
        }

        try
        {
            var orderedItems = result.Value.OrderBy(keySelector).ToList();

            return ResultPaged<T>.Success(
                    orderedItems,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Orders the elements of a paged collection in descending order according to a key.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <typeparam name="TKey">The type of the ordering key.</typeparam>
    /// <param name="result">The paged Result containing the collection to order.</param>
    /// <param name="keySelector">The function to extract the key for each element.</param>
    /// <returns>A new paged Result containing the ordered collection.</returns>
    /// <example>
    /// <code>
    /// var result = ResultPaged{Product}.Success(products, totalCount, page, pageSize)
    ///     .OrderByItemsDescending(product => product.Price);
    /// </code>
    /// </example>
    public static ResultPaged<T> OrderByDescending<T, TKey>(
        this ResultPaged<T> result,
        Func<T, TKey> keySelector)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (keySelector is null)
        {
            return ResultPaged<T>.Failure()
                .WithError(new Error("Key selector cannot be null"));
        }

        try
        {
            var orderedItems = result.Value.OrderByDescending(keySelector).ToList();

            return ResultPaged<T>.Success(
                    orderedItems,
                    result.TotalCount,
                    result.CurrentPage,
                    result.PageSize)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return ResultPaged<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    #endregion
}