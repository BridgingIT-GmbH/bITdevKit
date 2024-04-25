// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Net.Http;
using System.Security;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;
using ProblemDetailsOptions = Hellang.Middleware.ProblemDetails.ProblemDetailsOptions;

public static class Configure
{
    public static void ProblemDetails(
        ProblemDetailsOptions options)
    {
        ProblemDetails(options, false);
    }

    public static void ProblemDetails(
        ProblemDetailsOptions options,
        bool includeExceptionDetails = false,
        IEnumerable<Func<Exception, MvcProblemDetails>> mappings = null,
        IEnumerable<string> allowHeaderNames = null)
    {
        options.IncludeExceptionDetails = (ctx, ex) => includeExceptionDetails;  //ctx.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
        options.AllowedHeaderNames.Add("CorrelationId");
        options.AllowedHeaderNames.Add("FlowId");
        options.AllowedHeaderNames.Add("TraceId");

        foreach (var allowHeaderName in allowHeaderNames.SafeNull())
        {
            options.AllowedHeaderNames.Add(allowHeaderName);
        }

        options.Map<FluentValidation.ValidationException>(ex =>
        {
            return new ValidationProblemDetails
            {
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = $"[{nameof(FluentValidation.ValidationException)}] A model validation error has occurred while executing the request",
                Type = "https://httpstatuses.com/400",
                Errors = ex.Errors?
                    .OrderBy(v => v.PropertyName)
                    .GroupBy(v => v.PropertyName.Replace("Entity.", string.Empty, StringComparison.OrdinalIgnoreCase), v => v.ErrorMessage)
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly
                    .ToDictionary(g => g.Key, g => g.ToArray()) ?? []
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly
            };
        });

        options.Map<BusinessRuleNotSatisfiedException>(ex =>
        {
            return new MvcProblemDetails
            {
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://httpstatuses.com/400"
            };
        });

        options.Map<SecurityException>(ex =>
        {
            return new MvcProblemDetails
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://httpstatuses.com/401"
            };
        });

        options.Map<ModuleNotEnabledException>(ex =>
        {
            return new MvcProblemDetails
            {
                Title = "Module Not Enabled",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://httpstatuses.com/503"
            };
        });

        options.Map<AggregateNotFoundException>(ex =>
        {
            return new MvcProblemDetails
            {
                Title = "Aggregate Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://httpstatuses.com/404"
            };
        });

        options.Map<EntityNotFoundException>(ex =>
        {
            return new MvcProblemDetails
            {
                Title = "Entity Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://httpstatuses.com/404"
            };
        });

        foreach (var mapping in mappings.SafeNull())
        {
            options.Map(mapping);
        }

        options.MapToStatusCode<NotImplementedException>(StatusCodes.Status501NotImplemented);
        options.MapToStatusCode<HttpRequestException>(StatusCodes.Status503ServiceUnavailable);
        options.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);
        options.Rethrow<NotSupportedException>();
    }
}