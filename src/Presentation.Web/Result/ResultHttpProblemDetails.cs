// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Diagnostics;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>Represents a Result failure with exception diagnostics that the host can opt in to returning.</summary>
/// <example><code>var problem = ResultHttpProblemDetails.Create(500, result: result);</code></example>
public sealed class ResultHttpProblemDetails : ProblemDetails
{
    private readonly IResultError[] exceptionErrors;

    private ResultHttpProblemDetails(IResultError[] exceptionErrors)
    {
        this.exceptionErrors = exceptionErrors;
        this.Detail = "An unexpected error occurred.";
    }

    /// <summary>Creates a Problem Details response with exception details hidden and ambient correlation when available.</summary>
    /// <param name="status">The HTTP response status.</param>
    /// <param name="title">An optional title for the problem.</param>
    /// <param name="result">An optional failure to retain for host-controlled exception diagnostics.</param>
    /// <returns>The Problem Details response.</returns>
    /// <example><code>return TypedResults.Problem(ResultHttpProblemDetails.Create(404));</code></example>
    public static ProblemDetails Create(int status, string title = null, Common.IResult result = null)
    {
        var exceptionErrors = result?.Errors?.Where(error =>
                error is ExceptionError or RuleExceptionError or FileSystemPermissionError { Details: not null }
                || GetException(error) is not null)
            .ToArray();
        var problem = exceptionErrors is { Length: > 0 }
            ? new ResultHttpProblemDetails(exceptionErrors)
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = title ?? ReasonPhrases.GetReasonPhrase(status);
        problem.Type = $"https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/{status}";
        SetCorrelationId(problem);
        return problem;
    }

    /// <summary>Applies request correlation and host-controlled diagnostics to a Problem Details response.</summary>
    /// <param name="problem">The problem to prepare.</param>
    /// <param name="context">The HTTP context for the response.</param>
    /// <returns>The Problem Details to serialize.</returns>
    /// <example><code>var response = ResultHttpProblemDetails.PrepareResponse(problem, httpContext);</code></example>
    public static ProblemDetails PrepareResponse(ProblemDetails problem, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(context);

        if (problem is ResultHttpProblemDetails exceptionProblem)
        {
            var options = context.RequestServices.GetService<IOptions<ResultHttpProblemDetailsOptions>>();
            problem = options?.Value.IncludeExceptionDetails == true
                ? exceptionProblem.WithExceptionDetails()
                : Copy(problem);
        }
        else if (problem.GetType() == typeof(ProblemDetails))
        {
            problem = Copy(problem);
        }

        SetCorrelationId(problem, context);
        return problem;
    }

    private static void SetCorrelationId(ProblemDetails problem, HttpContext context = null)
    {
        var id = context?.TryGetCorrelationId() ?? CorrelationId.Current
            ?? Activity.Current?.TraceId.ToHexString() ?? context?.TraceIdentifier;
        if (id is not null)
        {
            problem.Extensions["correlationId"] = id;
        }
    }

    private static ProblemDetails Copy(ProblemDetails problem) => new()
    {
        Detail = problem.Detail,
        Instance = problem.Instance,
        Status = problem.Status,
        Title = problem.Title,
        Type = problem.Type,
        Extensions = new Dictionary<string, object>(problem.Extensions)
    };

    private ProblemDetails WithExceptionDetails()
    {
        var response = Copy(this);
        response.Extensions["exceptionDetails"] = this.exceptionErrors.Select(error =>
            GetException(error)?.ToString() ??
            (error is FileSystemPermissionError permission ? permission.Details : error.Message)).ToArray();

        return response;
    }

    private static Exception GetException(IResultError error) => error switch
    {
        ExceptionError exception => exception.Exception,
        RuleExceptionError rule => rule.Exception,
        BlobStorageError storage => storage.InnerException,
        DecryptionError decryption => decryption.InnerException,
        DocumentStoreInvalidContinuationTokenError continuation => continuation.InnerException,
        DocumentStorePartialUpdateError partialUpdate => partialUpdate.InnerException,
        DocumentStoreProviderError provider => provider.InnerException,
        DocumentStoreSerializationError serialization => serialization.InnerException,
        ExternalServiceError externalService => externalService.InnerException,
        FileSystemError fileSystem => fileSystem.InnerException,
        HttpError http => http.InnerException,
        NotificationError notification => notification.InnerException,
        PartialOperationError partialOperation => partialOperation.InnerException,
        ServiceUnavailableError unavailable => unavailable.InnerException,
        _ => null
    };
}
