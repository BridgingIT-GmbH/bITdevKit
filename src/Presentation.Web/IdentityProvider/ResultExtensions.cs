// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public static class ResultsExtensions
{
    /// <summary>
    /// Adds OAuth2 response headers to the result.
    /// </summary>
    public static IResult WithOAuthHeaders(this IResult result)
    {
        return new HeaderResult(result, new HeaderDictionary
        {
            { "Cache-Control", "no-store" },
            { "Pragma", "no-cache" }
        });
    }

    /// <summary>
    /// Adds custom headers to the result.
    /// </summary>
    public static IResult WithHeaders(this IResult result, HeaderDictionary headers)
    {
        return new HeaderResult(result, headers);
    }

    private sealed class HeaderResult : IResult
    {
        private readonly IResult result;
        private readonly HeaderDictionary headers;

        public HeaderResult(IResult result, HeaderDictionary headers)
        {
            this.result = result;
            this.headers = headers;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            foreach (var header in this.headers)
            {
                httpContext.Response.Headers[header.Key] = header.Value;
            }

            await this.result.ExecuteAsync(httpContext);
        }
    }
}
