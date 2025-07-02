// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Common;
using Microsoft.AspNetCore.Mvc;

public static class ResultActionExtensions
{
    public static ActionResult ToOkActionResult(this Result result, IActionResultMapper actionResultMapper = null)
    {
        return actionResultMapper is not null
            ? actionResultMapper.Ok(result)
            : new DefaultActionResultMapper().Ok(result);
    }

    public static ActionResult<TModel> ToOkActionResult<TModel>(
        this Result result,
        IMapper mapper,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<Result, TModel>(result);

        return actionResultMapper is not null
            ? actionResultMapper.Ok(result, model)
            : new DefaultActionResultMapper().Ok(result, model);
    }

    public static ActionResult<TModel> ToOkActionResult<TModel>(
        this Result<TModel> result,
        IActionResultMapper actionResultMapper = null)
    {
        return actionResultMapper is not null
            ? actionResultMapper.Ok(result, result.IsSuccess ? result.Value : default)
            : new DefaultActionResultMapper().Ok(result, result.IsSuccess ? result.Value : default);
    }

    public static ActionResult<TModel> ToOkActionResult<TModel>(
        this Result result,
        TModel model,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Ok(result, model)
            : new DefaultActionResultMapper().Ok(result, model);
    }

    public static ActionResult<TModel> ToOkActionResult<TSource, TModel>(
        this Result<TSource> result,
        IMapper mapper,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.IsSuccess ? result.Value : default);

        return actionResultMapper is not null
            ? actionResultMapper.Ok(result, model)
            : new DefaultActionResultMapper().Ok(result, model);
    }

    public static ActionResult<ICollection<TModel>> ToOkActionResult<TModel>(
        this Result<IEnumerable<TModel>> result,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Ok(result, result.IsSuccess ? result.Value : default)
            : new DefaultActionResultMapper().Ok(result, result.IsSuccess ? result.Value : default);
    }

    public static ActionResult<ICollection<TModel>> ToOkActionResult<TSource, TModel>(
        this Result<IEnumerable<TSource>> result,
        IMapper mapper,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var models = result.IsSuccess ? result.Value.Select(mapper.Map<TSource, TModel>) : default;

        return actionResultMapper is not null
            ? actionResultMapper.Ok(result, models)
            : new DefaultActionResultMapper().Ok(result, models);
    }

    public static ActionResult<ResultPaged<TModel>> ToOkActionResult<TModel>(
        this ResultPaged<TModel> result,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Ok(result)
            : new DefaultActionResultMapper().Ok(result);
    }

    public static ActionResult<ResultPaged<TModel>> ToOkActionResult<TSource, TModel>(
        this ResultPaged<TSource> result,
        IMapper mapper,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var resultPaged = result.IsSuccess
            ? ResultPaged<TModel>.Success(result.Value.Select(mapper.Map<TSource, TModel>), result.CurrentPage, result.PageSize)
                .WithMessages(result.Messages)
            : ResultPaged<TModel>.Failure()
                .WithMessages(result.Messages);

        foreach (var error in result.Errors.SafeNull())
        {
            resultPaged.WithError(error);
        }

        return actionResultMapper is not null
            ? actionResultMapper.Ok(resultPaged)
            : new DefaultActionResultMapper().Ok(resultPaged);
    }

    public static ActionResult<TModel> ToOkActionResult<TModel>(
        this Result result,
        Action<TModel> action,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Ok(result, action)
            : new DefaultActionResultMapper().Ok(result, action);
    }

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(
        this Result<TModel> result,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Created(result, result.IsSuccess ? result.Value : default, routeName, routeValues)
            : new DefaultActionResultMapper().Created(result, result.IsSuccess ? result.Value : default, routeName, routeValues);
    }

    public static ActionResult<TModel> ToCreatedActionResult<TSource, TModel>(
        this Result<TSource> result,
        IMapper mapper,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.IsSuccess ? result.Value : default);

        return actionResultMapper is not null
            ? actionResultMapper.Created(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Created(result, model, routeName, routeValues);
    }

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(
        this Result result,
        TModel model,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Created(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Created(result, model, routeName, routeValues);
    }

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(
        this Result result,
        Action<TModel> action,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Created(result, action, routeName, routeValues)
            : new DefaultActionResultMapper().Created(result, action, routeName, routeValues);
    }

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(
        this Result<TModel> result,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Created(result, result.IsSuccess ? result.Value : default, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Created(result, result.IsSuccess ? result.Value : default, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToCreatedActionResult<TSource, TModel>(
        this Result<TSource> result,
        IMapper mapper,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.IsSuccess ? result.Value : default);

        return actionResultMapper is not null
            ? actionResultMapper.Created(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Created(result, model, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(
        this Result result,
        TModel model,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Created(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Created(result, model, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToCreatedActionResult<TModel>(
        this Result result,
        Action<TModel> action,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Created(result, action, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Created(result, action, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(
        this Result<TModel> result,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Updated(result, result.IsSuccess ? result.Value : default, routeName, routeValues)
            : new DefaultActionResultMapper().Updated(result, result.IsSuccess ? result.Value : default, routeName, routeValues);
    }

    public static ActionResult<TModel> ToUpdatedActionResult<TSource, TModel>(
        this Result<TSource> result,
        IMapper mapper,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.IsSuccess ? result.Value : default);

        return actionResultMapper is not null
            ? actionResultMapper.Updated(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Updated(result, model, routeName, routeValues);
    }

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(
        this Result result,
        TModel model,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Updated(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Updated(result, model, routeName, routeValues);
    }

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(
        this Result result,
        Action<TModel> action,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Updated(result, action, routeName, routeValues)
            : new DefaultActionResultMapper().Updated(result, action, routeName, routeValues);
    }

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(
        this Result<TModel> result,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Updated(result, result.IsSuccess ? result.Value : default, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Updated(result, result.IsSuccess ? result.Value : default, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToUpdatedActionResult<TSource, TModel>(
        this Result<TSource> result,
        IMapper mapper,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.IsSuccess ? result.Value : default);

        return actionResultMapper is not null
            ? actionResultMapper.Updated(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Updated(result, model, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(
        this Result result,
        TModel model,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Updated(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Updated(result, model, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToUpdatedActionResult<TModel>(
        this Result result,
        Action<TModel> action,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Updated(result, action, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Updated(result, action, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(
        this Result<TModel> result,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Accepted(result, result.IsSuccess ? result.Value : default, routeName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, result.IsSuccess ? result.Value : default, routeName, routeValues);
    }

    public static ActionResult<TModel> ToAcceptedActionResult<TSource, TModel>(
        this Result<TSource> result,
        IMapper mapper,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.IsSuccess ? result.Value : default);

        return actionResultMapper is not null
            ? actionResultMapper.Accepted(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, model, routeName, routeValues);
    }

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(
        this Result result,
        TModel model,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Accepted(result, model, routeName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, model, routeName, routeValues);
    }

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(
        this Result result,
        Action<TModel> action,
        string routeName = null,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Accepted(result, action, routeName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, action, routeName, routeValues);
    }

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(
        this Result<TModel> result,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Accepted(result, result.IsSuccess ? result.Value : default, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, result.IsSuccess ? result.Value : default, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToAcceptedActionResult<TSource, TModel>(
        this Result<TSource> result,
        IMapper mapper,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.IsSuccess ? result.Value : default);

        return actionResultMapper is not null
            ? actionResultMapper.Accepted(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, model, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(
        this Result result,
        TModel model,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Accepted(result, model, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, model, actionName, controllerName, routeValues);
    }

    public static ActionResult<TModel> ToAcceptedActionResult<TModel>(
        this Result result,
        Action<TModel> action,
        string actionName,
        string controllerName,
        object routeValues = null,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Accepted(result, action, actionName, controllerName, routeValues)
            : new DefaultActionResultMapper().Accepted(result, action, actionName, controllerName, routeValues);
    }

    public static ActionResult ToDeletedActionResult(this Result result, IActionResultMapper actionResultMapper = null)
    {
        return actionResultMapper is not null
            ? actionResultMapper.Deleted(result)
            : new DefaultActionResultMapper().Deleted(result);
    }

    public static ActionResult<TModel> ToDeletedActionResult<TModel>(
        this Result result,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Deleted<TModel>(result)
            : new DefaultActionResultMapper().Deleted<TModel>(result);
    }

    public static ActionResult<TModel> ToNoContentActionResult<TModel>(
        this Result result,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.NoContent<TModel>(result)
            : new DefaultActionResultMapper().NoContent<TModel>(result);
    }

    public static ActionResult<TModel> ToObjectActionResult<TModel>(
        this Result<TModel> result,
        int statusCode,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Object(result, result.IsSuccess ? result.Value : default, statusCode)
            : new DefaultActionResultMapper().Object(result, result.IsSuccess ? result.Value : default, statusCode);
    }

    public static ActionResult<TModel> ToObjectActionResult<TModel>(
        this Result result,
        TModel model,
        int statusCode,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Object(result, model, statusCode)
            : new DefaultActionResultMapper().Object(result, model, statusCode);
    }

    public static ActionResult<TModel> ToObjectActionResult<TSource, TModel>(
        this Result<TSource> result,
        IMapper mapper,
        int statusCode,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var model = mapper.Map<TSource, TModel>(result.IsSuccess ? result.Value : default);

        return actionResultMapper is not null
            ? actionResultMapper.Object(result, model, statusCode)
            : new DefaultActionResultMapper().Object(result, model, statusCode);
    }

    public static ActionResult<ICollection<TModel>> ToObjectActionResult<TModel>(
        this Result<IEnumerable<TModel>> result,
        int statusCode,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Object(result, result.IsSuccess ? result.Value : default, statusCode)
            : new DefaultActionResultMapper().Object(result, result.IsSuccess ? result.Value : default, statusCode);
    }

    public static ActionResult<ICollection<TModel>> ToObjectActionResult<TModel, TSource>(
        this Result<IEnumerable<TSource>> result,
        IMapper mapper,
        int statusCode,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var models = result.IsSuccess ? result.Value.Select(mapper.Map<TSource, TModel>) : default;

        return actionResultMapper is not null
            ? actionResultMapper.Object(result, models, statusCode)
            : new DefaultActionResultMapper().Object(result, models, statusCode);
    }

    public static ActionResult<ICollection<TModel>> ToObjectActionResult<TModel>(
        this ResultPaged<TModel> result,
        int statusCode,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        return actionResultMapper is not null
            ? actionResultMapper.Object(result, result.IsSuccess ? result.Value : default, statusCode)
            : new DefaultActionResultMapper().Object(result, result.IsSuccess ? result.Value : default, statusCode);
    }

    public static ActionResult<ResultPaged<TModel>> ToObjectActionResult<TModel, TSource>(
        this ResultPaged<TSource> result,
        IMapper mapper,
        int statusCode,
        IActionResultMapper actionResultMapper = null)
        where TModel : class, new()
    {
        var resultPaged = result.IsSuccess
            ? ResultPaged<TModel>.Success(result.Value.Select(mapper.Map<TSource, TModel>), result.CurrentPage, result.PageSize)
                .WithMessages(result.Messages)
            : ResultPaged<TModel>.Failure()
                .WithMessages(result.Messages);

        // var resultPaged =
        //     new ResultPaged<TModel>(models, result.Messages, result.TotalCount, result.CurrentPage, result.PageSize)
        //     {
        //         IsSuccess = result.IsSuccess
        //     };

        foreach (var error in result.Errors.SafeNull())
        {
            resultPaged.WithError(error);
        }

        return actionResultMapper is not null
            ? actionResultMapper.Object(resultPaged, statusCode)
            : new DefaultActionResultMapper().Object(resultPaged, statusCode);
    }
}