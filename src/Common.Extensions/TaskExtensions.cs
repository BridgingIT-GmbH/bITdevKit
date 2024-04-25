// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public static class TaskExtensions
{
    /// <summary>
    ///  Start a task but don't to wait for it to finish
    /// </summary>
    /// <example>
    /// SendEmailAsync().FireAndForget(errorHandler => Console.WriteLine(errorHandler.Message));
    /// </example>
    [DebuggerStepThrough]
    public static void Forget(
        this Task task,
        Action<Exception> errorHandler = null)
    {
        if (task is null)
        {
            throw new ArgumentNullException(nameof(task));
        }

        task.ContinueWith(t =>
        {
            if (t.IsFaulted && errorHandler is not null)
            {
                errorHandler(t.Exception);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    ///  Start a task but don''t to wait for it to finish
    /// </summary>
    /// <example>
    /// SendEmailAsync().FireAndForget(errorHandler => Console.WriteLine(errorHandler.Message));
    /// </example>
    [DebuggerStepThrough]
    public static void Forget(
        this ValueTask task,
        Action<Exception> errorHandler = null)
    {
        task.Forget(errorHandler);
    }

    /// <summary>
    /// Retry a task a specific number of times
    /// </summary>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static async Task Retry(this Task task, int retryCount, TimeSpan delay)
    {
        for (var i = 0; i < retryCount; i++)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch when (i != retryCount - 1)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Retry a task a specific number of times
    /// </summary>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static async Task<TResult> Retry<TResult>(this Task<TResult> task, int retryCount, TimeSpan delay)
    {
        for (var i = 0; i < retryCount; i++)
        {
            try
            {
                return await task.ConfigureAwait(false);
            }
            catch when (i != retryCount - 1)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }

        return default; // Should not be reached
    }

    /// <summary>
    /// Retry a task a specific number of times
    /// </summary>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static async Task<TResult> Retry<TResult>(this ValueTask<TResult> task, int retryCount, TimeSpan delay)
    {
        for (var i = 0; i < retryCount; i++)
        {
            try
            {
                return await task.ConfigureAwait(false);
            }
            catch when (i != retryCount - 1)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }

        return default; // Should not be reached
    }

    /// <summary>
    /// Retry a task a specific number of times
    /// </summary>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static async Task Retry(this ValueTask task, int retryCount, TimeSpan delay)
    {
        for (var i = 0; i < retryCount; i++)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch when (i != retryCount - 1)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Retry a task a specific number of times
    /// </summary>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static async Task<TResult> Retry<TResult>(this Func<Task<TResult>> taskFactory, int retryCount, TimeSpan delay)
    {
        for (var i = 0; i < retryCount; i++)
        {
            try
            {
                return await taskFactory().ConfigureAwait(false);
            }
            catch when (i != retryCount - 1)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }

        return default; // Should not be reached
    }

    /// <summary>
    /// Retry a task a specific number of times
    /// </summary>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static async Task<TResult> Retry<TResult>(this Func<ValueTask<TResult>> taskFactory, int retryCount, TimeSpan delay)
    {
        return await taskFactory.Retry(retryCount, delay);
    }

    /// <summary>
    /// Executes a callback function when a Task encounters an exception.
    /// </summary>
    /// <returns></returns>
    /// <example>
    /// await GetResultAsync().OnFailure(ex => Console.WriteLine(ex.Message));
    /// </example>
    [DebuggerStepThrough]
    public static async Task OnFailure(this Task task, Action<Exception> onFailure)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            onFailure(ex);
        }
    }

    /// <summary>
    /// Executes a callback function when a Task encounters an exception.
    /// </summary>
    /// <returns></returns>
    /// <example>
    /// await GetResultAsync().OnFailure(ex => Console.WriteLine(ex.Message));
    /// </example>
    [DebuggerStepThrough]
    public static async Task OnFailure(this ValueTask task, Action<Exception> onFailure)
    {
        await task.OnFailure(onFailure);
    }

    /// <summary>
    ///  Set a timeout for a task. If the task takes longer than the timeout the task will be cancelled.
    /// </summary>
    /// <returns></returns>
    /// <example>
    /// await GetResultAsync().WithTimeout(TimeSpan.FromSeconds(1));
    /// </example>
    [DebuggerStepThrough]
    public static async Task WithTimeout(this Task task, TimeSpan timeout)
    {
        var delayTask = Task.Delay(timeout); //.ConfigureAwait(false);
        var completedTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

        if (completedTask == delayTask)
        {
            throw new TimeoutException();
        }

        await task;
    }

    /// <summary>
    ///  Set a timeout for a task. If the task takes longer than the timeout the task will be cancelled.
    /// </summary>
    /// <returns></returns>
    /// <example>
    /// await GetResultAsync().WithTimeout(TimeSpan.FromSeconds(1));
    /// </example>
    [DebuggerStepThrough]
    public static async Task WithTimeout(this ValueTask task, TimeSpan timeout)
    {
        await task.WithTimeout(timeout);
    }

    /// <summary>
    /// Use a fallback value when a task fails
    /// </summary>
    /// <returns></returns>
    /// <example>
    /// var result = await GetResultAsync().Fallback("fallback");
    /// </example>
    [DebuggerStepThrough]
    public static async Task<TResult> Fallback<TResult>(this Task<TResult> task, TResult fallbackValue)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch
        {
            return fallbackValue;
        }
    }

    /// <summary>
    /// Use a fallback value when a task fails
    /// </summary>
    /// <returns></returns>
    /// <example>
    /// var result = await GetResultAsync().Fallback("fallback");
    /// </example>
    [DebuggerStepThrough]
    public static async Task<TResult> Fallback<TResult>(this ValueTask<TResult> task, TResult fallbackValue)
    {
        return await task.Fallback(fallbackValue);
    }

    [DebuggerStepThrough]
    public static ConfiguredTaskAwaitable<TResult> AnyContext<TResult>(this Task<TResult> task)
    {
        if (task is null)
        {
            throw new ArgumentNullException(nameof(task));
        }

        return task.ConfigureAwait(false);
    }

    [DebuggerStepThrough]
    public static ConfiguredTaskAwaitable AnyContext(this Task task)
    {
        if (task is null)
        {
            throw new ArgumentNullException(nameof(task));
        }

        return task.ConfigureAwait(false);
    }

    [DebuggerStepThrough]
    public static ConfiguredValueTaskAwaitable<TResult> AnyContext<TResult>(this ValueTask<TResult> task)
    {
        //if (task is null)
        //{
        //    throw new ArgumentNullException(nameof(task));
        //}

        return task.ConfigureAwait(false);
    }

    [DebuggerStepThrough]
    public static ConfiguredValueTaskAwaitable AnyContext(this ValueTask task)
    {
        //if (task is null)
        //{
        //    throw new ArgumentNullException(nameof(task));
        //}

        return task.ConfigureAwait(false);
    }
}