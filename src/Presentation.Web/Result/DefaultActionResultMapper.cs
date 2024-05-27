// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Net;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Mvc;

public class DefaultActionResultMapper : IActionResultMapper
{
    public virtual ActionResult Ok(IResult result)
    {
        if (!result.HasError())
        {
            if (result.IsSuccess)
            {
                return new OkObjectResult(result);
            }
            else
            {
                return new ObjectResult(result)
                {
                    StatusCode = 200
                };
            }
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Ok<TModel>(IResult result, TModel model)
    {
        if (!result.HasError())
        {
            if (result.IsSuccess)
            {
                return new OkObjectResult(model);
            }
            else
            {
                return new ObjectResult(model)
                {
                    StatusCode = 200
                };
            }
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Ok<TModel>(IResult result, Action<TModel> action)
        where TModel : new()
    {
        if (!result.HasError())
        {
            var model = new TModel();
            action?.Invoke(model);

            if (result.IsSuccess)
            {
                return new OkObjectResult(model);
            }
            else
            {
                return new ObjectResult(model)
                {
                    StatusCode = 200
                };
            }
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<ICollection<TModel>> Ok<TModel>(IResult result, IEnumerable<TModel> models)
    {
        if (!result.HasError())
        {
            if (result.IsSuccess)
            {
                return new OkObjectResult(models);
            }
            else
            {
                return new ObjectResult(models)
                {
                    StatusCode = 200
                };
            }
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<PagedResult<TModel>> Ok<TModel>(PagedResult<TModel> result)
    {
        if (!result.HasError())
        {
            if (result.IsSuccess)
            {
                return new OkObjectResult(result);
            }
            else
            {
                return new ObjectResult(result)
                {
                    StatusCode = 200
                };
            }
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Created<TModel>(IResult result, TModel model, string routeName = null, object routeValues = null)
    {
        if (!result.HasError())
        {
            if (result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(routeName))
                {
                    return new CreatedAtRouteResult(routeName, routeValues, model);
                }
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 201
                    };
                }
            }

            return new ObjectResult(model)
            {
                StatusCode = 200
            };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Created<TModel>(IResult result, Action<TModel> action, string routeName = null, object routeValues = null)
        where TModel : new()
    {
        if (!result.HasError())
        {
            var model = new TModel();
            action?.Invoke(model);

            if (result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(routeName))
                {
                    return new CreatedAtRouteResult(routeName, routeValues, model);
                }
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 201
                    };
                }
            }

            return new ObjectResult(model)
            {
                StatusCode = 200
            };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Created<TModel>(IResult result, TModel model, string actionName, string controllerName, object routeValues = null)
    {
        if (!result.HasError())
        {
            if (result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(actionName) && !string.IsNullOrWhiteSpace(controllerName))
                {
                    return new CreatedAtActionResult(actionName, controllerName, routeValues, model);
                }
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 201
                    };
                }
            }

            return new ObjectResult(model)
            {
                StatusCode = 200
            };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Created<TModel>(IResult result, Action<TModel> action, string actionName, string controllerName, object routeValues = null)
        where TModel : new()
    {
        if (!result.HasError())
        {
            var model = new TModel();
            action?.Invoke(model);

            if (result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(actionName) && !string.IsNullOrWhiteSpace(controllerName))
                {
                    return new CreatedAtActionResult(actionName, controllerName, routeValues, model);
                }
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 201
                    };
                }
            }

            return new ObjectResult(model)
            {
                StatusCode = 200
            };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Updated<TModel>(IResult result, TModel model, string routeName = null, object routeValues = null)
    {
        if (!result.HasError())
        {
            if (result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(routeName))
                {
                    return new UpdatedAtRouteResult(routeName, routeValues, model)
                    {
                        StatusCode = 200
                    };
                }
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 200
                    };
                }
            }

            return new ObjectResult(model)
            {
                StatusCode = 200
            };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Updated<TModel>(IResult result, Action<TModel> action, string routeName = null, object routeValues = null)
        where TModel : new()
    {
        if (!result.HasError())
        {
            var model = new TModel();

            if (result.IsSuccess && action is not null)
            {
                action.Invoke(model);

                if (!string.IsNullOrWhiteSpace(routeName))
                {
                    return new UpdatedAtRouteResult(routeName, routeValues, model)
                    {
                        StatusCode = 200
                    };
                }
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 200
                    };
                }
            }

            return new ObjectResult(model)
            {
                StatusCode = 200
            };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Updated<TModel>(IResult result, TModel model, string actionName, string controllerName, object routeValues = null)
    {
        if (!result.HasError())
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
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 200
                    };
                }
            }

            return new ObjectResult(model)
            {
                StatusCode = 200
            };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Updated<TModel>(IResult result, Action<TModel> action, string actionName, string controllerName, object routeValues = null)
        where TModel : new()
    {
        if (!result.HasError())
        {
            var model = new TModel();

            if (result.IsSuccess && action is not null)
            {
                action.Invoke(model);

                if (!string.IsNullOrWhiteSpace(actionName) && !string.IsNullOrWhiteSpace(controllerName))
                {
                    return new UpdatedAtActionResult(actionName, controllerName, routeValues, model)
                    {
                        StatusCode = 200
                    };
                }
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 200
                    };
                }
            }

            return new ObjectResult(model)
            {
                StatusCode = 200
            };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Accepted<TModel>(IResult result, TModel model, string routeName = null, object routeValues = null)
    {
        if (!result.HasError())
        {
            if (result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(routeName))
                {
                    return new AcceptedAtRouteResult(routeName, routeValues, model);
                }
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 202
                    };
                }
            }
            else
            {
                return new ObjectResult(model)
                {
                    StatusCode = 200
                };
            }
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Accepted<TModel>(IResult result, Action<TModel> action, string routeName = null, object routeValues = null)
        where TModel : new()
    {
        if (!result.HasError())
        {
            var model = new TModel();
            action?.Invoke(model);

            if (result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(routeName))
                {
                    return new AcceptedAtRouteResult(routeName, routeValues, model);
                }
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 202
                    };
                }
            }

            return new ObjectResult(model)
            {
                StatusCode = 200
            };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Accepted<TModel>(IResult result, TModel model, string actionName, string controllerName, object routeValues = null)
    {
        if (!result.HasError())
        {
            if (result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(actionName) && !string.IsNullOrWhiteSpace(controllerName))
                {
                    return new AcceptedAtActionResult(actionName, controllerName, routeValues, model);
                }
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 202
                    };
                }
            }
            else
            {
                return new ObjectResult(model)
                {
                    StatusCode = 200
                };
            }
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Accepted<TModel>(IResult result, Action<TModel> action, string actionName, string controllerName, object routeValues = null)
        where TModel : new()
    {
        if (!result.HasError())
        {
            var model = new TModel();
            action?.Invoke(model);

            if (result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(actionName) && !string.IsNullOrWhiteSpace(controllerName))
                {
                    return new AcceptedAtActionResult(actionName, controllerName, routeValues, model);
                }
                else
                {
                    return new OkObjectResult(model)
                    {
                        StatusCode = 202
                    };
                }
            }

            return new ObjectResult(model)
            {
                StatusCode = 200
            };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult Deleted(IResult result)
    {
        return this.NoContent(result);
    }

    public virtual ActionResult<TModel> Deleted<TModel>(IResult result)
    {
        return this.NoContent<TModel>(result);
    }

    public virtual ActionResult NoContent(IResult result)
    {
        if (!result.HasError())
        {
            return new NoContentResult();
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> NoContent<TModel>(IResult result)
    {
        if (!result.HasError())
        {
            return new NoContentResult();
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<TModel> Object<TModel>(IResult result, TModel model, int statusCode)
    {
        if (!result.HasError())
        {
            return new ObjectResult(model) { StatusCode = statusCode };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<ICollection<TModel>> Object<TModel>(IResult result, IEnumerable<TModel> models, int statusCode)
    {
        if (!result.HasError())
        {
            return new ObjectResult(models) { StatusCode = statusCode };
        }
        else
        {
            return MapError(result);
        }
    }

    public virtual ActionResult<PagedResult<TModel>> Object<TModel>(PagedResult<TModel> result, int statusCode)
    {
        if (!result.HasError())
        {
            return new ObjectResult(result) { StatusCode = statusCode };
        }
        else
        {
            return MapError(result);
        }
    }

    private static ActionResult MapError(IResult result)
    {
        if (result.HasError<NotFoundResultError>(out var errors))
        {
            return new NotFoundResult();
        }
        else if (result.HasError<EntityNotFoundResultError>(out errors))
        {
            return new NotFoundResult();
        }
        else if (result.HasError<ValidationResultError>(out errors))
        {
            // TODO: not yet handled
            // throw new FluentValidationException(errors) > handled by ProblemsDetails middleware
            // return this.ValidationProblem(...)
        }
        else if (result.HasError<BusinessRuleResultError>(out errors))
        {
            // TODO: not yet handled
            // throw new BusinessRuleNotSatisfiedException(error) > handled by ProblemsDetails middleware
        }

        return new ObjectResult(new ProblemDetails
        {
            Title = "Unhandled Result Error",
            Status = (int)HttpStatusCode.InternalServerError,
            Detail = string.Join(Environment.NewLine, result.Errors.Select(e => $"[{e.GetType().Name}] {e.Message}")),
            Extensions =
            {
                ["IsSuccess"] = result.IsSuccess,
                ["Messages"] = string.Join(Environment.NewLine, result.Messages)
            },
            Type = "https://httpstatuses.com/500"
        })
        { StatusCode = 500 }; // unhandled
    }
}