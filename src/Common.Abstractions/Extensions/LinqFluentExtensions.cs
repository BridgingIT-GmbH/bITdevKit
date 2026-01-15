// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static class LinqFluentExtensions
{
    #region Find - Fluent FirstOrDefault alternative

    /// <summary>
    /// Finds the first element matching the predicate, or returns null.
    /// Designed for fluent chaining with WhenNotNull and other optional methods.
    /// Returns null if source or predicate is null.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The collection to search. Can be null.</param>
    /// <param name="predicate">The condition to match. Can be null.</param>
    /// <returns>The first matching element, or null if not found, source is null, or predicate is null.</returns>
    /// <example>
    /// <code>
    /// // Fluent null-safe finding
    /// var primaryAddress = addresses
    ///     .Find(a => a.IsPrimary)
    ///     .WhenNotNull(a => customer.SetPrimaryAddress(a.Id));
    /// 
    /// // With transformation
    /// var email = users
    ///     .Find(u => u.IsAdmin)
    ///     .Select(u => u.Email)
    ///     .OrElse(() => "admin@company.com");
    /// 
    /// // Pattern matching
    /// var result = orders
    ///     .Find(o => o.Id == orderId)
    ///     .Match(
    ///         some: o => ProcessOrder(o),
    ///         none: () => OrderResult.NotFound);
    /// </code>
    /// </example>
    public static T Find<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        where T : class
    {
        if (source == null || predicate == null)
        {
            return null;
        }

        return source.FirstOrDefault(predicate);
    }

    /// <summary>
    /// Finds the first element matching the predicate for value types, or returns null.
    /// Returns null if source or predicate is null.
    /// </summary>
    /// <typeparam name="T">The value type of elements in the collection.</typeparam>
    /// <param name="source">The collection to search. Can be null.</param>
    /// <param name="predicate">The condition to match. Can be null.</param>
    /// <returns>The first matching element, or null if not found, source is null, or predicate is null.</returns>
    /// <example>
    /// <code>
    /// // Find with value types
    /// int? firstEven = numbers
    ///     .FindValue(n => n % 2 == 0);
    /// 
    /// // Chain with WhenNotNull
    /// dates
    ///     .FindValue(d => d > DateTime.Now)
    ///     .WhenNotNull(nextDate => ScheduleReminder(nextDate));
    /// </code>
    /// </example>
    public static T? FindValue<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        where T : struct
    {
        if (source == null || predicate == null)
        {
            return null;
        }

        foreach (var item in source)
        {
            if (predicate(item))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Asynchronously finds the first element matching the async predicate.
    /// Returns null if source or predicate is null.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The collection to search. Can be null.</param>
    /// <param name="predicate">The async condition to match. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the first matching element, or null.</returns>
    /// <example>
    /// <code>
    /// // Async find with validation
    /// var validUser = await users.FindAsync(
    ///     async (user, ct) => await authService.IsValidAsync(user.Id, ct),
    ///     cancellationToken);
    /// 
    /// // Async find and chain
    /// await users
    ///     .FindAsync(
    ///         async (u, ct) => await permissionService.HasAccessAsync(u.Id, ct),
    ///         cancellationToken)
    ///     .WhenNotNullAsync(async u => await GrantAccessAsync(u.Id), cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> FindAsync<T>(
        this IEnumerable<T> source,
        Func<T, CancellationToken, Task<bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (source == null || predicate == null)
        {
            return null;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await predicate(item, cancellationToken).AnyContext())
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Asynchronously finds the first element matching the async predicate (without cancellation token parameter).
    /// Returns null if source or predicate is null.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The collection to search. Can be null.</param>
    /// <param name="predicate">The async condition to match. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the first matching element, or null.</returns>
    /// <example>
    /// <code>
    /// var activeSubscription = await subscriptions.FindAsync(
    ///     async sub => await subscriptionService.IsActiveAsync(sub.Id),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> FindAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task<bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (source == null || predicate == null)
        {
            return null;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await predicate(item).AnyContext())
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Asynchronously finds the first value type matching the async predicate.
    /// Returns null if source or predicate is null.
    /// </summary>
    /// <typeparam name="T">The value type of elements in the collection.</typeparam>
    /// <param name="source">The collection to search. Can be null.</param>
    /// <param name="predicate">The async condition to match. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the first matching element, or null.</returns>
    /// <example>
    /// <code>
    /// var validDate = await dates.FindValueAsync(
    ///     async (date, ct) => await calendarService.IsAvailableAsync(date, ct),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T?> FindValueAsync<T>(
        this IEnumerable<T> source,
        Func<T, CancellationToken, Task<bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : struct
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (source == null || predicate == null)
        {
            return null;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await predicate(item, cancellationToken).AnyContext())
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Asynchronously finds the first value type matching the async predicate (without cancellation token parameter).
    /// Returns null if source or predicate is null.
    /// </summary>
    /// <typeparam name="T">The value type of elements in the collection.</typeparam>
    /// <param name="source">The collection to search. Can be null.</param>
    /// <param name="predicate">The async condition to match. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the first matching element, or null.</returns>
    /// <example>
    /// <code>
    /// var availableSlot = await timeSlots.FindValueAsync(
    ///     async slot => await bookingService.IsSlotAvailableAsync(slot),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T?> FindValueAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task<bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : struct
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (source == null || predicate == null)
        {
            return null;
        }

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await predicate(item).AnyContext())
            {
                return item;
            }
        }

        return null;
    }

    #endregion

    #region WhenNotNull / WhenNull - Conditional execution on null

    /// <summary>
    /// Executes an action when the reference type value is not null.
    /// Does nothing if value or action is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Simple execution
    /// customer?.Email.WhenNotNull(email => SendWelcomeEmail(email));
    /// 
    /// // Method group syntax
    /// addresses
    ///     .Find(a => a.IsPrimary)
    ///     .WhenNotNull(customer.SetPrimaryAddress);
    /// 
    /// // Chaining multiple operations
    /// user?.Name
    ///     .WhenNotNull(logger.LogInfo)
    ///     .WhenNotNull(analytics.Track);
    /// </code>
    /// </example>
    public static T WhenNotNull<T>(this T value, Action<T> action)
        where T : class
    {
        if (value != null && action != null)
        {
            action(value);
        }

        return value;
    }

    /// <summary>
    /// Executes an action when the nullable value type has a value.
    /// Does nothing if value has no value or action is null.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original nullable value for method chaining.</returns>
    /// <example>
    /// <code>
    /// order.ShippedDate.WhenNotNull(date => Console.WriteLine($"Shipped: {date}"));
    /// 
    /// user.Age.WhenNotNull(ValidateAge);
    /// </code>
    /// </example>
    public static T? WhenNotNull<T>(this T? value, Action<T> action)
        where T : struct
    {
        if (value.HasValue && action != null)
        {
            action(value.Value);
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the reference type value is not null.
    /// Does nothing if value or action is null. Can be called on sync or async value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// // With cancellation token
    /// await user?.Id.WhenNotNullAsync(
    ///     async (id, ct) => await userService.LoadProfileAsync(id, ct),
    ///     cancellationToken);
    /// 
    /// // Method group with cancellation
    /// await orders
    ///     .Find(o => o.IsPending)
    ///     .WhenNotNullAsync(orderService.ProcessAsync, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> WhenNotNullAsync<T>(
        this T value,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value != null && action != null)
        {
            await action(value, cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the reference type value is not null (without cancellation token parameter).
    /// Does nothing if value or action is null. Can be called on sync or async value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Simple async execution
    /// await customer?.Email.WhenNotNullAsync(
    ///     async email => await emailService.SendWelcomeAsync(email),
    ///     cancellationToken);
    /// 
    /// // Method group syntax
    /// await users
    ///     .Find(u => u.IsNew)
    ///     .WhenNotNullAsync(onboardingService.StartAsync, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> WhenNotNullAsync<T>(
        this T value,
        Func<T, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value != null && action != null)
        {
            await action(value).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the reference type value is not null on async result.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="sourceTask">The async task result to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await orders
    ///     .FindAsync(o => o.Id == orderId, cancellationToken)
    ///     .WhenNotNullAsync(
    ///         async (order, ct) => await auditService.LogAccessAsync(order.Id, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> WhenNotNullAsync<T>(
        this Task<T> sourceTask,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || action == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (value != null)
        {
            await action(value, cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the reference type value is not null on async result (without cancellation token parameter).
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="sourceTask">The async task result to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await users
    ///     .FindAsync(u => u.IsActive, cancellationToken)
    ///     .WhenNotNullAsync(
    ///         async user => await notificationService.SendGreetingAsync(user.Email),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> WhenNotNullAsync<T>(
        this Task<T> sourceTask,
        Func<T, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || action == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (value != null)
        {
            await action(value).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Executes an action when the reference type is null.
    /// Does nothing if value is not null or action is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// user?.Email
    ///     .WhenNotNull(SendNewsletter)
    ///     .WhenNull(() => logger.LogWarning("User has no email"));
    /// 
    /// config?.ConnectionString
    ///     .WhenNull(() => throw new InvalidOperationException("Connection string required"));
    /// </code>
    /// </example>
    public static T WhenNull<T>(this T value, Action action)
        where T : class
    {
        if (value == null && action != null)
        {
            action();
        }

        return value;
    }

    /// <summary>
    /// Executes an action when the nullable value type has no value.
    /// Does nothing if value has a value or action is null.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original nullable value for method chaining.</returns>
    /// <example>
    /// <code>
    /// order.ShippedDate
    ///     .WhenNotNull(date => DisplayShipDate(date))
    ///     .WhenNull(() => DisplayPendingStatus());
    /// </code>
    /// </example>
    public static T? WhenNull<T>(this T? value, Action action)
        where T : struct
    {
        if (!value.HasValue && action != null)
        {
            action();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the reference type is null.
    /// Does nothing if value is not null or action is null. Can be called on sync or async value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await user?.Profile.WhenNullAsync(
    ///     async ct => await profileService.CreateDefaultAsync(userId, ct),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> WhenNullAsync<T>(
        this T value,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value == null && action != null)
        {
            await action(cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the reference type is null on async result.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="sourceTask">The async task result to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await orders
    ///     .FindAsync(o => o.Id == orderId, cancellationToken)
    ///     .WhenNullAsync(
    ///         async ct => await notificationService.LogNotFoundAsync(orderId, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> WhenNullAsync<T>(
        this Task<T> sourceTask,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || action == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (value == null)
        {
            await action(cancellationToken).AnyContext();
        }

        return value;
    }

    #endregion

    #region String-specific null/empty checks

    /// <summary>
    /// Executes an action when the string is not null or empty.
    /// Does nothing if value is null, empty, or action is null.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Only process non-empty emails
    /// user.Email
    ///     .WhenNotNullOrEmpty(email => SendNotification(email));
    /// 
    /// // Chaining with empty check
    /// searchQuery
    ///     .WhenNotNullOrEmpty(query => PerformSearch(query))
    ///     .WhenNullOrEmpty(() => ShowAllResults());
    /// 
    /// // Form validation
    /// formData.Username
    ///     .WhenNotNullOrEmpty(ValidateUsername)
    ///     .WhenNullOrEmpty(() => errors.Add("Username is required"));
    /// </code>
    /// </example>
    public static string WhenNotNullOrEmpty(this string value, Action<string> action)
    {
        if (!string.IsNullOrEmpty(value) && action != null)
        {
            action(value);
        }

        return value;
    }

    /// <summary>
    /// Executes an action when the string is not null, empty, or whitespace.
    /// Does nothing if value is null, empty, whitespace, or action is null.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Only process meaningful input
    /// userInput
    ///     .WhenNotNullOrWhiteSpace(input => ProcessCommand(input));
    /// 
    /// // Validation with whitespace check
    /// formData.Description
    ///     .WhenNotNullOrWhiteSpace(desc => SaveDescription(desc))
    ///     .WhenNullOrWhiteSpace(() => UseDefaultDescription());
    /// 
    /// // Search with trimming consideration
    /// searchTerm
    ///     .WhenNotNullOrWhiteSpace(term => Search(term.Trim()));
    /// </code>
    /// </example>
    public static string WhenNotNullOrWhiteSpace(this string value, Action<string> action)
    {
        if (!string.IsNullOrWhiteSpace(value) && action != null)
        {
            action(value);
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the string is not null or empty.
    /// Does nothing if value is null, empty, or action is null. Can be called on sync or async value.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await user.Email.WhenNotNullOrEmptyAsync(
    ///     async (email, ct) => await emailService.ValidateAsync(email, ct),
    ///     cancellationToken);
    /// 
    /// await apiKey.WhenNotNullOrEmptyAsync(
    ///     async (key, ct) => await authService.AuthenticateAsync(key, ct),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<string> WhenNotNullOrEmptyAsync(
        this string value,
        Func<string, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(value) && action != null)
        {
            await action(value, cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the string is not null or empty (without cancellation token parameter).
    /// Does nothing if value is null, empty, or action is null. Can be called on sync or async value.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await user.Email.WhenNotNullOrEmptyAsync(
    ///     async email => await emailService.SendWelcomeAsync(email),
    ///     cancellationToken);
    /// 
    /// await config.ConnectionString.WhenNotNullOrEmptyAsync(
    ///     databaseService.ConnectAsync,
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<string> WhenNotNullOrEmptyAsync(
        this string value,
        Func<string, Task> action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(value) && action != null)
        {
            await action(value).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the string is not null or empty on async result.
    /// </summary>
    /// <param name="sourceTask">The async task result string to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await userService.GetEmailAsync(userId, cancellationToken)
    ///     .WhenNotNullOrEmptyAsync(
    ///         async (email, ct) => await SendWelcomeAsync(email, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<string> WhenNotNullOrEmptyAsync(
        this Task<string> sourceTask,
        Func<string, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || action == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (!string.IsNullOrEmpty(value))
        {
            await action(value, cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the string is not null, empty, or whitespace.
    /// Does nothing if value is null, empty, whitespace, or action is null. Can be called on sync or async value.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await searchQuery.WhenNotNullOrWhiteSpaceAsync(
    ///     async (query, ct) => await searchService.SearchAsync(query, ct),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<string> WhenNotNullOrWhiteSpaceAsync(
        this string value,
        Func<string, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(value) && action != null)
        {
            await action(value, cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the string is not null, empty, or whitespace on async result.
    /// </summary>
    /// <param name="sourceTask">The async task result string to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await configService.GetApiKeyAsync(cancellationToken)
    ///     .WhenNotNullOrWhiteSpaceAsync(
    ///         async (key, ct) => await InitializeApiAsync(key, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<string> WhenNotNullOrWhiteSpaceAsync(
        this Task<string> sourceTask,
        Func<string, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || action == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (!string.IsNullOrWhiteSpace(value))
        {
            await action(value, cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Executes an action when the string is null or empty.
    /// Does nothing if value has content or action is null.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Handle missing email
    /// user.Email
    ///     .WhenNotNullOrEmpty(SendNewsletter)
    ///     .WhenNullOrEmpty(() => logger.LogWarning("No email for user"));
    /// 
    /// // Validation
    /// formData.RequiredField
    ///     .WhenNullOrEmpty(() => validationErrors.Add("Field is required"));
    /// </code>
    /// </example>
    public static string WhenNullOrEmpty(this string value, Action action)
    {
        if (string.IsNullOrEmpty(value) && action != null)
        {
            action();
        }

        return value;
    }

    /// <summary>
    /// Executes an action when the string is null, empty, or whitespace.
    /// Does nothing if value has non-whitespace content or action is null.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Handle missing or blank input
    /// searchQuery
    ///     .WhenNotNullOrWhiteSpace(PerformSearch)
    ///     .WhenNullOrWhiteSpace(() => ShowDefaultResults());
    /// 
    /// // Strict validation
    /// formData.Name
    ///     .WhenNullOrWhiteSpace(() => errors.Add("Name cannot be blank"));
    /// </code>
    /// </example>
    public static string WhenNullOrWhiteSpace(this string value, Action action)
    {
        if (string.IsNullOrWhiteSpace(value) && action != null)
        {
            action();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the string is null or empty.
    /// Does nothing if value has content or action is null. Can be called on sync or async value.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await user.Email.WhenNullOrEmptyAsync(
    ///     async ct => await notificationService.RequestEmailAsync(user.Id, ct),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<string> WhenNullOrEmptyAsync(
        this string value,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(value) && action != null)
        {
            await action(cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the string is null or empty on async result.
    /// </summary>
    /// <param name="sourceTask">The async task result string to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await userService.GetBackupEmailAsync(userId, cancellationToken)
    ///     .WhenNullOrEmptyAsync(
    ///         async ct => await SendEmailVerificationAsync(userId, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<string> WhenNullOrEmptyAsync(
        this Task<string> sourceTask,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || action == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (string.IsNullOrEmpty(value))
        {
            await action(cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the string is null, empty, or whitespace.
    /// Does nothing if value has non-whitespace content or action is null. Can be called on sync or async value.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await searchQuery.WhenNullOrWhiteSpaceAsync(
    ///     async ct => await searchService.GetDefaultResultsAsync(ct),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<string> WhenNullOrWhiteSpaceAsync(
        this string value,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(value) && action != null)
        {
            await action(cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function when the string is null, empty, or whitespace on async result.
    /// </summary>
    /// <param name="sourceTask">The async task result string to check.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await configService.GetApiTokenAsync(cancellationToken)
    ///     .WhenNullOrWhiteSpaceAsync(
    ///         async ct => await LoadApiTokenFromVaultAsync(ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<string> WhenNullOrWhiteSpaceAsync(
        this Task<string> sourceTask,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || action == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (string.IsNullOrWhiteSpace(value))
        {
            await action(cancellationToken).AnyContext();
        }

        return value;
    }

    #endregion

    #region When / Unless - Conditional execution and transformation with predicate

    /// <summary>
    /// Executes a side effect if predicate is true, returns value unchanged.
    /// Does nothing if value or action is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="predicate">The condition to evaluate. Can be null.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Simple side effect
    /// user
    ///     .When(u => u.IsActive, u => logger.LogInfo(u.Name));
    /// 
    /// // Chaining multiple conditions
    /// order
    ///     .When(o => o.Total > 1000, o => ApplyPremiumBenefit(o))
    ///     .When(o => o.IsInternational, o => AddCustomsForm(o));
    /// </code>
    /// </example>
    public static T When<T>(this T value, Func<T, bool> predicate, Action<T> action)
        where T : class
    {
        if (value != null && predicate != null && predicate(value) && action != null)
        {
            action(value);
        }

        return value;
    }

    /// <summary>
    /// Executes a side effect if predicate is true on value type, returns value unchanged.
    /// Does nothing if value has no value or predicate/action is null.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="predicate">The condition to evaluate. Can be null.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original nullable value for method chaining.</returns>
    /// <example>
    /// <code>
    /// order.ShippedDate
    ///     .When(d => d > DateTime.Now, d => ScheduleReminder(d));
    /// </code>
    /// </example>
    public static T? When<T>(this T? value, Func<T, bool> predicate, Action<T> action)
        where T : struct
    {
        if (value.HasValue && predicate != null && predicate(value.Value) && action != null)
        {
            action(value.Value);
        }

        return value;
    }

    /// <summary>
    /// Transforms value if predicate is true, returns unchanged if false.
    /// Returns unchanged value if predicate or transformation function is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to transform.</param>
    /// <param name="predicate">The condition to evaluate. Can be null.</param>
    /// <param name="then">The transformation function. Can be null.</param>
    /// <returns>The transformed value or original value if condition is false.</returns>
    /// <example>
    /// <code>
    /// // Conditional transformation
    /// items
    ///     .When(i => i.Count > 0, i => i.Where(x => x.IsActive))
    ///     .ToList();
    /// 
    /// // Chaining transformations
    /// movies
    ///     .Select(m => m.Title)
    ///     .When(titles => !string.IsNullOrEmpty(search),
    ///         titles => titles.Where(s => s.Contains(search)))
    ///     .When(orderDesc,
    ///         titles => titles.OrderByDescending(s => s))
    ///     .Take(limit)
    ///     .ToArray();
    /// </code>
    /// </example>
    public static T When<T>(this T value, Func<T, bool> predicate, Func<T, T> then)
        where T : class
    {
        if (value != null && predicate != null && predicate(value) && then != null)
        {
            return then(value);
        }

        return value;
    }

    /// <summary>
    /// Transforms value if predicate is true on value type, returns unchanged if false.
    /// Returns unchanged value if predicate or transformation function is null.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to transform.</param>
    /// <param name="predicate">The condition to evaluate. Can be null.</param>
    /// <param name="then">The transformation function. Can be null.</param>
    /// <returns>The transformed value or original value if condition is false.</returns>
    /// <example>
    /// <code>
    /// decimal price = basePrice
    ///     .When(p => p > 100, p => p * 0.9m);  // Apply 10% discount if over 100
    /// </code>
    /// </example>
    public static T? When<T>(this T? value, Func<T, bool> predicate, Func<T, T> then)
        where T : struct
    {
        if (value.HasValue && predicate != null && predicate(value.Value) && then != null)
        {
            return then(value.Value);
        }

        return value;
    }

    /// <summary>
    /// Applies then or else transformation based on predicate.
    /// Returns unchanged value if predicate or transformation functions are null.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="value">The value to transform.</param>
    /// <param name="predicate">The condition to evaluate. Can be null.</param>
    /// <param name="then">The transformation if true. Can be null.</param>
    /// <param name="@else">The transformation if false. Can be null.</param>
    /// <returns>The transformed value based on predicate, or default if transformations are null.</returns>
    /// <example>
    /// <code>
    /// var result = order
    ///     .When(o => o.IsShipped,
    ///         o => new ShippedResult(o),
    ///         o => new PendingResult(o));
    /// 
    /// // With LINQ
    /// var sorted = items
    ///     .When(i => orderDesc,
    ///         i => i.OrderByDescending(x => x.Date),
    ///         i => i.OrderBy(x => x.Date))
    ///     .ToList();
    /// </code>
    /// </example>
    public static TOut When<TIn, TOut>(this TIn value, Func<TIn, bool> predicate,
        Func<TIn, TOut> then, Func<TIn, TOut> @else)
        where TIn : class
    {
        if (value == null || predicate == null)
        {
            return default;
        }

        if (predicate(value))
        {
            return then != null ? then(value) : default;
        }

        return @else != null ? @else(value) : default;
    }

    /// <summary>
    /// Applies then or else transformation on value type based on predicate.
    /// Returns default if predicate or transformation functions are null.
    /// </summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="value">The nullable value to transform.</param>
    /// <param name="predicate">The condition to evaluate. Can be null.</param>
    /// <param name="then">The transformation if true. Can be null.</param>
    /// <param name="@else">The transformation if false. Can be null.</param>
    /// <returns>The transformed value based on predicate, or default if transformations are null.</returns>
    /// <example>
    /// <code>
    /// string status = order.ShippedDate.When(
    ///     d => d < DateTime.Now,
    ///     d => $"Shipped on {d:d}",
    ///     d => "Not yet shipped");
    /// </code>
    /// </example>
    public static TOut When<TIn, TOut>(this TIn? value, Func<TIn, bool> predicate,
        Func<TIn, TOut> then, Func<TIn, TOut> @else)
        where TIn : struct
    {
        if (!value.HasValue || predicate == null)
        {
            return default;
        }

        if (predicate(value.Value))
        {
            return then != null ? then(value.Value) : default;
        }

        return @else != null ? @else(value.Value) : default;
    }

    /// <summary>
    /// Executes a side effect if predicate is false, returns value unchanged.
    /// Does nothing if value or action is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="predicate">The condition to negate. Can be null.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// // More readable negation
    /// user
    ///     .Unless(u => u.IsVerified, u => SendVerificationEmail(u));
    /// 
    /// // Clearer intent than When with negation
    /// order
    ///     .Unless(o => o.IsProcessed, o => RequeueOrder(o));
    /// </code>
    /// </example>
    public static T Unless<T>(this T value, Func<T, bool> predicate, Action<T> action)
        where T : class
    {
        if (value != null && predicate != null && !predicate(value) && action != null)
        {
            action(value);
        }

        return value;
    }

    /// <summary>
    /// Executes a side effect if predicate is false on value type, returns value unchanged.
    /// Does nothing if value has no value or predicate/action is null.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="predicate">The condition to negate. Can be null.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original nullable value for method chaining.</returns>
    /// <example>
    /// <code>
    /// DateTime? deadline = task.DueDate
    ///     .Unless(d => d < DateTime.Now, d => SendOverdueWarning(d));
    /// </code>
    /// </example>
    public static T? Unless<T>(this T? value, Func<T, bool> predicate, Action<T> action)
        where T : struct
    {
        if (value.HasValue && predicate != null && !predicate(value.Value) && action != null)
        {
            action(value.Value);
        }

        return value;
    }

    /// <summary>
    /// Transforms value if predicate is false, returns unchanged if true.
    /// Returns unchanged value if predicate or transformation function is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to transform.</param>
    /// <param name="predicate">The condition to negate. Can be null.</param>
    /// <param name="then">The transformation function. Can be null.</param>
    /// <returns>The transformed value or original value if condition is true.</returns>
    /// <example>
    /// <code>
    /// // Apply default only if not already set
    /// items
    ///     .Unless(i => i.Any(), i => i.Concat(defaultItems))
    ///     .ToList();
    /// 
    /// // Cleaner than When with negation
    /// config
    ///     .Unless(c => c.IsCached, c => c.FreshLoad())
    ///     .Apply();
    /// </code>
    /// </example>
    public static T Unless<T>(this T value, Func<T, bool> predicate, Func<T, T> then)
        where T : class
    {
        if (value != null && predicate != null && !predicate(value) && then != null)
        {
            return then(value);
        }

        return value;
    }

    /// <summary>
    /// Transforms value if predicate is false on value type, returns unchanged if true.
    /// Returns unchanged value if predicate or transformation function is null.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to transform.</param>
    /// <param name="predicate">The condition to negate. Can be null.</param>
    /// <param name="then">The transformation function. Can be null.</param>
    /// <returns>The transformed value or original value if condition is true.</returns>
    /// <example>
    /// <code>
    /// decimal price = basePrice
    ///     .Unless(p => p < 50, p => p + 10m);  // Add shipping unless under 50
    /// </code>
    /// </example>
    public static T? Unless<T>(this T? value, Func<T, bool> predicate, Func<T, T> then)
        where T : struct
    {
        if (value.HasValue && predicate != null && !predicate(value.Value) && then != null)
        {
            return then(value.Value);
        }

        return value;
    }

    /// <summary>
    /// Applies then or else transformation based on predicate being false.
    /// Returns default if predicate or transformation functions are null.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="value">The value to transform.</param>
    /// <param name="predicate">The condition to negate. Can be null.</param>
    /// <param name="then">The transformation if false. Can be null.</param>
    /// <param name="@else">The transformation if true. Can be null.</param>
    /// <returns>The transformed value based on negated predicate, or default if transformations are null.</returns>
    /// <example>
    /// <code>
    /// var items = orders
    ///     .Unless(o => o.IsShipped,
    ///         o => o.Where(x => x.IsPending),
    ///         o => o.Where(x => x.IsDelivered))
    ///     .ToList();
    /// </code>
    /// </example>
    public static TOut Unless<TIn, TOut>(this TIn value, Func<TIn, bool> predicate,
        Func<TIn, TOut> then, Func<TIn, TOut> @else)
        where TIn : class
    {
        if (value == null || predicate == null)
        {
            return default;
        }

        if (!predicate(value))
        {
            return then != null ? then(value) : default;
        }

        return @else != null ? @else(value) : default;
    }

    /// <summary>
    /// Applies then or else transformation on value type based on predicate being false.
    /// Returns default if predicate or transformation functions are null.
    /// </summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="value">The nullable value to transform.</param>
    /// <param name="predicate">The condition to negate. Can be null.</param>
    /// <param name="then">The transformation if false. Can be null.</param>
    /// <param name="@else">The transformation if true. Can be null.</param>
    /// <returns>The transformed value based on negated predicate, or default if transformations are null.</returns>
    /// <example>
    /// <code>
    /// string message = order.ProcessedAt.Unless(
    ///     d => d < DateTime.Now,
    ///     d => "Not yet processed",
    ///     d => $"Processed on {d:d}");
    /// </code>
    /// </example>
    public static TOut Unless<TIn, TOut>(this TIn? value, Func<TIn, bool> predicate,
        Func<TIn, TOut> then, Func<TIn, TOut> @else)
        where TIn : struct
    {
        if (!value.HasValue || predicate == null)
        {
            return default;
        }

        if (!predicate(value.Value))
        {
            return then != null ? then(value.Value) : default;
        }

        return @else != null ? @else(value.Value) : default;
    }

    /// <summary>
    /// Asynchronously executes a side effect if predicate is true.
    /// Can be called on sync or async value. Does nothing if predicate or action is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="predicate">The async condition to evaluate. Can be null.</param>
    /// <param name="action">The async action to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Async condition and action
    /// await order
    ///     .WhenAsync(
    ///         async o => await IsHighValueAsync(o.Id),
    ///         async o => await ApplyPremiumBenefitsAsync(o.Id),
    ///         cancellationToken);
    /// 
    /// // Chain async operations
    /// await orders
    ///     .FindAsync(o => o.Id == orderId, cancellationToken)
    ///     .WhenAsync(
    ///         async (o, ct) => await o.IsEligibleAsync(ct),
    ///         async (o, ct) => await ProcessAsync(o, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> WhenAsync<T>(
        this T value,
        Func<T, CancellationToken, Task<bool>> predicate,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value != null && predicate != null && action != null)
        {
            if (await predicate(value, cancellationToken).AnyContext())
            {
                await action(value, cancellationToken).AnyContext();
            }
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a side effect if predicate is false.
    /// Can be called on sync or async value. Does nothing if predicate or action is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="predicate">The async condition to negate. Can be null.</param>
    /// <param name="action">The async action to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await user
    ///     .UnlessAsync(
    ///         async u => await IsVerifiedAsync(u.Id),
    ///         async u => await SendVerificationAsync(u.Email),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> UnlessAsync<T>(
        this T value,
        Func<T, CancellationToken, Task<bool>> predicate,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value != null && predicate != null && action != null)
        {
            if (!await predicate(value, cancellationToken).AnyContext())
            {
                await action(value, cancellationToken).AnyContext();
            }
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a side effect if predicate is true on async result.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="sourceTask">The async task result to check.</param>
    /// <param name="predicate">The async condition to evaluate. Can be null.</param>
    /// <param name="action">The async action to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await orders
    ///     .FindAsync(o => o.Id == orderId, cancellationToken)
    ///     .WhenAsync(
    ///         async (o, ct) => await o.IsReadyAsync(ct),
    ///         async (o, ct) => await ShipAsync(o.Id, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> WhenAsync<T>(
        this Task<T> sourceTask,
        Func<T, CancellationToken, Task<bool>> predicate,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || predicate == null || action == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (value != null && await predicate(value, cancellationToken).AnyContext())
        {
            await action(value, cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a side effect if predicate is false on async result.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="sourceTask">The async task result to check.</param>
    /// <param name="predicate">The async condition to negate. Can be null.</param>
    /// <param name="action">The async action to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value for method chaining.</returns>
    /// <example>
    /// <code>
    /// await users
    ///     .FindAsync(u => u.Id == userId, cancellationToken)
    ///     .UnlessAsync(
    ///         async (u, ct) => await HasAccessAsync(u.Id, ct),
    ///         async (u, ct) => await DenyAccessAsync(u.Id, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> UnlessAsync<T>(
        this Task<T> sourceTask,
        Func<T, CancellationToken, Task<bool>> predicate,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || predicate == null || action == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (value != null && !await predicate(value, cancellationToken).AnyContext())
        {
            await action(value, cancellationToken).AnyContext();
        }

        return value;
    }

    #endregion

    #region When / Otherwise - Bool chain execution

    /// <summary>
    /// Executes an action if bool condition is true.
    /// Does nothing if condition is false or action is null.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original condition for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Simple condition check
    /// user.IsAdmin
    ///     .When(() => logger.LogInfo("Admin access granted"));
    /// 
    /// // Chaining conditions
    /// order.Items.Any()
    ///     .When(() => ProcessOrder(order))
    ///     .Otherwise(() => logger.LogWarning("Empty order"));
    /// 
    /// // Multiple conditions
    /// (items.Count > 0 && user.HasPermission("edit"))
    ///     .When(() => EnableEditMode())
    ///     .Otherwise(() => DisableEditMode());
    /// </code>
    /// </example>
    public static bool When(this bool condition, Action action)
    {
        if (condition && action != null)
        {
            action();
        }

        return condition;
    }

    /// <summary>
    /// Executes an action if bool condition is false.
    /// Does nothing if condition is true or action is null.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="action">The action to execute. Can be null.</param>
    /// <returns>The original condition for method chaining.</returns>
    /// <example>
    /// <code>
    /// user.HasPermission("admin")
    ///     .When(() => ShowAdminPanel())
    ///     .Otherwise(() => ShowAccessDenied());
    /// 
    /// cache.Contains(key)
    ///     .Otherwise(() => cache.Set(key, ComputeValue()));
    /// 
    /// // Readable negation
    /// (!isProcessed)
    ///     .When(() => logger.LogWarning("Not yet processed"));
    /// </code>
    /// </example>
    public static bool Otherwise(this bool condition, Action action)
    {
        if (!condition && action != null)
        {
            action();
        }

        return condition;
    }

    /// <summary>
    /// Asynchronously executes a function if bool condition is true.
    /// Can be called on sync or async condition. Does nothing if action is null.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original condition for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Async action on bool
    /// await user.IsSubscribed.WhenAsync(
    ///     async ct => await emailService.SendNewsletterAsync(user.Email, ct),
    ///     cancellationToken);
    /// 
    /// // Chaining async conditions
    /// await order.RequiresApproval.WhenAsync(
    ///     async ct => await approvalService.RequestApprovalAsync(order.Id, ct),
    ///     cancellationToken)
    ///     .OtherwiseAsync(
    ///         async ct => await NotifyCustomerAsync(order.CustomerId, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<bool> WhenAsync(
        this bool condition,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (condition && action != null)
        {
            await action(cancellationToken).AnyContext();
        }

        return condition;
    }

    /// <summary>
    /// Asynchronously executes a function if bool condition is false.
    /// Can be called on sync or async condition. Does nothing if action is null.
    /// </summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original condition for method chaining.</returns>
    /// <example>
    /// <code>
    /// await cache.ContainsAsync(key).OtherwiseAsync(
    ///     async ct => await cache.SetAsync(key, await ComputeValueAsync(ct), ct),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<bool> OtherwiseAsync(
        this bool condition,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (!condition && action != null)
        {
            await action(cancellationToken).AnyContext();
        }

        return condition;
    }

    /// <summary>
    /// Asynchronously executes a function if awaited condition is true.
    /// Enables chaining after async condition operations.
    /// </summary>
    /// <param name="conditionTask">The task containing the condition to evaluate.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original condition for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Chain after async condition
    /// await userService.ExistsAsync(userId, cancellationToken)
    ///     .WhenAsync(
    ///         async ct => await LoadUserDataAsync(userId, ct),
    ///         cancellationToken)
    ///     .OtherwiseAsync(
    ///         async ct => await CreateNewUserAsync(userId, ct),
    ///         cancellationToken);
    /// 
    /// // Check multiple conditions
    /// await authService.IsValidTokenAsync(token, cancellationToken)
    ///     .WhenAsync(
    ///         async ct => await GrantAccessAsync(userId, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<bool> WhenAsync(
        this Task<bool> conditionTask,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (conditionTask == null || action == null)
        {
            return false;
        }

        var condition = await conditionTask.AnyContext();
        if (condition)
        {
            await action(cancellationToken).AnyContext();
        }

        return condition;
    }

    /// <summary>
    /// Asynchronously executes a function if awaited condition is false.
    /// Enables chaining after async condition operations.
    /// </summary>
    /// <param name="conditionTask">The task containing the condition to evaluate.</param>
    /// <param name="action">The async function to execute. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original condition for method chaining.</returns>
    /// <example>
    /// <code>
    /// await cacheService.ExistsAsync(key, cancellationToken)
    ///     .OtherwiseAsync(
    ///         async ct => await cacheService.SetAsync(key, value, ct),
    ///         cancellationToken);
    /// 
    /// // Check if not already processed
    /// await orderService.IsProcessedAsync(orderId, cancellationToken)
    ///     .OtherwiseAsync(
    ///         async ct => await ProcessOrderAsync(orderId, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<bool> OtherwiseAsync(
        this Task<bool> conditionTask,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (conditionTask == null || action == null)
        {
            return false;
        }

        var condition = await conditionTask.AnyContext();
        if (!condition)
        {
            await action(cancellationToken).AnyContext();
        }

        return condition;
    }

    #endregion

    #region Do - Side effects without changing value

    /// <summary>
    /// Executes an action for side effects, returning the value unchanged.
    /// Useful for logging, debugging, and audit trails.
    /// Does nothing if value or action is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to tap into.</param>
    /// <param name="action">The action to execute for side effects. Can be null.</param>
    /// <returns>The original value unchanged.</returns>
    /// <example>
    /// <code>
    /// // Logging in the middle of a chain
    /// var user = users
    ///     .Find(u => u.Id == userId)
    ///     .Do(u => logger.LogDebug($"Found user: {u.Name}"))
    ///     .Do(u => auditService.LogAccess(u.Id))
    ///     .WhenNotNull(ProcessUser);
    /// 
    /// // Multiple taps for different concerns
    /// var summary = items
    ///     .Where(i => i.IsActive)
    ///     .Do(filtered => logger.LogInfo($"Filtered: {filtered.Count()} items"))
    ///     .Select(i => i.Price)
    ///     .Do(prices => logger.LogDebug($"Min: {prices.Min()}, Max: {prices.Max()}"))
    ///     .Sum();
    /// </code>
    /// </example>
    public static T Do<T>(this T value, Action<T> action)
        where T : class
    {
        if (value != null && action != null)
        {
            action(value);
        }

        return value;
    }

    /// <summary>
    /// Executes an action for side effects on value type, returning the value unchanged.
    /// Does nothing if value has no value or action is null.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to tap into.</param>
    /// <param name="action">The action to execute for side effects. Can be null.</param>
    /// <returns>The original value unchanged.</returns>
    /// <example>
    /// <code>
    /// var total = prices
    ///     .Sum()
    ///     .Do(sum => logger.LogInfo($"Total calculated: {sum}"));
    /// 
    /// DateTime? shipDate = order.ShippedDate
    ///     .Do(date => auditService.LogShipment(date));
    /// </code>
    /// </example>
    public static T? Do<T>(this T? value, Action<T> action)
        where T : struct
    {
        if (value.HasValue && action != null)
        {
            action(value.Value);
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function for side effects on sync or async value, wrapping result in Task.
    /// Useful for async logging, validation, and audit trails.
    /// Does nothing if value or action is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to tap into.</param>
    /// <param name="action">The async function to execute for side effects. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value unchanged.</returns>
    /// <example>
    /// <code>
    /// // Async logging in a chain
    /// var user = users
    ///     .Find(u => u.Id == userId)
    ///     .DoAsync(
    ///         async (u, ct) => await auditService.LogAccessAsync(u.Id, ct),
    ///         cancellationToken)
    ///     .WhenNotNullAsync(
    ///         async (u, ct) => await ProcessUserAsync(u, ct),
    ///         cancellationToken);
    /// 
    /// // Multiple async taps
    /// await items
    ///     .Find(i => i.IsActive)
    ///     .DoAsync(
    ///         async (item, ct) => await logger.LogAsync($"Processing: {item.Id}", ct),
    ///         cancellationToken)
    ///     .DoAsync(
    ///         async (item, ct) => await validator.ValidateAsync(item, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> DoAsync<T>(
        this T value,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value != null && action != null)
        {
            await action(value, cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function for side effects on sync or async value (without cancellation token parameter).
    /// Does nothing if value or action is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to tap into.</param>
    /// <param name="action">The async function to execute for side effects. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value unchanged.</returns>
    /// <example>
    /// <code>
    /// // Without CT parameter
    /// await user
    ///     .DoAsync(async u => await logger.LogAsync($"User: {u.Name}"), cancellationToken)
    ///     .WhenNotNullAsync(
    ///         async u => await SendGreetingAsync(u.Email),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> DoAsync<T>(
        this T value,
        Func<T, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value != null && action != null)
        {
            await action(value).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function for side effects on value type, wrapping result in Task.
    /// Does nothing if value has no value or action is null.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to tap into.</param>
    /// <param name="action">The async function to execute for side effects. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value unchanged.</returns>
    /// <example>
    /// <code>
    /// var total = await CalculateTotalAsync(cancellationToken)
    ///     .DoAsync(
    ///         async (sum, ct) => await logger.LogTotalAsync(sum, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T?> DoAsync<T>(
        this T? value,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        where T : struct
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value.HasValue && action != null)
        {
            await action(value.Value, cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function for side effects on async result.
    /// Continues the async chain without modification.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="sourceTask">The async task result to tap into.</param>
    /// <param name="action">The async function to execute for side effects. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value unchanged.</returns>
    /// <example>
    /// <code>
    /// // Async tap on async result
    /// await orders
    ///     .FindAsync(o => o.Id == orderId, cancellationToken)
    ///     .DoAsync(
    ///         async (order, ct) => await auditService.LogOrderAccessAsync(order.Id, ct),
    ///         cancellationToken)
    ///     .WhenNotNullAsync(
    ///         async (order, ct) => await ProcessOrderAsync(order, ct),
    ///         cancellationToken);
    /// 
    /// // Multiple async taps in chain
    /// var user = await userService.GetUserAsync(userId, cancellationToken)
    ///     .DoAsync(
    ///         async (u, ct) => await logger.LogAccessAsync(u.Id, ct),
    ///         cancellationToken)
    ///     .DoAsync(
    ///         async (u, ct) => await NotifyAsync(u.Email, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> DoAsync<T>(
        this Task<T> sourceTask,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || action == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (value != null)
        {
            await action(value, cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously executes a function for side effects on async result (without cancellation token parameter).
    /// Continues the async chain without modification.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="sourceTask">The async task result to tap into.</param>
    /// <param name="action">The async function to execute for side effects. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value unchanged.</returns>
    /// <example>
    /// <code>
    /// await userService.LoadUserAsync(userId, cancellationToken)
    ///     .DoAsync(
    ///         async u => await LogUserLoadAsync(u),
    ///         cancellationToken)
    ///     .Select(u => u.Profile);
    /// </code>
    /// </example>
    public static async Task<T> DoAsync<T>(
        this Task<T> sourceTask,
        Func<T, Task> action,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || action == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (value != null)
        {
            await action(value).AnyContext();
        }

        return value;
    }

    #endregion

    #region Select - Transform value (bridge sync and async chains)

    /// <summary>
    /// Applies a synchronous transformation to a value.
    /// Alias for projection, provides fluent chaining with optional methods.
    /// Returns default if selector is null.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="source">The value to transform.</param>
    /// <param name="selector">The transformation function. Can be null.</param>
    /// <returns>The transformed value, or default if selector is null.</returns>
    /// <example>
    /// <code>
    /// // Simple transformation
    /// var email = user
    ///     .Select(u => u.Email);
    /// 
    /// // Bridge sync to async
    /// var profile = user
    ///     .Select(u => new { u.Id, u.Name })
    ///     .SelectAsync(async data => new
    ///     {
    ///         data.Id,
    ///         data.Name,
    ///         Permissions = await permissionService.GetAsync(data.Id)
    ///     });
    /// 
    /// // With LINQ
    /// var report = orders
    ///     .Select(o => o.Items)
    ///     .When(items => items.Any(),
    ///         items => items.Where(i => i.IsActive))
    ///     .ToList();
    /// </code>
    /// </example>
    //public static TOut Select<TIn, TOut>(this TIn source, Func<TIn, TOut> selector)
    //{
    //    if (selector == null)
    //    {
    //        return default;
    //    }

    //    return selector(source);
    //}

    /// <summary>
    /// Applies a synchronous transformation to an async result.
    /// Enables bridging from async to sync operations in a fluent chain.
    /// Returns default if selector is null.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="sourceTask">The async task result to transform.</param>
    /// <param name="selector">The transformation function. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the transformed value, or default if selector is null.</returns>
    /// <example>
    /// <code>
    /// // Async to sync projection
    /// var summary = await userService.GetUserAsync(userId, cancellationToken)
    ///     .Select(u => new 
    ///     { 
    ///         u.Id, 
    ///         u.Name,
    ///         u.Email 
    ///     });
    /// 
    /// // With LINQ chaining
    /// var orders = await orderService.GetOrdersAsync(customerId, cancellationToken)
    ///     .Select(list => list
    ///         .Where(o => o.IsActive)
    ///         .OrderByDescending(o => o.CreatedDate)
    ///         .Take(10)
    ///         .ToList());
    /// 
    /// // Complex transformation
    /// var report = await orders
    ///     .FindAsync(o => o.Id == orderId, cancellationToken)
    ///     .Select(order => new
    ///     {
    ///         Order = order,
    ///         Items = order.Items
    ///             .Where(i => i.IsActive)
    ///             .ToList()
    ///     })
    ///     .Do(data => logger.LogDebug($"Found {data.Items.Count} items"));
    /// </code>
    /// </example>
    public static async Task<TOut> Select<TIn, TOut>(
        this Task<TIn> sourceTask,
        Func<TIn, TOut> selector,
        CancellationToken cancellationToken = default)
    {
        if (sourceTask == null || selector == null)
        {
            return default;
        }

        var source = await sourceTask.AnyContext();
        return selector(source);
    }

    /// <summary>
    /// Applies an asynchronous transformation to a sync or async value.
    /// Wraps sync value in Task if needed, enabling seamless async chaining.
    /// Returns default if selector is null.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="source">The value to transform.</param>
    /// <param name="selector">The async transformation function. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the transformed value, or default if selector is null.</returns>
    /// <example>
    /// <code>
    /// // Sync to async projection
    /// var enriched = await user
    ///     .SelectAsync(async (u, ct) => new
    ///     {
    ///         User = u,
    ///         Permissions = await permissionService.GetAsync(u.Id, ct)
    ///     },
    ///     cancellationToken);
    /// 
    /// // Chain multiple async transforms
    /// var details = await orders
    ///     .Find(o => o.Id == orderId)
    ///     .SelectAsync(async (order, ct) => new
    ///     {
    ///         Order = order,
    ///         Customer = await customerService.GetAsync(order.CustomerId, ct)
    ///     },
    ///     cancellationToken)
    ///     .SelectAsync(async (data, ct) => new
    ///     {
    ///         data.Order,
    ///         data.Customer,
    ///         History = await orderHistoryService.GetAsync(data.Order.Id, ct)
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<TOut> SelectAsync<TIn, TOut>(
        this TIn source,
        Func<TIn, CancellationToken, Task<TOut>> selector,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (selector == null)
        {
            return default;
        }

        return await selector(source, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Applies an asynchronous transformation to a sync or async value (without cancellation token parameter).
    /// Wraps sync value in Task if needed, enabling seamless async chaining.
    /// Returns default if selector is null.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="source">The value to transform.</param>
    /// <param name="selector">The async transformation function. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the transformed value, or default if selector is null.</returns>
    /// <example>
    /// <code>
    /// // Simpler async without CT
    /// var profile = await user
    ///     .SelectAsync(async u => await profileService.LoadAsync(u.Id),
    ///     cancellationToken);
    /// 
    /// // Method group syntax
    /// var data = await order
    ///     .SelectAsync(orderService.EnrichAsync, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<TOut> SelectAsync<TIn, TOut>(
        this TIn source,
        Func<TIn, Task<TOut>> selector,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (selector == null)
        {
            return default;
        }

        return await selector(source).AnyContext();
    }

    /// <summary>
    /// Applies an asynchronous transformation to an async result.
    /// Continues the async chain with transformation.
    /// Returns default if selector is null.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="sourceTask">The async task result to transform.</param>
    /// <param name="selector">The async transformation function. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the transformed value, or default if selector is null.</returns>
    /// <example>
    /// <code>
    /// // Async to async projection
    /// var enriched = await userService.GetUserAsync(userId, cancellationToken)
    ///     .SelectAsync(async (user, ct) => new
    ///     {
    ///         User = user,
    ///         Permissions = await permissionService.GetAsync(user.Id, ct)
    ///     },
    ///     cancellationToken)
    ///     .SelectAsync(async (data, ct) => new
    ///     {
    ///         data.User,
    ///         data.Permissions,
    ///         Roles = await roleService.GetAsync(data.User.Id, ct)
    ///     },
    ///     cancellationToken);
    /// 
    /// // Complex async chain
    /// var report = await orders
    ///     .FindAsync(o => o.Id == orderId, cancellationToken)
    ///     .SelectAsync(async (order, ct) => new
    ///     {
    ///         Order = order,
    ///         Customer = await customerService.GetAsync(order.CustomerId, ct)
    ///     },
    ///     cancellationToken)
    ///     .DoAsync(
    ///         async (data, ct) => await auditService.LogAsync($"Order {data.Order.Id} retrieved", ct),
    ///         cancellationToken)
    ///     .SelectAsync(async (data, ct) => new OrderReport
    ///     {
    ///         OrderId = data.Order.Id,
    ///         CustomerName = data.Customer.Name,
    ///         Total = data.Order.Total,
    ///         History = await orderHistoryService.GetAsync(data.Order.Id, ct)
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<TOut> SelectAsync<TIn, TOut>(
        this Task<TIn> sourceTask,
        Func<TIn, CancellationToken, Task<TOut>> selector,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || selector == null)
        {
            return default;
        }

        var source = await sourceTask.AnyContext();
        return await selector(source, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Applies an asynchronous transformation to an async result (without cancellation token parameter).
    /// Continues the async chain with transformation.
    /// Returns default if selector is null.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="sourceTask">The async task result to transform.</param>
    /// <param name="selector">The async transformation function. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the transformed value, or default if selector is null.</returns>
    /// <example>
    /// <code>
    /// // Simpler async without CT parameter
    /// var profile = await userService.GetUserAsync(userId, cancellationToken)
    ///     .SelectAsync(async u => await profileService.LoadAsync(u.Id), cancellationToken);
    /// 
    /// // Method group
    /// var enriched = await orders
    ///     .FindAsync(o => o.Id == orderId, cancellationToken)
    ///     .SelectAsync(orderService.EnrichAsync, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<TOut> SelectAsync<TIn, TOut>(
        this Task<TIn> sourceTask,
        Func<TIn, Task<TOut>> selector,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || selector == null)
        {
            return default;
        }

        var source = await sourceTask.AnyContext();
        return await selector(source).AnyContext();
    }

    #endregion

    #region Throw / ThrowWhen - Validation and error handling

    /// <summary>
    /// Throws an exception if the reference type value is null.
    /// Returns the value if not null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check. Can be null.</param>
    /// <param name="exceptionFactory">Factory function to create exception. Can be null.</param>
    /// <returns>The original value if not null.</returns>
    /// <example>
    /// <code>
    /// // Throw if not found
    /// var order = orders
    ///     .Find(o => o.Id == orderId)
    ///     .Throw(() => new OrderNotFoundException($"Order {orderId} not found"));
    /// 
    /// // With custom exception
    /// var customer = customers
    ///     .Find(c => c.Email == email)
    ///     .Throw(() => new InvalidOperationException("Customer not registered"));
    /// 
    /// // Chaining validations
    /// var user = users
    ///     .Find(u => u.Id == userId)
    ///     .Throw(() => new ArgumentException("User not found"))
    ///     .WhenNotNull(u => ProcessUser(u));
    /// </code>
    /// </example>
    public static T Throw<T>(this T value, Func<Exception> exceptionFactory)
        where T : class
    {
        if (value == null && exceptionFactory != null)
        {
            throw exceptionFactory();
        }

        return value;
    }

    /// <summary>
    /// Throws an exception if the value type has no value.
    /// Returns the value if it has one.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="exceptionFactory">Factory function to create exception. Can be null.</param>
    /// <returns>The original value if it has one.</returns>
    /// <example>
    /// <code>
    /// int? count = items.FindValue(i => i.IsActive)
    ///     .Throw(() => new InvalidOperationException("No active items"));
    /// 
    /// DateTime? date = order.ShippedDate
    ///     .Throw(() => new OrderNotShippedException("Order not yet shipped"));
    /// </code>
    /// </example>
    public static T? Throw<T>(this T? value, Func<Exception> exceptionFactory)
        where T : struct
    {
        if (!value.HasValue && exceptionFactory != null)
        {
            throw exceptionFactory();
        }

        return value;
    }

    /// <summary>
    /// Throws an exception if the condition is true.
    /// Returns the value if condition is false.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to validate. Can be null.</param>
    /// <param name="condition">The condition to evaluate. Can be null.</param>
    /// <param name="exceptionFactory">Factory function to create exception. Can be null.</param>
    /// <returns>The original value if condition is false.</returns>
    /// <example>
    /// <code>
    /// // Throw if deleted
    /// var order = orders
    ///     .Find(o => o.Id == orderId)
    ///     .ThrowWhen(o => o.IsDeleted, o => new InvalidOperationException($"Order {o.Id} is deleted"));
    /// 
    /// // Throw if not verified
    /// var user = users
    ///     .Find(u => u.Id == userId)
    ///     .ThrowWhen(u => !u.IsVerified, u => new UnverifiedUserException(u.Id));
    /// 
    /// // Multiple validations
    /// var address = addresses
    ///     .Find(a => a.IsPrimary)
    ///     .ThrowWhen(a => a == null, () => new NoAddressException())
    ///     .ThrowWhen(a => !a.IsVerified, a => new UnverifiedAddressException(a.Id));
    /// </code>
    /// </example>
    public static T ThrowWhen<T>(this T value, Func<T, bool> condition,
        Func<T, Exception> exceptionFactory)
        where T : class
    {
        if (value != null && condition != null && condition(value) && exceptionFactory != null)
        {
            throw exceptionFactory(value);
        }

        return value;
    }

    /// <summary>
    /// Throws an exception if the condition is true on value type.
    /// Returns the value if condition is false.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to validate.</param>
    /// <param name="condition">The condition to evaluate. Can be null.</param>
    /// <param name="exceptionFactory">Factory function to create exception. Can be null.</param>
    /// <returns>The original value if condition is false.</returns>
    /// <example>
    /// <code>
    /// int? quantity = order.Quantity
    ///     .ThrowWhen(q => q <= 0, q => new InvalidQuantityException(q));
    /// 
    /// DateTime? date = appointment.ScheduledAt
    ///     .ThrowWhen(d => d < DateTime.Now, d => new PastDateException(d));
    /// </code>
    /// </example>
    public static T? ThrowWhen<T>(this T? value, Func<T, bool> condition,
        Func<T, Exception> exceptionFactory)
        where T : struct
    {
        if (value.HasValue && condition != null && condition(value.Value) && exceptionFactory != null)
        {
            throw exceptionFactory(value.Value);
        }

        return value;
    }

    /// <summary>
    /// Asynchronously throws an exception if the reference type value is null.
    /// Can be called on sync or async value. Returns the value if not null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check. Can be null.</param>
    /// <param name="exceptionFactory">Async factory function to create exception. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value if not null.</returns>
    /// <example>
    /// <code>
    /// // Async throw on null
    /// var order = await orders
    ///     .Find(o => o.Id == orderId)
    ///     .ThrowAsync(async ct => 
    ///         new OrderNotFoundException($"Order {orderId} not found"),
    ///     cancellationToken);
    /// 
    /// // With async validation in factory
    /// var user = await users
    ///     .Find(u => u.Id == userId)
    ///     .ThrowAsync(async ct => 
    ///         await CreateCustomExceptionAsync(userId, ct),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> ThrowAsync<T>(
        this T value,
        Func<CancellationToken, Task<Exception>> exceptionFactory,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (value == null && exceptionFactory != null)
        {
            throw await exceptionFactory(cancellationToken).AnyContext();
        }

        return value;
    }

    /// <summary>
    /// Asynchronously throws an exception if the condition is true.
    /// Can be called on sync or async value. Returns the value if condition is false.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to validate. Can be null.</param>
    /// <param name="condition">The async condition to evaluate. Can be null.</param>
    /// <param name="exceptionFactory">Async factory function to create exception. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value if condition is false.</returns>
    /// <example>
    /// <code>
    /// // Async throw if deleted
    /// var order = await orders
    ///     .Find(o => o.Id == orderId)
    ///     .ThrowWhenAsync(
    ///         async (o, ct) => await o.IsDeletedAsync(ct),
    ///         async (o, ct) => new InvalidOperationException($"Order {o.Id} is deleted"),
    ///         cancellationToken);
    /// 
    /// // Async validation
    /// var user = await users
    ///     .Find(u => u.Id == userId)
    ///     .ThrowWhenAsync(
    ///         async (u, ct) => !(await authService.IsValidAsync(u.Id, ct)),
    ///         async (u, ct) => new UnauthorizedAccessException($"User {u.Id} not authorized"),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> ThrowWhenAsync<T>(
        this T value,
        Func<T, CancellationToken, Task<bool>> condition,
        Func<T, CancellationToken, Task<Exception>> exceptionFactory,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value != null && condition != null && exceptionFactory != null)
        {
            if (await condition(value, cancellationToken).AnyContext())
            {
                throw await exceptionFactory(value, cancellationToken).AnyContext();
            }
        }

        return value;
    }

    /// <summary>
    /// Asynchronously throws an exception if the condition is true on async result.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="sourceTask">The async task result to validate.</param>
    /// <param name="condition">The async condition to evaluate. Can be null.</param>
    /// <param name="exceptionFactory">Async factory function to create exception. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value if condition is false.</returns>
    /// <example>
    /// <code>
    /// // Async throw on async result
    /// var order = await orders
    ///     .FindAsync(o => o.Id == orderId, cancellationToken)
    ///     .ThrowAsync(
    ///         async ct => new OrderNotFoundException($"Order {orderId} not found"),
    ///         cancellationToken)
    ///     .ThrowWhenAsync(
    ///         async (o, ct) => await o.IsDeletedAsync(ct),
    ///         async (o, ct) => new InvalidOperationException($"Order {o.Id} is deleted"),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> ThrowWhenAsync<T>(
        this Task<T> sourceTask,
        Func<T, CancellationToken, Task<bool>> condition,
        Func<T, CancellationToken, Task<Exception>> exceptionFactory,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null || condition == null || exceptionFactory == null)
        {
            return null;
        }

        var value = await sourceTask.AnyContext();
        if (value != null && await condition(value, cancellationToken).AnyContext())
        {
            throw await exceptionFactory(value, cancellationToken).AnyContext();
        }

        return value;
    }

    #endregion

    #region Match - Handle both some and none cases

    /// <summary>
    /// Pattern matches on a nullable value, executing the appropriate function
    /// for the some (has value) or none (null) case.
    /// Returns default if both functions are null.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="value">The nullable value to match on.</param>
    /// <param name="some">Function to execute when value is not null. Can be null.</param>
    /// <param name="none">Function to execute when value is null. Can be null.</param>
    /// <returns>The result of the matching function, or default if appropriate function is null.</returns>
    /// <example>
    /// <code>
    /// // Pattern matching with both cases
    /// string message = user?.Email.Match(
    ///     some: email => $"Contact: {email}",
    ///     none: () => "No email provided");
    /// 
    /// // With operations
    /// var result = orders
    ///     .Find(o => o.Id == orderId)
    ///     .Match(
    ///         some: o => ProcessOrder(o),
    ///         none: () => OrderResult.NotFound);
    /// 
    /// // Complex matching
    /// var display = customer?.Status.Match(
    ///     some: status => GetStatusDisplay(status),
    ///     none: () => "Unknown");
    /// </code>
    /// </example>
    public static TResult Match<TSource, TResult>(
        this TSource value,
        Func<TSource, TResult> some,
        Func<TResult> none)
        where TSource : class
    {
        if (value != null)
        {
            return some != null ? some(value) : default;
        }

        return none != null ? none() : default;
    }

    /// <summary>
    /// Pattern matches on a nullable value type.
    /// Returns default if both functions are null.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="value">The nullable value to match on.</param>
    /// <param name="some">Function to execute when value has a value. Can be null.</param>
    /// <param name="none">Function to execute when value has no value. Can be null.</param>
    /// <returns>The result of the matching function, or default if appropriate function is null.</returns>
    /// <example>
    /// <code>
    /// // Match on nullable struct
    /// string status = order.ShippedDate.Match(
    ///     some: date => $"Shipped on {date:d}",
    ///     none: () => "Pending shipment");
    /// 
    /// // With calculations
    /// decimal discount = order.DiscountPercent.Match(
    ///     some: percent => CalculateDiscount(percent),
    ///     none: () => 0m);
    /// </code>
    /// </example>
    public static TResult Match<TSource, TResult>(
        this TSource? value,
        Func<TSource, TResult> some,
        Func<TResult> none)
        where TSource : struct
    {
        if (value.HasValue)
        {
            return some != null ? some(value.Value) : default;
        }

        return none != null ? none() : default;
    }

    /// <summary>
    /// Asynchronously pattern matches on a nullable value, executing the appropriate async function
    /// for the some (has value) or none (null) case.
    /// Can be called on sync or async value. Returns default if both functions are null.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="value">The nullable value to match on.</param>
    /// <param name="some">Async function to execute when value is not null. Can be null.</param>
    /// <param name="none">Async function to execute when value is null. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the result of the matching function, or default.</returns>
    /// <example>
    /// <code>
    /// // Async pattern matching
    /// var result = await user?.Id.MatchAsync(
    ///     some: async (id, ct) => await userService.GetProfileAsync(id, ct),
    ///     none: async ct => await guestService.CreateTemporaryProfileAsync(ct),
    ///     cancellationToken);
    /// 
    /// // With Find
    /// var response = await orders
    ///     .Find(o => o.Id == orderId)
    ///     .MatchAsync(
    ///         some: async (order, ct) => await BuildOrderResponseAsync(order, ct),
    ///         none: async ct => await Task.FromResult(OrderResponse.NotFound),
    ///         cancellationToken);
    /// 
    /// // Load data conditionally
    /// var data = await maybeUser.MatchAsync(
    ///     some: async (u, ct) => await LoadUserDataAsync(u.Id, ct),
    ///     none: async ct => await LoadDefaultDataAsync(ct),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TSource, TResult>(
        this TSource value,
        Func<TSource, CancellationToken, Task<TResult>> some,
        Func<CancellationToken, Task<TResult>> none,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value != null)
        {
            return some != null ? await some(value, cancellationToken).AnyContext() : default;
        }

        return none != null ? await none(cancellationToken).AnyContext() : default;
    }

    /// <summary>
    /// Asynchronously pattern matches on a nullable value (without cancellation token parameter).
    /// Can be called on sync or async value. Returns default if both functions are null.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="value">The nullable value to match on.</param>
    /// <param name="some">Async function to execute when value is not null. Can be null.</param>
    /// <param name="none">Async function to execute when value is null. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the result of the matching function, or default.</returns>
    /// <example>
    /// <code>
    /// // Simpler async without CT parameter
    /// var profile = await users
    ///     .Find(u => u.Id == userId)
    ///     .MatchAsync(
    ///         some: async user => await profileService.LoadAsync(user.Id),
    ///         none: async () => await profileService.GetDefaultAsync(),
    ///         cancellationToken);
    /// 
    /// // Method group syntax
    /// var display = await customer?.Email.MatchAsync(
    ///         some: emailService.ValidateAndFormatAsync,
    ///         none: () => Task.FromResult("No email on file"),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TSource, TResult>(
        this TSource value,
        Func<TSource, Task<TResult>> some,
        Func<Task<TResult>> none,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value != null)
        {
            return some != null ? await some(value).AnyContext() : default;
        }

        return none != null ? await none().AnyContext() : default;
    }

    /// <summary>
    /// Asynchronously pattern matches on an async result.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="sourceTask">The async task result to match on.</param>
    /// <param name="some">Async function to execute when value is not null. Can be null.</param>
    /// <param name="none">Async function to execute when value is null. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the result of the matching function, or default.</returns>
    /// <example>
    /// <code>
    /// // Async match on async result
    /// var result = await orders
    ///     .FindAsync(o => o.Id == orderId, cancellationToken)
    ///     .MatchAsync(
    ///         some: async (order, ct) => await ProcessOrderAsync(order, ct),
    ///         none: async ct => await LogOrderNotFoundAsync(orderId, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TSource, TResult>(
        this Task<TSource> sourceTask,
        Func<TSource, CancellationToken, Task<TResult>> some,
        Func<CancellationToken, Task<TResult>> none,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null)
        {
            return default;
        }

        var value = await sourceTask.AnyContext();
        if (value != null)
        {
            return some != null ? await some(value, cancellationToken).AnyContext() : default;
        }

        return none != null ? await none(cancellationToken).AnyContext() : default;
    }

    /// <summary>
    /// Asynchronously pattern matches on an async result (without cancellation token parameter).
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="sourceTask">The async task result to match on.</param>
    /// <param name="some">Async function to execute when value is not null. Can be null.</param>
    /// <param name="none">Async function to execute when value is null. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the result of the matching function, or default.</returns>
    /// <example>
    /// <code>
    /// var profile = await userService.GetUserAsync(userId, cancellationToken)
    ///     .MatchAsync(
    ///         some: async u => await LoadProfileAsync(u.Id),
    ///         none: async () => CreateDefaultProfile(),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TSource, TResult>(
        this Task<TSource> sourceTask,
        Func<TSource, Task<TResult>> some,
        Func<Task<TResult>> none,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null)
        {
            return default;
        }

        var value = await sourceTask.AnyContext();
        if (value != null)
        {
            return some != null ? await some(value).AnyContext() : default;
        }

        return none != null ? await none().AnyContext() : default;
    }

    /// <summary>
    /// Asynchronously pattern matches on a nullable value type.
    /// Can be called on sync or async value. Returns default if both functions are null.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="value">The nullable value to match on.</param>
    /// <param name="some">Async function to execute when value has a value. Can be null.</param>
    /// <param name="none">Async function to execute when value has no value. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the result of the matching function, or default.</returns>
    /// <example>
    /// <code>
    /// var tracking = await order.ShippedDate.MatchAsync(
    ///     some: async (date, ct) => await trackingService.GetInfoAsync(date, ct),
    ///     none: async ct => await trackingService.GetPendingInfoAsync(ct),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TSource, TResult>(
        this TSource? value,
        Func<TSource, CancellationToken, Task<TResult>> some,
        Func<CancellationToken, Task<TResult>> none,
        CancellationToken cancellationToken = default)
        where TSource : struct
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value.HasValue)
        {
            return some != null ? await some(value.Value, cancellationToken).AnyContext() : default;
        }

        return none != null ? await none(cancellationToken).AnyContext() : default;
    }

    /// <summary>
    /// Asynchronously pattern matches on a nullable value type (without cancellation token parameter).
    /// Can be called on sync or async value. Returns default if both functions are null.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="value">The nullable value to match on.</param>
    /// <param name="some">Async function to execute when value has a value. Can be null.</param>
    /// <param name="none">Async function to execute when value has no value. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the result of the matching function, or default.</returns>
    /// <example>
    /// <code>
    /// var status = await payment.ProcessedAt.MatchAsync(
    ///     some: async date => await FormatProcessedStatusAsync(date),
    ///     none: async () => await GetPendingStatusAsync(),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TSource, TResult>(
        this TSource? value,
        Func<TSource, Task<TResult>> some,
        Func<Task<TResult>> none,
        CancellationToken cancellationToken = default)
        where TSource : struct
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value.HasValue)
        {
            return some != null ? await some(value.Value).AnyContext() : default;
        }

        return none != null ? await none().AnyContext() : default;
    }

    #endregion

    #region OrElse - Fallback value factory

    /// <summary>
    /// Returns the value if not null, otherwise returns the result of the factory function.
    /// Useful when the default value is expensive to compute (lazy evaluation).
    /// Returns default if both value is null and factory is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="factory">Factory function to create default value. Can be null.</param>
    /// <returns>The original value or the factory result.</returns>
    /// <example>
    /// <code>
    /// // Lazy default computation
    /// var config = cachedConfig.OrElse(() => LoadConfigFromDatabase());
    /// 
    /// // Avoid expensive operations when value exists
    /// var user = users
    ///     .Find(u => u.Id == userId)
    ///     .OrElse(() => userRepository.FindById(userId));
    /// 
    /// // Fallback chain
    /// var address = primaryAddress
    ///     .OrElse(() => secondaryAddress)
    ///     .OrElse(() => defaultAddress);
    /// </code>
    /// </example>
    public static T OrElse<T>(this T value, Func<T> factory)
        where T : class
    {
        if (value != null)
        {
            return value;
        }

        return factory != null ? factory() : default;
    }

    /// <summary>
    /// Returns the value type if it has one, otherwise returns the result of the factory function.
    /// Returns default if both value has no value and factory is null.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="factory">Factory function to create default value. Can be null.</param>
    /// <returns>The original value or the factory result.</returns>
    /// <example>
    /// <code>
    /// int quantity = order.Quantity.OrElse(() => settings.DefaultQuantity);
    /// 
    /// DateTime deadline = task.DueDate.OrElse(() => DateTime.Now.AddDays(7));
    /// 
    /// // With Find
    /// var count = items
    ///     .FindValue(i => i.IsActive)
    ///     .OrElse(() => 0);
    /// </code>
    /// </example>
    public static T? OrElse<T>(this T? value, Func<T> factory)
        where T : struct
    {
        if (value.HasValue)
        {
            return value;
        }

        return factory != null ? factory() : null;
    }

    /// <summary>
    /// Asynchronously returns the value if not null, otherwise returns the result
    /// of the async factory function. Can be called on sync or async value.
    /// Returns default if both value is null and factory is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="factory">Async factory function to create default value. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value or the factory result.</returns>
    /// <example>
    /// <code>
    /// // Async fallback on sync value
    /// var user = await cachedUser.OrElseAsync(
    ///     async ct => await userRepository.FindByIdAsync(userId, ct),
    ///     cancellationToken);
    /// 
    /// // Multiple async fallbacks
    /// var settings = await localSettings.OrElseAsync(
    ///     async ct => await settingsService.FetchRemoteAsync(ct),
    ///     cancellationToken);
    /// 
    /// // With Find
    /// var customer = await customers
    ///     .Find(c => c.Email == email)
    ///     .OrElseAsync(
    ///         async ct => await customerService.LoadByEmailAsync(email, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> OrElseAsync<T>(
        this T value,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value != null)
        {
            return value;
        }

        return factory != null ? await factory(cancellationToken).AnyContext() : default;
    }

    /// <summary>
    /// Asynchronously returns the value if not null, otherwise returns the result
    /// of the async factory function (without cancellation token parameter).
    /// Can be called on sync or async value. Returns default if both value is null and factory is null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="factory">Async factory function to create default value. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value or the factory result.</returns>
    /// <example>
    /// <code>
    /// // Simpler async without CT parameter
    /// var config = await cachedConfig.OrElseAsync(
    ///     async () => await configService.LoadAsync(),
    ///     cancellationToken);
    /// 
    /// // Method group syntax
    /// var profile = await memoryCache.OrElseAsync(
    ///     externalApi.FetchProfileAsync,
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> OrElseAsync<T>(
        this T value,
        Func<Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value != null)
        {
            return value;
        }

        return factory != null ? await factory().AnyContext() : default;
    }

    /// <summary>
    /// Asynchronously returns the value type if it has one, otherwise returns the result
    /// of the async factory function. Can be called on sync or async value.
    /// Returns default if both value has no value and factory is null.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="factory">Async factory function to create default value. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value or the factory result.</returns>
    /// <example>
    /// <code>
    /// var rate = await cachedExchangeRate.OrElseAsync(
    ///     async ct => await exchangeService.GetCurrentRateAsync(ct),
    ///     cancellationToken);
    /// 
    /// var timestamp = await cachedTime.OrElseAsync(
    ///     async ct => await timeService.GetServerTimeAsync(ct),
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T?> OrElseAsync<T>(
        this T? value,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : struct
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value.HasValue)
        {
            return value;
        }

        return factory != null ? await factory(cancellationToken).AnyContext() : null;
    }

    /// <summary>
    /// Asynchronously returns the value type if it has one, otherwise returns the result
    /// of the async factory function (without cancellation token parameter).
    /// Can be called on sync or async value. Returns default if both value has no value and factory is null.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The nullable value to check.</param>
    /// <param name="factory">Async factory function to create default value. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value or the factory result.</returns>
    /// <example>
    /// <code>
    /// var timestamp = await cachedTimestamp.OrElseAsync(
    ///     timeService.GetServerTimeAsync,
    ///     cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T?> OrElseAsync<T>(
        this T? value,
        Func<Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : struct
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value.HasValue)
        {
            return value;
        }

        return factory != null ? await factory().AnyContext() : null;
    }

    /// <summary>
    /// Asynchronously returns the value if not null on async result, otherwise returns the result
    /// of the async factory function.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="sourceTask">The async task result to check.</param>
    /// <param name="factory">Async factory function to create default value. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value or the factory result.</returns>
    /// <example>
    /// <code>
    /// var user = await userService.GetCachedUserAsync(userId, cancellationToken)
    ///     .OrElseAsync(
    ///         async ct => await userService.LoadFromDatabaseAsync(userId, ct),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> OrElseAsync<T>(
        this Task<T> sourceTask,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null)
        {
            return factory != null ? await factory(cancellationToken).AnyContext() : default;
        }

        var value = await sourceTask.AnyContext();
        if (value != null)
        {
            return value;
        }

        return factory != null ? await factory(cancellationToken).AnyContext() : default;
    }

    /// <summary>
    /// Asynchronously returns the value if not null on async result, otherwise returns the result
    /// of the async factory function (without cancellation token parameter).
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="sourceTask">The async task result to check.</param>
    /// <param name="factory">Async factory function to create default value. Can be null.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task containing the original value or the factory result.</returns>
    /// <example>
    /// <code>
    /// var config = await configCache.GetAsync(key, cancellationToken)
    ///     .OrElseAsync(
    ///         () => configService.LoadAsync(key),
    ///         cancellationToken);
    /// </code>
    /// </example>
    public static async Task<T> OrElseAsync<T>(
        this Task<T> sourceTask,
        Func<Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceTask == null)
        {
            return factory != null ? await factory().AnyContext() : default;
        }

        var value = await sourceTask.AnyContext();
        if (value != null)
        {
            return value;
        }

        return factory != null ? await factory().AnyContext() : default;
    }

    #endregion
}
