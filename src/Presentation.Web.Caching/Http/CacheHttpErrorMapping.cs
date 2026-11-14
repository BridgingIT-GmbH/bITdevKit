// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Registers cache failures once with the existing shared Result Problem Details mapper.</summary>
/// <example><code>CacheHttpErrorMapping.Register();</code></example>
public static class CacheHttpErrorMapping
{
    private static readonly Lock Gate = new();

    /// <summary>Installs bounded cache status classification without replacing the host's mapping for unrelated errors.</summary>
    public static void Register()
    {
        lock (Gate)
        {
            if (!ResultMapHttpExtensions.HasErrorHandlerFor<CacheError>())
            {
                ResultMapHttpExtensions.RegisterErrorHandler<CacheError>((_, result) =>
                {
                    var error = result.Errors.OfType<CacheError>().First();
                    var status = error.Code switch
                    {
                        CacheErrorCode.NotFound => StatusCodes.Status404NotFound,
                        CacheErrorCode.Conflict => StatusCodes.Status412PreconditionFailed,
                        CacheErrorCode.Capacity => StatusCodes.Status429TooManyRequests,
                        CacheErrorCode.SourceFailure => StatusCodes.Status502BadGateway,
                        CacheErrorCode.ProviderUnavailable or CacheErrorCode.Indeterminate or CacheErrorCode.SourceRejected => StatusCodes.Status503ServiceUnavailable,
                        CacheErrorCode.CoordinationTimeout or CacheErrorCode.SourceTimeout => StatusCodes.Status504GatewayTimeout,
                        CacheErrorCode.Corruption => StatusCodes.Status500InternalServerError,
                        _ => StatusCodes.Status400BadRequest
                    };
                    return TypedResults.Problem(statusCode: status, title: "Cache request failed", detail: error.Message,
                        extensions: new Dictionary<string, object> { ["code"] = error.Code.ToString() });
                });
            }

            if (!ResultMapHttpExtensions.HasErrorHandlerFor<CacheHttpRangeError>())
            {
                ResultMapHttpExtensions.RegisterErrorHandler<CacheHttpRangeError>((_, _) =>
                    TypedResults.Problem(statusCode: StatusCodes.Status416RangeNotSatisfiable, title: "Cache range is not satisfiable"));
            }
        }
    }
}

/// <summary>Identifies an unsatisfiable public byte-range request before response content begins.</summary>
/// <example><code>return Result.Failure(new CacheHttpRangeError());</code></example>
public sealed class CacheHttpRangeError() : ResultErrorBase("The requested byte range is not satisfiable.");