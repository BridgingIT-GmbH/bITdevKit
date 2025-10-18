// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.Security;
using ProblemDetailsOptions = Hellang.Middleware.ProblemDetails.ProblemDetailsOptions;

//public static class ServiceCollectionExtensions
//{
//    public static IServiceCollection AddProblemDetails(this IServiceCollection source)
//    {
//        return Hellang.Middleware.ProblemDetails.ProblemDetailsExtensions
//            .AddProblemDetails(source, Configure.ProblemDetails);
//    }
//}

public static class Configure
{
    public static void ProblemDetails(ProblemDetailsOptions options)
    {
        ProblemDetails(options, false);
    }

    public static void ProblemDetails(
        ProblemDetailsOptions options,
        bool includeExceptionDetails = false,
        IEnumerable<Func<Exception, ProblemDetails>> mappings = null,
        IEnumerable<string> allowedHeaderNames = null)
    {
        //options.IncludeExceptionDetails = (ctx, ex) => includeExceptionDetails;  //ctx.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
        options.IncludeExceptionDetails = (ctx, ex) =>
        {
            var env = ctx.RequestServices.GetRequiredService<IHostEnvironment>();

            return includeExceptionDetails || env.IsDevelopment() || env.IsStaging();
        };

        //options.ShouldLogUnhandledException = (ctx, e, d) => d.Status >= 500;
        options.AllowedHeaderNames.Add("CorrelationId");
        options.AllowedHeaderNames.Add("FlowId");
        options.AllowedHeaderNames.Add("TraceId");

        foreach (var allowHeaderName in allowedHeaderNames.SafeNull())
        {
            options.AllowedHeaderNames.Add(allowHeaderName);
        }

        options.Map<ValidationException>(ex =>
        {
            var errors = ex.Errors?.OrderBy(v => v.PropertyName)
                .GroupBy(v => v.PropertyName.Replace("Entity.", string.Empty, StringComparison.OrdinalIgnoreCase), v => v.ErrorMessage)
                .ToDictionary(g => g.Key, g => g.ToArray()) ?? new Dictionary<string, string[]>(StringComparer.Ordinal);

            return new ProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = "[ValidationException] A validation error has occurred",
                Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/400",
                Extensions = new Dictionary<string, object> { ["data"] = new { errors } }
            };
        });

        options.Map<RuleException>(ex =>
        {
            return new ProblemDetails
            {
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/400",
                Extensions = new Dictionary<string, object>() { ["data"] = new { } }
            };
        });

        options.Map<DomainPolicyException>(ex =>
        {
            return new ProblemDetails
            {
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/400",
                Extensions = new Dictionary<string, object>() { ["data"] = new { } }
            };
        });

        options.Map<SecurityException>(ex =>
        {
            return new ProblemDetails
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/401",
                Extensions = new Dictionary<string, object>() { ["data"] = new { } }
            };
        });

        options.Map<ModuleNotEnabledException>(ex =>
        {
            return new ProblemDetails
            {
                Title = "Module Not Enabled",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/503",
                Extensions = new Dictionary<string, object>() { ["data"] = new { } }
            };
        });

        options.Map<AggregateNotFoundException>(ex =>
        {
            return new ProblemDetails
            {
                Title = "Aggregate Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/404",
                Extensions = new Dictionary<string, object>() { ["data"] = new { } }
            };
        });

        options.Map<EntityNotFoundException>(ex =>
        {
            return new ProblemDetails
            {
                Title = "Entity Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/404",
                Extensions = new Dictionary<string, object>() { ["data"] = new { } }
            };
        });

        options.Map<Exception>(ex =>
        {
            object data = null;
            if (includeExceptionDetails)
            {
                data = new
                {
                    errors = new[]
                    {
                        new
                        {
                            type = ex.GetType().FullName,
                            message = ex.GetFullMessage(),
                            stackTrace = ex.StackTrace
                        }
                    }
                };
            }
            else
            {
                data = new { };
            }

            return new ProblemDetails
            {
                Title = "Internal Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = $"[{ex.GetType().Name}] {ex.Message}",
                Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/500",
                Extensions = new Dictionary<string, object>()
                {
                    ["data"] = data
                }
            };
        });

        foreach (var mapping in mappings.SafeNull())
        {
            options.Map(mapping);
        }

        options.MapToStatusCode<NotImplementedException>(StatusCodes.Status501NotImplemented);
        options.MapToStatusCode<HttpRequestException>(StatusCodes.Status503ServiceUnavailable);
        //options.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);
        options.Rethrow<NotSupportedException>();
    }
}