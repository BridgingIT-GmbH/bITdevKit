// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;
using FluentValidation.Internal;

/// <summary>
/// Represents the result of an operation, which can either be a success or a failure.
/// Contains functional methods to better work with success and failure results and their values, as well as construct results from actions or tasks.
/// </summary>
public readonly partial struct PagedResult<T>
{
    /// <summary>
    /// Maps the page collection to a new type while preserving pagination information.
    /// </summary>
    /// <example>
    /// var userDtos = pagedUsers.Map(users => users.Select(u => new UserDto(u)));
    /// // Preserves page count, current page, and page size
    /// </example>
    public PagedResult<TNew> Map<TNew>(Func<IEnumerable<T>, IEnumerable<TNew>> mapper)
    {
        if (!this.IsSuccess || mapper is null)
        {
            return PagedResult<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var newValues = mapper(this.Value);

            return PagedResult<TNew>.Success(
                    newValues,
                    this.TotalCount,
                    this.CurrentPage,
                    this.PageSize)
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public async Task<PagedResult<TNew>> MapAsync<TNew>(
        Func<IEnumerable<T>, CancellationToken, Task<IEnumerable<TNew>>> mapper,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || mapper is null)
        {
            return PagedResult<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var newValues = await mapper(this.Value, cancellationToken);

            return PagedResult<TNew>.Success(
                    newValues,
                    this.TotalCount,
                    this.CurrentPage,
                    this.PageSize)
                .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    /// Binds the page collection to a new paged result while preserving metadata.
    /// </summary>
    /// <example>
    /// var activeUsers = pagedUsers.Bind(users =>
    ///     users.Any()
    ///         ? PagedResult{User}.Success(
    ///             users.Where(u => u.IsActive),
    ///             totalCount: activeCount,
    ///             page: currentPage,
    ///             pageSize: pageSize)
    ///         : PagedResult{User}.Failure("No active users found")
    /// );
    /// </example>
    public PagedResult<TNew> Bind<TNew>(Func<IEnumerable<T>, PagedResult<TNew>> binder)
    {
        if (!this.IsSuccess || binder is null)
        {
            return PagedResult<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var result = binder(this.Value);

            return result.WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    /// Asynchronously binds the page collection to a new paged result.
    /// </summary>
    /// <example>
    /// var activeUsers = await pagedUsers.BindAsync(async (users, ct) =>
    ///     users.Any()
    ///         ? await ValidateAndFilterUsersAsync(users, ct)
    ///         : PagedResult{User}.Failure("No users to process"),
    ///     cancellationToken
    /// );
    /// </example>
    public async Task<PagedResult<TNew>> BindAsync<TNew>(
        Func<IEnumerable<T>, CancellationToken, Task<PagedResult<TNew>>> binder,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || binder is null)
        {
            return PagedResult<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var result = await binder(this.Value, cancellationToken);

            return result.WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public PagedResult<T> Filter(Func<IEnumerable<T>, bool> predicate, IResultError error)
    {
        if (!this.IsSuccess || predicate is null)
        {
            return this;
        }

        try
        {
            return predicate(this.Value)
                ? this
                : Failure(this.Value)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public async Task<PagedResult<T>> FilterAsync(
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || predicate is null)
        {
            return this;
        }

        try
        {
            return await predicate(this.Value, cancellationToken)
                ? this
                : Failure(this.Value)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public PagedResult<T> Ensure(Func<IEnumerable<T>, bool> predicate, IResultError error)
    {
        if (!this.IsSuccess)
        {
            return this;
        }

        if (predicate is null)
        {
            return Failure(this.Value)
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(this.Messages);
        }

        try
        {
            return predicate(this.Value)
                ? this
                : Failure(this.Value)
                    .WithError(error ?? new Error("Ensure condition not met"))
                    .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public async Task<PagedResult<T>> EnsureAsync(
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess)
        {
            return this;
        }

        if (predicate is null)
        {
            return Failure(this.Value)
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(this.Messages);
        }

        try
        {
            return await predicate(this.Value, cancellationToken)
                ? this
                : Failure(this.Value)
                    .WithError(error ?? new Error("Ensure condition not met"))
                    .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public PagedResult<T> Unless(Func<IEnumerable<T>, bool> predicate, IResultError error)
    {
        if (!this.IsSuccess || predicate is null)
        {
            return this;
        }

        try
        {
            return predicate(this.Value)
                ? Failure(this.Value)
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(this.Messages)
                : this;
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public async Task<PagedResult<T>> UnlessAsync(
        Func<IEnumerable<T>, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || predicate is null)
        {
            return this;
        }

        try
        {
            return await predicate(this.Value, cancellationToken)
                ? Failure(this.Value)
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(this.Messages)
                : this;
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public PagedResult<T> Tap(Action<IEnumerable<T>> operation)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            operation(this.Value);

            return this;
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public async Task<PagedResult<T>> TapAsync(
        Func<IEnumerable<T>, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            await operation(this.Value, cancellationToken);

            return this;
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public PagedResult<TNew> TeeMap<TNew>(
        Func<IEnumerable<T>, IEnumerable<TNew>> mapper,
        Action<IEnumerable<TNew>> operation)
    {
        if (!this.IsSuccess || mapper is null)
        {
            return PagedResult<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var newValues = mapper(this.Value);
            operation?.Invoke(newValues);

            return PagedResult<TNew>.Success(
                    newValues,
                    this.TotalCount,
                    this.CurrentPage,
                    this.PageSize)
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public async Task<PagedResult<TNew>> TeeMapAsync<TNew>(
        Func<IEnumerable<T>, IEnumerable<TNew>> mapper,
        Func<IEnumerable<TNew>, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || mapper is null)
        {
            return PagedResult<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var newValues = mapper(this.Value);

            if (operation is not null)
            {
                await operation(newValues, cancellationToken);
            }

            return PagedResult<TNew>.Success(
                    newValues,
                    this.TotalCount,
                    this.CurrentPage,
                    this.PageSize)
                .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    /// Maps both success and failure cases simultaneously while preserving pagination metadata.
    /// </summary>
    /// <typeparam name="TNew">The type of the new result value.</typeparam>
    /// <param name="onSuccess">Function to transform the collection if successful.</param>
    /// <param name="onFailure">Function to transform the errors if failed.</param>
    /// <returns>A new PagedResult with either the transformed collection or transformed errors.</returns>
    /// <example>
    /// <code>
    /// var result = pagedUsers
    ///     .BiMap(
    ///         users => users.Select(u => new UserDto(u)),
    ///         errors => errors.Select(e => new PublicError(e.Message))
    ///     );
    /// </code>
    /// </example>
    public PagedResult<TNew> BiMap<TNew>(
        Func<IEnumerable<T>, IEnumerable<TNew>> onSuccess,
        Func<IReadOnlyList<IResultError>, IEnumerable<IResultError>> onFailure)
    {
        if (this.IsSuccess)
        {
            if (onSuccess is null)
            {
                return PagedResult<TNew>.Failure()
                    .WithError(new Error("Success mapper is null"))
                    .WithMessages(this.Messages);
            }

            try
            {
                return PagedResult<TNew>.Success(
                        onSuccess(this.Value),
                        this.TotalCount,
                        this.CurrentPage,
                        this.PageSize)
                    .WithMessages(this.Messages);
            }
            catch (Exception ex)
            {
                return PagedResult<TNew>.Failure()
                    .WithError(Result.Settings.ExceptionErrorFactory(ex))
                    .WithMessages(this.Messages);
            }
        }

        if (onFailure is null)
        {
            return PagedResult<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            return PagedResult<TNew>.Failure()
                .WithErrors(onFailure(this.Errors))
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    /// Creates a new PagedResult by choosing elements from the current page.
    /// Preserves paging metadata with adjusted counts.
    /// </summary>
    /// <example>
    /// var adultUsers = pagedUsers.Choose(users =>
    ///     users.Any(u => u.Age >= 18)
    ///         ? new ResultChooseOption<IEnumerable<User>>(users.Where(u => u.Age >= 18))
    ///         : ResultChooseOption<IEnumerable<User>>.None()
    /// );
    /// </example>
    public PagedResult<TNew> Choose<TNew>(
        Func<IEnumerable<T>, ResultChooseOption<IEnumerable<TNew>>> operation)
    {
        if (!this.IsSuccess || operation is null)
        {
            return PagedResult<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var option = operation(this.Value);

            if (!option.TryGetValue(out var values))
            {
                return PagedResult<TNew>.Failure()
                    .WithError(new Error("No values were chosen"))
                    .WithMessages(this.Messages);
            }

            return PagedResult<TNew>.Success(
                    values,
                    this.TotalCount,
                    this.CurrentPage,
                    this.PageSize)
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    /// Asynchronously creates a new PagedResult by choosing elements from the current page.
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
    public async Task<PagedResult<TNew>> ChooseAsync<TNew>(
        Func<IEnumerable<T>, CancellationToken, Task<ResultChooseOption<IEnumerable<TNew>>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || operation is null)
        {
            return PagedResult<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var option = await operation(this.Value, cancellationToken);

            if (!option.TryGetValue(out var values))
            {
                return PagedResult<TNew>.Failure()
                    .WithError(new Error("No values were chosen"))
                    .WithMessages(this.Messages);
            }

            return PagedResult<TNew>.Success(
                    values,
                    this.TotalCount,
                    this.CurrentPage,
                    this.PageSize)
                .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return PagedResult<TNew>.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return PagedResult<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    /// Tries to perform an operation on the current page collection.
    /// </summary>
    /// <example>
    /// var result = PagedResult{User}.Try(() => {
    ///     var users = _repository.GetPagedUsers(page, pageSize);
    ///     return (users, totalCount: _repository.GetTotalCount());
    /// });
    /// </example>
    public static PagedResult<T> Try(
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

            return Success(values, totalCount);
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
    /// var result = await PagedResult{User}.TryAsync(
    ///     async ct => {
    ///         var users = await _repository.GetPagedUsersAsync(page, pageSize, ct);
    ///         var count = await _repository.GetTotalCountAsync(ct);
    ///         return (users, count);
    ///     },
    ///     cancellationToken
    /// );
    /// </example>
    public static async Task<PagedResult<T>> TryAsync(
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

            return Success(values, totalCount);
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

    public PagedResult<TOutput> Collect<TOutput>(Func<T, Result<TOutput>> operation)
    {
        if (!this.IsSuccess || operation is null)
        {
            return PagedResult<TOutput>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        var results = new List<TOutput>();
        var errors = new List<IResultError>();
        var messages = new List<string>();

        foreach (var item in this.Value)
        {
            try
            {
                var result = operation(item);
                if (result.IsSuccess)
                {
                    results.Add(result.Value);
                }
                else
                {
                    errors.AddRange(result.Errors);
                }

                messages.AddRange(result.Messages);
            }
            catch (Exception ex)
            {
                errors.Add(Result.Settings.ExceptionErrorFactory(ex));
            }
        }

        return errors.Any()
            ? PagedResult<TOutput>.Failure()
                .WithErrors(errors)
                .WithMessages(messages)
            : PagedResult<TOutput>.Success(
                    results,
                    this.TotalCount,
                    this.CurrentPage,
                    this.PageSize)
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
    public async Task<PagedResult<TOutput>> CollectAsync<TOutput>(
        Func<T, CancellationToken, Task<Result<TOutput>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || operation is null)
        {
            return PagedResult<TOutput>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        var results = new List<TOutput>();
        var errors = new List<IResultError>();
        var messages = new List<string>();

        foreach (var item in this.Value)
        {
            try
            {
                var result = await operation(item, cancellationToken);
                if (result.IsSuccess)
                {
                    results.Add(result.Value);
                }
                else
                {
                    errors.AddRange(result.Errors);
                }

                messages.AddRange(result.Messages);
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
            ? PagedResult<TOutput>.Failure()
                .WithErrors(errors)
                .WithMessages(messages)
            : PagedResult<TOutput>.Success(
                    results,
                    this.TotalCount,
                    this.CurrentPage,
                    this.PageSize)
                .WithMessages(messages);
    }

    /// <summary>
    /// Chains a new operation if the current PagedResult is successful.
    /// </summary>
    /// <example>
    /// var result = pagedUsers
    ///     .AndThen(users => ValidatePageSize(users))
    ///     .AndThen(users => ProcessUserPage(users, CurrentPage));
    /// </example>
    public PagedResult<T> AndThen(Func<IEnumerable<T>, PagedResult<T>> operation)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            var result = operation(this.Value);

            return result.WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public async Task<PagedResult<T>> AndThenAsync(
        Func<IEnumerable<T>, CancellationToken, Task<PagedResult<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            var result = await operation(this.Value, cancellationToken);

            return result.WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    /// Provides a fallback page collection in case of failure.
    /// </summary>
    /// <example>
    /// var users = pagedUsers.OrElse(() => {
    ///     var fallbackUsers = _cache.GetUserPage(page, pageSize);
    ///     return PagedResult{User}.Success(
    ///         fallbackUsers,
    ///         _cache.GetTotalUserCount(),
    ///         page,
    ///         pageSize
    ///     );
    /// });
    /// </example>
    public PagedResult<T> OrElse(Func<PagedResult<T>> fallback)
    {
        if (this.IsSuccess || fallback is null)
        {
            return this;
        }

        try
        {
            var result = fallback();

            return result.WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public async Task<PagedResult<T>> OrElseAsync(
        Func<CancellationToken, Task<PagedResult<T>>> fallback,
        CancellationToken cancellationToken = default)
    {
        if (this.IsSuccess || fallback is null)
        {
            return this;
        }

        try
        {
            var result = await fallback(cancellationToken);

            return result.WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
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
    public PagedResult<T> Validate(
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null)
    {
        if (!this.IsSuccess)
        {
            return this;
        }

        if (validator is null)
        {
            return Failure(this.Value)
                .WithError(new Error("Validator cannot be null"))
                .WithMessages(this.Messages);
        }

        try
        {
            var errors = new List<IResultError>();
            foreach (var item in this.Value)
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
                return Failure(this.Value)
                    .WithErrors(errors)
                    .WithMessages(this.Messages);
            }

            return this;
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage("Validation failed due to an error")
                .WithMessages(this.Messages);
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
    public async Task<PagedResult<T>> ValidateAsync(
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess)
        {
            return this;
        }

        if (validator is null)
        {
            return Failure(this.Value)
                .WithError(new Error("Validator cannot be null"))
                .WithMessages(this.Messages);
        }

        try
        {
            var errors = new List<IResultError>();
            foreach (var item in this.Value)
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
                return Failure(this.Value)
                    .WithErrors(errors)
                    .WithMessages(this.Messages);
            }

            return this;
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage("Validation failed due to an error")
                .WithMessages(this.Messages);
        }
    }
}