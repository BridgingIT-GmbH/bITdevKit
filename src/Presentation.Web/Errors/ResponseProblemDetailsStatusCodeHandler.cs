// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Errors;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Problem = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>Configures the default DevKit response for eligible empty HTTP error statuses.</summary>
public sealed class ResponseProblemDetailsStatusCodeHandler : IPostConfigureOptions<StatusCodePagesOptions>
{
    private static readonly Func<StatusCodeContext, Task> DefaultHandler = new StatusCodePagesOptions().HandleAsync;

    /// <inheritdoc />
    public void PostConfigure(string name, StatusCodePagesOptions options)
    {
        if (name == Options.DefaultName && options.HandleAsync == DefaultHandler)
        {
            options.HandleAsync = WriteStatusAsync;
        }
    }

    /// <summary>Writes the public JSON problem and request identifiers when no registered writer accepts the response.</summary>
    /// <param name="context">The HTTP request and response.</param>
    /// <param name="problem">The mapped public problem.</param>
    /// <param name="cancellationToken">A token that cancels response writing.</param>
    /// <returns>A task that completes when the response is written.</returns>
    public static Task WriteFallbackAsync(HttpContext context, Problem problem, CancellationToken cancellationToken)
    {
        var response = ProblemDetailsPayload.Copy(problem);
        response.Extensions.Remove("exceptionDetails");
        ProblemDetailsPayload.SetCorrelation(response, context, automatic: true);
        ProblemDetailsPayload.SetTrace(response, context);
        return context.Response.WriteAsJsonAsync(response, response.GetType(), options: null, ProblemDetailsPayload.ContentType, cancellationToken);
    }

    private static async Task WriteStatusAsync(StatusCodeContext statusContext)
    {
        var http = statusContext.HttpContext;
        var options = http.RequestServices.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value;
        if (ResponseProblemDetailsService.HasFailedWrite(http) || !options.Handles(http))
        {
            return;
        }

        var status = http.Response.StatusCode;
        var problem = new Problem
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Type = $"https://httpstatuses.io/{status}"
        };
        ProblemDetailsPayload.SetCorrelation(problem, http, automatic: true);
        var headers = http.Response.Headers;
        foreach (var name in headers.Keys.Where(name => !options.AllowedHeaderNames.Contains(name)).ToArray())
        {
            headers.Remove(name);
        }

        headers.CacheControl = "no-cache, no-store";
        headers.Pragma = "no-cache";
        headers.Expires = "-1";

        var service = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        if (!await service.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem }))
        {
            await WriteFallbackAsync(http, problem, http.RequestAborted);
        }
    }
}
