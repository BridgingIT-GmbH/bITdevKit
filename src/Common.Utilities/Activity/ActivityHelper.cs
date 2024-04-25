// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Diagnostics;
using System.Threading.Tasks;

public static class ActivityHelper
{
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

        await source.Source.StartActvity(operationName, operation, kind, parentId, tags, baggages, displayName, throwException, cancellationToken);
    }

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

        return await source.Source.StartActvity(operationName, operation, kind, tags, baggages, displayName, throwException, cancellationToken);
    }

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

    private static void RecordException(Activity activity, Exception ex)
    {
        if (activity is null || ex is null)
        {
            return;
        }

        activity?.AddTag("exception.type", ex.GetType().Name);
        activity?.AddTag("exception.message", ex.Message);
        activity?.AddTag("exception.stacktrace", ex.StackTrace.ToString());
    }
}
