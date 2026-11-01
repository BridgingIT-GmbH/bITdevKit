// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

/// <summary>Maps prepared sequence Results through existing HTTP error conventions.</summary>
/// <example><code>return (await requester.SendAsync(query, cancellationToken: ct)).MapHttpNdjson();</code></example>
public static class ResultMapNdjsonExtensions
{
    /// <summary>Maps a prepared IAsyncEnumerable&lt;T&gt; without enumerating a failed value.</summary>
    /// <example><code>return result.MapHttpNdjson();</code></example>
    public static Results<NdjsonHttpResult<T>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpNdjson<T>(this Result<IAsyncEnumerable<T>> result, NdjsonEndpointOptions options = null, ILogger logger = null) =>
        Map<T, IAsyncEnumerable<T>>(result, value => NdjsonHttpResult.From(value, options), logger);

    /// <summary>Maps a prepared IEnumerable&lt;T&gt; without enumerating a failed value.</summary>
    /// <example><code>return result.MapHttpNdjson();</code></example>
    public static Results<NdjsonHttpResult<T>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpNdjson<T>(this Result<IEnumerable<T>> result, NdjsonEndpointOptions options = null, ILogger logger = null) =>
        Map<T, IEnumerable<T>>(result, value => NdjsonHttpResult.From(value, options), logger);

    /// <summary>Maps a prepared List&lt;T&gt; without enumerating a failed value.</summary>
    /// <example><code>return result.MapHttpNdjson();</code></example>
    public static Results<NdjsonHttpResult<T>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpNdjson<T>(this Result<List<T>> result, NdjsonEndpointOptions options = null, ILogger logger = null) =>
        Map<T, List<T>>(result, value => NdjsonHttpResult.From(value, options), logger);

    /// <summary>Maps a prepared T[] without enumerating a failed value.</summary>
    /// <example><code>return result.MapHttpNdjson();</code></example>
    public static Results<NdjsonHttpResult<T>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpNdjson<T>(this Result<T[]> result, NdjsonEndpointOptions options = null, ILogger logger = null) =>
        Map<T, T[]>(result, value => NdjsonHttpResult.From(value, options), logger);

    private static Results<NdjsonHttpResult<T>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> Map<T, TSequence>(Result<TSequence> result, Func<TSequence, NdjsonHttpResult<T>> success, ILogger logger)
        where TSequence : class
    {
        if (result.IsSuccess) { return success(result.Value); }

        // Specialized MapOk retains the repository's Problem Details validation mapping
        // and custom error handlers without the generic mapper's BadRequest cast.
        return ResultMapHttpExtensions.MapOk(result, logger).Result switch
        {
            NotFound missing => missing,
            UnauthorizedHttpResult unauthorized => unauthorized,
            BadRequest bad => bad,
            ProblemHttpResult problem => problem,
            Ok<TSequence> customSuccess => success(customSuccess.Value),
            _ => throw new InvalidOperationException("Unsupported HTTP result mapping.")
        };
    }
}
