// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;

public static class ResultLinqExtensions
{
    /// <summary>
    /// Converts a collection to a collection of successful Results.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The collection to convert.</param>
    /// <returns>An enumerable of successful Results, one for each item.</returns>
    /// <example>
    /// <code>
    /// var results = customers
    ///     .ToResult()
    ///     .Bind(customer => ValidateCustomer(customer))
    ///     .Flatten();
    /// </code>
    /// </example>
    public static IEnumerable<Result<T>> ToResults<T>(this ICollection<T> items)
    {
        foreach (var item in items.SafeNull())
        {
            yield return Result<T>.Success(item);
        }
    }

    /// <summary>
    /// Converts an enumerable to a collection of successful Results.
    /// </summary>
    /// <typeparam name="T">The type of items in the enumerable.</typeparam>
    /// <param name="items">The enumerable to convert.</param>
    /// <returns>An enumerable of successful Results, one for each item.</returns>
    /// <example>
    /// <code>
    /// var results = GetUserIds()
    ///     .ToResult()
    ///     .Bind(id => FetchUser(id))
    ///     .Flatten();
    /// </code>
    /// </example>
    public static IEnumerable<Result<T>> ToResults<T>(this IEnumerable<T> items)
    {
        foreach (var item in items.SafeNull())
        {
            yield return Result<T>.Success(item);
        }
    }

    /// <summary>
    /// Converts a dictionary to a collection of successful Results containing key-value pairs.
    /// </summary>
    /// <typeparam name="TKey">The type of the dictionary keys.</typeparam>
    /// <typeparam name="TValue">The type of the dictionary values.</typeparam>
    /// <param name="items">The dictionary to convert.</param>
    /// <returns>An enumerable of successful Results, one for each key-value pair.</returns>
    /// <example>
    /// <code>
    /// var results = settings
    ///     .ToResult()
    ///     .Bind(kvp => ValidateSetting(kvp.Key, kvp.Value))
    ///     .Flatten();
    /// </code>
    /// </example>
    public static IEnumerable<Result<KeyValuePair<TKey, TValue>>> ToResults<TKey, TValue>(
        this IDictionary<TKey, TValue> items)
    {
        foreach (var item in items.SafeNull())
        {
            yield return Result<KeyValuePair<TKey, TValue>>.Success(item);
        }
    }

    /// <summary>
    /// Binds each Result in a collection to a function that returns a Result.
    /// Preserves messages and errors from previous Results in the chain.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TVal">The type of items in the input Results.</typeparam>
    /// <param name="results">The collection of Results to bind.</param>
    /// <param name="binder">The function to apply to each successful Result's value.</param>
    /// <returns>A collection of Results from each operation.</returns>
    /// <example>
    /// <code>
    /// var results = customers
    ///     .ToResult()
    ///     .Bind(customer => ValidateCustomer(customer))
    ///     .Bind(customer => SaveCustomer(customer))
    ///     .Flatten();
    /// </code>
    /// </example>
    public static ICollection<Result<T>> Bind<T, TVal>(
        this ICollection<Result<TVal>> results,
        Func<TVal, Result<T>> binder)
    {
        if (results is null || binder is null)
        {
            return [];
        }

        return [.. results.Select(r => r.Bind(binder)) ?? []];
    }

    /// <summary>
    /// Binds each Result in an enumerable to a function that returns a Result.
    /// Preserves messages and errors from previous Results in the chain.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TVal">The type of items in the input Results.</typeparam>
    /// <param name="results">The enumerable of Results to bind.</param>
    /// <param name="binder">The function to apply to each successful Result's value.</param>
    /// <returns>An enumerable of Results from each operation.</returns>
    /// <example>
    /// <code>
    /// var results = GetUserIds()
    ///     .ToResult()
    ///     .Bind(id => FetchUser(id))
    ///     .Bind(user => EnrichUser(user))
    ///     .Flatten();
    /// </code>
    /// </example>
    public static IEnumerable<Result<T>> Bind<T, TVal>(
        this IEnumerable<Result<TVal>> results,
        Func<TVal, Result<T>> binder)
    {
        if (results is null || binder is null)
        {
            return [];
        }

        return results.Select(r => r.Bind(binder)) ?? Enumerable.Empty<Result<T>>();
    }

    /// <summary>
    /// Binds each Result in a collection to an async function that returns a Result.
    /// Preserves messages and errors from previous Results in the chain.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TVal">The type of items in the input Results.</typeparam>
    /// <param name="results">The collection of Results to bind.</param>
    /// <param name="binder">The async function to apply to each successful Result's value.</param>
    /// <returns>A task containing a collection of Results from each operation.</returns>
    /// <example>
    /// <code>
    /// var results = await customers
    ///     .ToResult()
    ///     .BindAsync(async customer => await ValidateCustomerAsync(customer))
    ///     .BindAsync(async customer => await SaveCustomerAsync(customer))
    ///     .FlattenAsync();
    /// </code>
    /// </example>
    public static async Task<ICollection<Result<T>>> BindAsync<T, TVal>(
        this ICollection<Result<TVal>> results,
        Func<TVal, Task<Result<T>>> binder)
    {
        var tasks = results?.Select(async r =>
        {
            try
            {
                if (r.IsSuccess)
                {
                    var result = await binder(r.Value);
                    return result
                        .WithMessages(r.Messages)
                        .WithErrors(r.Errors);
                }

                return Result<T>.Failure()
                    .WithMessages(r.Messages)
                    .WithErrors(r.Errors);
            }
            catch (Exception ex)
            {
                return Result<T>.Failure()
                    .WithMessage(ex.Message)
                    .WithError(Result.Settings.ExceptionErrorFactory(ex))
                    .WithMessages(r.Messages)
                    .WithErrors(r.Errors);
            }
        }) ?? [];

        return [.. await Task.WhenAll(tasks)];
    }

    /// <summary>
    /// Binds each Result in an enumerable to an async function that returns a Result.
    /// Preserves messages and errors from previous Results in the chain.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TVal">The type of items in the input Results.</typeparam>
    /// <param name="results">The enumerable of Results to bind.</param>
    /// <param name="binder">The async function to apply to each successful Result's value.</param>
    /// <returns>A task containing an enumerable of Results from each operation.</returns>
    /// <example>
    /// <code>
    /// var results = await fileNames
    ///     .ToResult()
    ///     .BindAsync(async file => await ProcessFileAsync(file))
    ///     .FlattenAsync();
    /// </code>
    /// </example>
    public static async Task<IEnumerable<Result<T>>> BindAsync<T, TVal>(
        this IEnumerable<Result<TVal>> results,
        Func<TVal, Task<Result<T>>> binder)
    {
        var tasks = results?.Select(async r =>
        {
            try
            {
                if (r.IsSuccess)
                {
                    var result = await binder(r.Value);
                    return result
                        .WithMessages(r.Messages)
                        .WithErrors(r.Errors);
                }

                return Result<T>.Failure()
                    .WithMessages(r.Messages)
                    .WithErrors(r.Errors);
            }
            catch (Exception ex)
            {
                return Result<T>.Failure()
                    .WithMessage(ex.Message)
                    .WithError(Result.Settings.ExceptionErrorFactory(ex))
                    .WithMessages(r.Messages)
                    .WithErrors(r.Errors);
            }
        }) ?? [];

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Binds each Result in a collection to an async function that returns a Result.
    /// Preserves messages and errors from previous Results in the chain.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TVal">The type of items in the input Results.</typeparam>
    /// <param name="resultTask">A task containing the collection of Results to bind.</param>
    /// <param name="binder">The async function to apply to each successful Result's value.</param>
    /// <returns>A task containing a collection of Results from each operation.</returns>
    /// <example>
    /// <code>
    /// var results = await GetCustomersAsync()
    ///     .BindAsync(async customer => await ValidateCustomerAsync(customer))
    ///     .FlattenAsync();
    /// </code>
    /// </example>
    public static async Task<ICollection<Result<T>>> BindAsync<T, TVal>(
        this Task<ICollection<Result<TVal>>> resultTask,
        Func<TVal, Task<Result<T>>> binder)
    {
        var results = await resultTask;
        return await results.BindAsync(binder);
    }

    /// <summary>
    /// Binds each Result in an enumerable to an async function that returns a Result.
    /// Preserves messages and errors from previous Results in the chain.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TVal">The type of items in the input Results.</typeparam>
    /// <param name="resultTask">A task containing the enumerable of Results to bind.</param>
    /// <param name="binder">The async function to apply to each successful Result's value.</param>
    /// <returns>A task containing an enumerable of Results from each operation.</returns>
    /// <example>
    /// <code>
    /// var results = await GetUserIdsAsync()
    ///     .BindAsync(async id => await FetchUserAsync(id))
    ///     .FlattenAsync();
    /// </code>
    /// </example>
    public static async Task<IEnumerable<Result<T>>> BindAsync<T, TVal>(
        this Task<IEnumerable<Result<TVal>>> resultTask,
        Func<TVal, Task<Result<T>>> binder)
    {
        var results = await resultTask;
        return await results.BindAsync(binder);
    }
}
