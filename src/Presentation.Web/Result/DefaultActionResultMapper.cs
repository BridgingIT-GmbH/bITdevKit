// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Net;
using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Represents default action result mapper.
/// </summary>
public class DefaultActionResultMapper : IActionResultMapper
{
    /// <summary>
    /// Executes the ok operation.
    /// </summary>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult Ok(IResult result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result);
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the ok operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Ok<TModel>(IResult result, TModel model)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(model);
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the ok operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="action">The action to invoke.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Ok<TModel>(IResult result, Action<TModel> action)
        where TModel : new()
    {
        if (result.IsSuccess)
        {
            var model = new TModel();
            action?.Invoke(model);

            return new OkObjectResult(model);
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the ok operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="models">The models used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<ICollection<TModel>> Ok<TModel>(IResult result, IEnumerable<TModel> models)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(models);
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the ok operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<ResultPaged<TModel>> Ok<TModel>(ResultPaged<TModel> result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result);
        }

        return MapError(result);
    }

    /// <summary>
    /// Creates d.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Created<TModel>(
        IResult result,
        TModel model,
        string routeName = null,
        object routeValues = null)
    {
        if (result.IsSuccess)
        {
            if (!string.IsNullOrWhiteSpace(routeName))
            {
                return new CreatedAtRouteResult(routeName, routeValues, model);
            }

            return new OkObjectResult(model) { StatusCode = 201 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Creates d.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="action">The action to invoke.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Created<TModel>(
        IResult result,
        Action<TModel> action,
        string routeName = null,
        object routeValues = null)
        where TModel : new()
    {
        if (result.IsSuccess)
        {
            var model = new TModel();
            action?.Invoke(model);

            if (!string.IsNullOrWhiteSpace(routeName))
            {
                return new CreatedAtRouteResult(routeName, routeValues, model);
            }

            return new OkObjectResult(model) { StatusCode = 201 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Creates d.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <param name="actionName">The action name used by the operation.</param>
    /// <param name="controllerName">The controller name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Created<TModel>(
        IResult result,
        TModel model,
        string actionName,
        string controllerName,
        object routeValues = null)
    {
        if (result.IsSuccess)
        {
            if (!string.IsNullOrWhiteSpace(actionName) && !string.IsNullOrWhiteSpace(controllerName))
            {
                return new CreatedAtActionResult(actionName, controllerName, routeValues, model);
            }

            return new OkObjectResult(model) { StatusCode = 201 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Creates d.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="action">The action to invoke.</param>
    /// <param name="actionName">The action name used by the operation.</param>
    /// <param name="controllerName">The controller name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Created<TModel>(
        IResult result,
        Action<TModel> action,
        string actionName,
        string controllerName,
        object routeValues = null)
        where TModel : new()
    {
        if (result.IsSuccess)
        {
            var model = new TModel();
            action?.Invoke(model);

            if (!string.IsNullOrWhiteSpace(actionName) && !string.IsNullOrWhiteSpace(controllerName))
            {
                return new CreatedAtActionResult(actionName, controllerName, routeValues, model);
            }

            return new OkObjectResult(model) { StatusCode = 201 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the updated operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Updated<TModel>(
        IResult result,
        TModel model,
        string routeName = null,
        object routeValues = null)
    {
        if (result.IsSuccess)
        {
            if (!string.IsNullOrWhiteSpace(routeName))
            {
                return new UpdatedAtRouteResult(routeName, routeValues, model) { StatusCode = 200 };
            }

            return new OkObjectResult(model) { StatusCode = 200 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the updated operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="action">The action to invoke.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Updated<TModel>(
        IResult result,
        Action<TModel> action,
        string routeName = null,
        object routeValues = null)
        where TModel : new()
    {
        if (result.IsSuccess)
        {
            var model = new TModel();

            if (action is not null)
            {
                action.Invoke(model);

                if (!string.IsNullOrWhiteSpace(routeName))
                {
                    return new UpdatedAtRouteResult(routeName, routeValues, model) { StatusCode = 200 };
                }

                return new OkObjectResult(model) { StatusCode = 200 };
            }

            return new ObjectResult(model) { StatusCode = 200 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the updated operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <param name="actionName">The action name used by the operation.</param>
    /// <param name="controllerName">The controller name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Updated<TModel>(
        IResult result,
        TModel model,
        string actionName,
        string controllerName,
        object routeValues = null)
    {
        if (result.IsSuccess)
        {
            if (!string.IsNullOrWhiteSpace(actionName) && !string.IsNullOrWhiteSpace(controllerName))
            {
                return new UpdatedAtActionResult(actionName, controllerName, routeValues, model)
                {
                    StatusCode = 200
                };
            }

            return new OkObjectResult(model) { StatusCode = 200 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the updated operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="action">The action to invoke.</param>
    /// <param name="actionName">The action name used by the operation.</param>
    /// <param name="controllerName">The controller name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Updated<TModel>(
        IResult result,
        Action<TModel> action,
        string actionName,
        string controllerName,
        object routeValues = null)
        where TModel : new()
    {
        if (result.IsSuccess)
        {
            var model = new TModel();

            if (action is not null)
            {
                action.Invoke(model);

                if (!string.IsNullOrWhiteSpace(actionName) && !string.IsNullOrWhiteSpace(controllerName))
                {
                    return new UpdatedAtActionResult(actionName, controllerName, routeValues, model)
                    {
                        StatusCode = 200
                    };
                }

                return new OkObjectResult(model) { StatusCode = 200 };
            }

            return new ObjectResult(model) { StatusCode = 200 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the accepted operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Accepted<TModel>(
        IResult result,
        TModel model,
        string routeName = null,
        object routeValues = null)
    {
        if (result.IsSuccess)
        {
            if (!string.IsNullOrWhiteSpace(routeName))
            {
                return new AcceptedAtRouteResult(routeName, routeValues, model);
            }

            return new OkObjectResult(model) { StatusCode = 202 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the accepted operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="action">The action to invoke.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Accepted<TModel>(
        IResult result,
        Action<TModel> action,
        string routeName = null,
        object routeValues = null)
        where TModel : new()
    {
        if (result.IsSuccess)
        {
            var model = new TModel();
            action?.Invoke(model);

            if (!string.IsNullOrWhiteSpace(routeName))
            {
                return new AcceptedAtRouteResult(routeName, routeValues, model);
            }

            return new OkObjectResult(model) { StatusCode = 202 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the accepted operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <param name="actionName">The action name used by the operation.</param>
    /// <param name="controllerName">The controller name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Accepted<TModel>(
        IResult result,
        TModel model,
        string actionName,
        string controllerName,
        object routeValues = null)
    {
        if (result.IsSuccess)
        {
            if (!string.IsNullOrWhiteSpace(actionName) && !string.IsNullOrWhiteSpace(controllerName))
            {
                return new AcceptedAtActionResult(actionName, controllerName, routeValues, model);
            }

            return new OkObjectResult(model) { StatusCode = 202 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the accepted operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="action">The action to invoke.</param>
    /// <param name="actionName">The action name used by the operation.</param>
    /// <param name="controllerName">The controller name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Accepted<TModel>(
        IResult result,
        Action<TModel> action,
        string actionName,
        string controllerName,
        object routeValues = null)
        where TModel : new()
    {
        if (result.IsSuccess)
        {
            var model = new TModel();
            action?.Invoke(model);

            if (!string.IsNullOrWhiteSpace(actionName) && !string.IsNullOrWhiteSpace(controllerName))
            {
                return new AcceptedAtActionResult(actionName, controllerName, routeValues, model);
            }

            return new OkObjectResult(model) { StatusCode = 202 };
        }

        return MapError(result);
    }

    /// <summary>
    /// Deletes d.
    /// </summary>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult Deleted(IResult result)
    {
        return this.NoContent(result);
    }

    /// <summary>
    /// Deletes d.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Deleted<TModel>(IResult result)
    {
        return this.NoContent<TModel>(result);
    }

    /// <summary>
    /// Executes the no content operation.
    /// </summary>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult NoContent(IResult result)
    {
        if (result.IsSuccess)
        {
            return new NoContentResult();
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the no content operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> NoContent<TModel>(IResult result)
    {
        if (result.IsSuccess)
        {
            return new NoContentResult();
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the object operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <param name="statusCode">The status code used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<TModel> Object<TModel>(IResult result, TModel model, int statusCode)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(model) { StatusCode = statusCode };
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the object operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="models">The models used by the operation.</param>
    /// <param name="statusCode">The status code used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<ICollection<TModel>> Object<TModel>(
        IResult result,
        IEnumerable<TModel> models,
        int statusCode)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(models) { StatusCode = statusCode };
        }

        return MapError(result);
    }

    /// <summary>
    /// Executes the object operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="statusCode">The status code used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual ActionResult<ResultPaged<TModel>> Object<TModel>(ResultPaged<TModel> result, int statusCode)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(result) { StatusCode = statusCode };
        }

        return MapError(result);
    }

    private static ActionResult MapError(IResult result)
    {
        var errors = result.Errors ?? [];

        var status = errors switch
        {
            _ when errors.Has<ForbiddenError>() => HttpStatusCode.Forbidden,
            _ when errors.Has<UnauthorizedError>() => HttpStatusCode.Unauthorized,
            _ when errors.Has<NotFoundError>() || errors.Has<EntityNotFoundError>() => HttpStatusCode.NotFound,
            _ => (HttpStatusCode?)null
        };
        if (status is { } knownStatus)
        {
            return ToProblemResult(ResultHttpProblemDetails.Create((int)knownStatus, result: result));
        }

        var validationDetails = errors.SelectMany<IResultError, string>(error => error switch
        {
            ValidationError validation => [$"[{validation.GetType().Name}] {validation.Message}"],
            FluentValidationError { Errors.Count: > 0 } fluent => fluent.Errors.Select(
                failure => $"[FluentValidationError] {failure.PropertyName}: {failure.ErrorMessage}"),
            FluentValidationError fluent => [$"[FluentValidationError] {fluent.Message}"],
            _ => []
        }).ToArray();
        var isValidationFailure = validationDetails.Length > 0;
        var problem = ResultHttpProblemDetails.Create(
            (int)(isValidationFailure ? HttpStatusCode.BadRequest : HttpStatusCode.InternalServerError),
            isValidationFailure ? "Bad Request" : "Unhandled Result Error",
            result);
        if (problem is not ResultHttpProblemDetails)
        {
            problem.Detail = string.Join("; ", isValidationFailure
                ? validationDetails
                : errors.Select(error => $"[{error.GetType().Name}] {error.Message}"));
            problem.Extensions["IsSuccess"] = result.IsSuccess;
            problem.Extensions["Messages"] = string.Join(Environment.NewLine, result.Messages.SafeNull());
            problem.Extensions["Errors"] = string.Join(Environment.NewLine, errors.Select(error => error.Message));
        }
        else if (isValidationFailure)
        {
            problem.Detail = string.Join("; ", validationDetails);
        }

        return ToProblemResult(problem);
    }

    private static ObjectResult ToProblemResult(ProblemDetails problem) =>
        new ResultProblemObjectResult(problem) { StatusCode = problem.Status };

    private sealed class ResultProblemObjectResult(ProblemDetails problem) : ObjectResult(problem)
    {
        public override Task ExecuteResultAsync(ActionContext context)
        {
            if (this.Value is not ProblemDetails details)
            {
                return base.ExecuteResultAsync(context);
            }

            var response = (ResultProblemObjectResult)this.MemberwiseClone();
            response.Value = ResultHttpProblemDetails.PrepareResponse(details, context.HttpContext);
            return context.HttpContext.RequestServices.GetRequiredService<IActionResultExecutor<ObjectResult>>()
                .ExecuteAsync(context, response);
        }
    }
}
