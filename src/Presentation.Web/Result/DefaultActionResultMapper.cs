// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Presentation.Web.Errors;
using Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

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
        var failure = result is Result mapped ? mapped : Result.Failure(result.Messages, result.Errors);
        return new ResultFailureActionResult(ResultMapHttpExtensions.MapDefaultFailure(failure), failure);
    }

    private sealed class ResultFailureActionResult(Microsoft.AspNetCore.Http.IResult response, Result failure) : ActionResult
    {
        public override async Task ExecuteResultAsync(ActionContext context)
        {
            var http = context.HttpContext;
            await response.ExecuteAsync(http);
            if (response is not ProblemHttpResult)
            {
                ResponseProblemDetailsService.LogCustomResult(http, failure);
            }
        }
    }
}
