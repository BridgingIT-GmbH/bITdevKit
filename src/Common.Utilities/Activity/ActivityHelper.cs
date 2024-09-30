// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

/// <summary>
/// Provides helper methods for starting and managing activities.
/// </summary>
public static class ActivityHelper
{
    /// <summary>
    /// Starts an activity with the specified parameters.
    /// </summary>
    /// <param name="source">The source activity.</param>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="kind">The kind of activity.</param>
    /// <param name="parentId">The parent ID of the activity.</param>
    /// <param name="tags">The tags to add to the activity.</param>
    /// <param name="baggages">The baggages to add to the activity.</param>
    /// <param name="displayName">The display name of the activity.</param>
    /// <param name="throwException">Whether to throw an exception if the operation fails.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task StartActvity(
        this Activity source,
        string operationName,
        Func<Activity, CancellationToken, Task> operation,
        ActivityKind kind = ActivityKind.Internal,
        string parentId = null,
        IDictionary<string, string> tags = null,
        IDictionary<string, string> baggages = null,
        string displayName = null,
        bool throwException = true,
        CancellationToken cancellationToken = default)
    {
        if (source?.Source is null)
        {
            await operation(source, cancellationToken);
        }

        await source.Source.StartActvity(operationName,
            operation,
            kind,
            parentId,
            tags,
            baggages,
            displayName,
            throwException,
            cancellationToken);
    }

    /// <summary>
    /// Starts an activity with the specified parameters.
    /// </summary>
    /// <param name="source">The source activity.</param>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="kind">The kind of activity.</param>
    /// <param name="parentId">The parent ID of the activity.</param>
    /// <param name="tags">The tags to add to the activity.</param>
    /// <param name="baggages">The baggages to add to the activity.</param>
    /// <param name="displayName">The display name of the activity.</param>
    /// <param name="throwException">Whether to throw an exception if the operation fails.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task StartActvity(
        this ActivitySource source,
        string operationName,
        Func<Activity, CancellationToken, Task> operation,
        ActivityKind kind = ActivityKind.Internal,
        string parentId = null,
        IDictionary<string, string> tags = null,
        IDictionary<string, string> baggages = null,
        string displayName = null,
        bool throwException = true,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return;
        }

        if (source is null)
        {
            await operation(default, cancellationToken);
        }
        else
        {
            using var activity = source.StartActivity(operationName, kind, parentId);

            if (activity is not null)
            {
                activity.DisplayName = displayName ?? operationName;

                foreach (var tag in tags.SafeNull())
                {
                    activity.SetTag(tag.Key, tag.Value);
                }

                foreach (var baggage in baggages.SafeNull())
                {
                    activity.SetBaggage(baggage.Key, baggage.Value);
                }
            }

            var success = false;
            try
            {
                await operation(activity, cancellationToken);
                success = true;
            }
            catch (Exception ex)
            {
                RecordException(activity, ex);

                if (throwException)
                {
                    throw;
                }
            }
            finally
            {
                activity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            }
        }
    }

    /// <summary>
    /// Starts an activity with the specified parameters and returns a result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source activity.</param>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="kind">The kind of activity.</param>
    /// <param name="tags">The tags to add to the activity.</param>
    /// <param name="baggages">The baggages to add to the activity.</param>
    /// <param name="displayName">The display name of the activity.</param>
    /// <param name="throwException">Whether to throw an exception if the operation fails.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a result of type <typeparamref name="TResult"/>.</returns>
    public static async Task<TResult> StartActvity<TResult>(
        this Activity source,
        string operationName,
        Func<Activity, CancellationToken, Task<TResult>> operation,
        ActivityKind kind = ActivityKind.Internal,
        IDictionary<string, string> tags = null,
        IDictionary<string, string> baggages = null,
        string displayName = null,
        bool throwException = true,
        CancellationToken cancellationToken = default)
    {
        if (source?.Source is null)
        {
            return await operation(source, cancellationToken);
        }

        return await source.Source.StartActvity(operationName,
            operation,
            kind,
            tags,
            baggages,
            displayName,
            throwException,
            cancellationToken);
    }

    /// <summary>
    /// Starts an activity with the specified parameters and returns a result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source activity.</param>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="kind">The kind of activity.</param>
    /// <param name="tags">The tags to add to the activity.</param>
    /// <param name="baggages">The baggages to add to the activity.</param>
    /// <param name="displayName">The display name of the activity.</param>
    /// <param name="throwException">Whether to throw an exception if the operation fails.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a result of type <typeparamref name="TResult"/>.</returns>
    public static async Task<TResult> StartActvity<TResult>(
        this ActivitySource source,
        string operationName,
        Func<Activity, CancellationToken, Task<TResult>> operation,
        ActivityKind kind = ActivityKind.Internal,
        IDictionary<string, string> tags = null,
        IDictionary<string, string> baggages = null,
        string displayName = null,
        bool throwException = true,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            return await operation(default, cancellationToken);
        }

        using var activity = source.StartActivity(operationName, kind);

        if (activity is not null)
        {
            activity.DisplayName = displayName ?? operationName;

            foreach (var tag in tags.SafeNull())
            {
                activity.SetTag(tag.Key, tag.Value);
            }

            foreach (var baggage in baggages.SafeNull())
            {
                activity.SetBaggage(baggage.Key, baggage.Value);
            }
        }

        TResult result = default;
        var success = false;
        try
        {
            result = await operation(activity, cancellationToken);
            success = true;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);

            if (throwException)
            {
                throw;
            }
        }
        finally
        {
            activity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }

        return result;
    }

    /// <summary>
    /// Records an exception in the activity.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="ex">The exception to record.</param>
    private static void RecordException(Activity activity, Exception ex)
    {
        if (activity is null || ex is null)
        {
            return;
        }

        activity?.AddTag("exception.type", ex.GetType().Name);
        activity?.AddTag("exception.message", ex.Message);
        activity?.AddTag("exception.stacktrace", ex.StackTrace);
    }
}