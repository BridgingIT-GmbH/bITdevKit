// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Mvc;

public static class ResultExtensions
{
    public static ActionResult ToOkActionResult(this IResult result, IActionResultMapper actionResultMapper = null) =>
        actionResultMapper is not null
            ? actionResultMapper.Ok(result)
            : new DefaultActionResultMapper().Ok(result);

    public static ActionResult<TModel> ToOkActionResult<TModel>(this IResult result, IMapper mapper, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<IResult, TModel>(result);

        return actionResultMapper is not null
            ? actionResultMapper.Ok(result, model)
            : new DefaultActionResultMapper().Ok(result, model);
    }

    public static ActionResult<TModel> ToOkActionResult<TModel>(this IResult<TModel> result, IActionResultMapper actionResultMapper = null) =>
        actionResultMapper is not null
            ? actionResultMapper.Ok(result, result.Value)
            : new DefaultActionResultMapper().Ok(result, result.Value);

    public static ActionResult<TModel> ToOkActionResult<TModel>(this IResult result, TModel model, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Ok(result, model)
            : new DefaultActionResultMapper().Ok(result, model);

    public static ActionResult<TModel> ToOkActionResult<TSource, TModel>(this IResult<TSource> result, IMapper mapper, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.Value);

        return actionResultMapper is not null
            ? actionResultMapper.Ok(result, model)
            : new DefaultActionResultMapper().Ok(result, model);
    }

    public static ActionResult<ICollection<TModel>> ToOkActionResult<TModel>(this IResult<IEnumerable<TModel>> result, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Ok(result, result.Value)
            : new DefaultActionResultMapper().Ok(result, result.Value);

    public static ActionResult<ICollection<TModel>> ToOkActionResult<TSource, TModel>(this IResult<IEnumerable<TSource>> result, IMapper mapper, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var models = result.Value.Select(mapper.Map<TSource, TModel>);

        return actionResultMapper is not null
            ? actionResultMapper.Ok(result, models)
            : new DefaultActionResultMapper().Ok(result, models);
    }

    public static ActionResult<PagedResult<TModel>> ToOkActionResult<TModel>(this PagedResult<TModel> result, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Ok(result)
            : new DefaultActionResultMapper().Ok(result);

    public static ActionResult<PagedResult<TModel>> ToOkActionResult<TSource, TModel>(this PagedResult<TSource> result, IMapper mapper, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var models = result.Value.Select(mapper.Map<TSource, TModel>);
        var pagedResult = new PagedResult<TModel>(models, result.Messages, result.TotalCount, result.CurrentPage, result.PageSize) { IsSuccess = result.IsSuccess };

        foreach (var error in result.Errors.SafeNull())
        {
            pagedResult.WithError(error);
        }

        return actionResultMapper is not null
            ? actionResultMapper.Ok(pagedResult)
            : new DefaultActionResultMapper().Ok(pagedResult);
    }

    public static ActionResult<TModel> ToOkActionResult<TModel>(this IResult result, Action<TModel> action, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Ok(result, action)
            : new DefaultActionResultMapper().Ok(result, action);

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(this IResult<TModel> result, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Created(result, result.Value, routeName, routeValues)
            : new DefaultActionResultMapper().Created(result, result.Value, routeName, routeValues);

    public static ActionResult<TModel> ToCreatedActionResult<TSource, TModel>(this IResult<TSource> result, IMapper mapper, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.Value);

        return actionResultMapper is not null
            ? actionResultMapper.Created(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Created(result, model, routeName, routeValues);
    }

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(this IResult result, TModel model, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Created(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Created(result, model, routeName, routeValues);

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(this IResult result, Action<TModel> action, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Created(result, action, routeName, routeValues)
            : new DefaultActionResultMapper().Created(result, action, routeName, routeValues);

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(this IResult<TModel> result, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Created(result, result.Value, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Created(result, result.Value, actionName, controllerName, routeValues);

    public static ActionResult<TModel> ToCreatedActionResult<TSource, TModel>(this IResult<TSource> result, IMapper mapper, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.Value);

        return actionResultMapper is not null
            ? actionResultMapper.Created(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Created(result, model, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(this IResult result, TModel model, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Created(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Created(result, model, actionName, controllerName, routeValues);

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(this IResult result, Action<TModel> action, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Created(result, action, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Created(result, action, actionName, controllerName, routeValues);

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(this IResult<TModel> result, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Updated(result, result.Value, routeName, routeValues)
            : new DefaultActionResultMapper().Updated(result, result.Value, routeName, routeValues);

    public static ActionResult<TModel> ToUpdatedActionResult<TSource, TModel>(this IResult<TSource> result, IMapper mapper, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.Value);

        return actionResultMapper is not null
            ? actionResultMapper.Updated(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Updated(result, model, routeName, routeValues);
    }

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(this IResult result, TModel model, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Updated(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Updated(result, model, routeName, routeValues);

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(this IResult result, Action<TModel> action, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Updated(result, action, routeName, routeValues)
            : new DefaultActionResultMapper().Updated(result, action, routeName, routeValues);

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(this IResult<TModel> result, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Updated(result, result.Value, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Updated(result, result.Value, actionName, controllerName, routeValues);

    public static ActionResult<TModel> ToUpdatedActionResult<TSource, TModel>(this IResult<TSource> result, IMapper mapper, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.Value);

        return actionResultMapper is not null
            ? actionResultMapper.Updated(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Updated(result, model, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(this IResult result, TModel model, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Updated(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Updated(result, model, actionName, controllerName, routeValues);

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(this IResult result, Action<TModel> action, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Updated(result, action, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Updated(result, action, actionName, controllerName, routeValues);

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(this IResult<TModel> result, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Accepted(result, result.Value, routeName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, result.Value, routeName, routeValues);

    public static ActionResult<TModel> ToAcceptedActionResult<TSource, TModel>(this IResult<TSource> result, IMapper mapper, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.Value);

        return actionResultMapper is not null
            ? actionResultMapper.Accepted(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, model, routeName, routeValues);
    }

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(this IResult result, TModel model, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Accepted(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, model, routeName, routeValues);

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(this IResult result, Action<TModel> action, string routeName = null, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Accepted(result, action, routeName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, action, routeName, routeValues);

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(this IResult<TModel> result, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Accepted(result, result.Value, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, result.Value, actionName, controllerName, routeValues);

    public static ActionResult<TModel> ToAcceptedActionResult<TSource, TModel>(this IResult<TSource> result, IMapper mapper, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.Value);

        return actionResultMapper is not null
            ? actionResultMapper.Accepted(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, model, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(this IResult result, TModel model, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Accepted(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, model, actionName, controllerName, routeValues);

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(this IResult result, Action<TModel> action, string actionName, string controllerName, object routeValues = null, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Accepted(result, action, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, action, actionName, controllerName, routeValues);

    public static ActionResult ToDeletedActionResult(this IResult result, IActionResultMapper actionResultMapper = null) =>
        actionResultMapper is not null
            ? actionResultMapper.Deleted(result)
            : new DefaultActionResultMapper().Deleted(result);

    public static ActionResult<TModel> ToDeletedActionResult<TModel>(this IResult result, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Deleted<TModel>(result)
            : new DefaultActionResultMapper().Deleted<TModel>(result);

    public static ActionResult<TModel> ToNoContentActionResult<TModel>(this IResult result, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.NoContent<TModel>(result)
            : new DefaultActionResultMapper().NoContent<TModel>(result);

    public static ActionResult<TModel> ToObjectActionResult<TModel>(this IResult<TModel> result, int statusCode, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Object(result, result.Value, statusCode)
            : new DefaultActionResultMapper().Object(result, result.Value, statusCode);

    public static ActionResult<TModel> ToObjectActionResult<TModel>(this IResult result, TModel model, int statusCode, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Object(result, model, statusCode)
            : new DefaultActionResultMapper().Object(result, model, statusCode);

    public static ActionResult<TModel> ToObjectActionResult<TSource, TModel>(this IResult<TSource> result, IMapper mapper, int statusCode, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.Value);

        return actionResultMapper is not null
            ? actionResultMapper.Object(result, model, statusCode)
            : new DefaultActionResultMapper().Object(result, model, statusCode);
    }

    public static ActionResult<ICollection<TModel>> ToObjectActionResult<TModel>(this IResult<IEnumerable<TModel>> result, int statusCode, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Object(result, result.Value, statusCode)
            : new DefaultActionResultMapper().Object(result, result.Value, statusCode);

    public static ActionResult<ICollection<TModel>> ToObjectActionResult<TModel, TSource>(this IResult<IEnumerable<TSource>> result, IMapper mapper, int statusCode, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var models = result.Value.Select(mapper.Map<TSource, TModel>);

        return actionResultMapper is not null
            ? actionResultMapper.Object(result, models, statusCode)
            : new DefaultActionResultMapper().Object(result, models, statusCode);
    }

    public static ActionResult<ICollection<TModel>> ToObjectActionResult<TModel>(this PagedResult<TModel> result, int statusCode, IActionResultMapper actionResultMapper = null)
        where TModel : class, new() =>
        actionResultMapper is not null
            ? actionResultMapper.Object(result, result.Value, statusCode)
            : new DefaultActionResultMapper().Object(result, result.Value, statusCode);

    public static ActionResult<PagedResult<TModel>> ToObjectActionResult<TModel, TSource>(this PagedResult<TSource> result, IMapper mapper, int statusCode, IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var models = result.Value.Select(mapper.Map<TSource, TModel>);
        var pagedResult = new PagedResult<TModel>(models, result.Messages, result.TotalCount, result.CurrentPage, result.PageSize) { IsSuccess = result.IsSuccess };

        foreach (var error in result.Errors.SafeNull())
        {
            pagedResult.WithError(error);
        }

        return actionResultMapper is not null
            ? actionResultMapper.Object(pagedResult, statusCode)
            : new DefaultActionResultMapper().Object(pagedResult, statusCode);
    }
}