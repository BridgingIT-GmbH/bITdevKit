// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Mvc;

public interface IActionResultMapper
{
    ActionResult Ok(IResult result);

    ActionResult<TModel> Ok<TModel>(IResult result, TModel model);

    ActionResult<TModel> Ok<TModel>(IResult result, Action<TModel> action)
        where TModel : new();

    ActionResult<ICollection<TModel>> Ok<TModel>(IResult result, IEnumerable<TModel> models);

    ActionResult<PagedResult<TModel>> Ok<TModel>(PagedResult<TModel> result);

    ActionResult<TModel> Created<TModel>(IResult result, TModel model, string routeName = null, object routeValues = null);

    ActionResult<TModel> Created<TModel>(IResult result, Action<TModel> action, string routeName = null, object routeValues = null)
        where TModel : new();

    ActionResult<TModel> Created<TModel>(IResult result, TModel model, string actionName, string controllerName, object routeValues = null);

    ActionResult<TModel> Created<TModel>(IResult result, Action<TModel> action, string actionName, string controllerName, object routeValues = null)
        where TModel : new();

    ActionResult<TModel> Updated<TModel>(IResult result, TModel model, string routeName = null, object routeValues = null);

    ActionResult<TModel> Updated<TModel>(IResult result, Action<TModel> action, string routeName = null, object routeValues = null)
        where TModel : new();

    ActionResult<TModel> Updated<TModel>(IResult result, TModel model, string actionName, string controllerName, object routeValues = null);

    ActionResult<TModel> Updated<TModel>(IResult result, Action<TModel> action, string actionName, string controllerName, object routeValues = null)
        where TModel : new();

    ActionResult<TModel> Accepted<TModel>(IResult result, TModel model, string routeName = null, object routeValues = null);

    ActionResult<TModel> Accepted<TModel>(IResult result, Action<TModel> action, string routeName = null, object routeValues = null)
        where TModel : new();

    ActionResult<TModel> Accepted<TModel>(IResult result, TModel model, string actionName, string controllerName, object routeValues = null);

    ActionResult<TModel> Accepted<TModel>(IResult result, Action<TModel> action, string actionName, string controllerName, object routeValues = null)
        where TModel : new();

    ActionResult Deleted(IResult result);

    ActionResult<TModel> Deleted<TModel>(IResult result);

    ActionResult NoContent(IResult result);

    ActionResult<TModel> NoContent<TModel>(IResult result);

    ActionResult<TModel> Object<TModel>(IResult result, TModel model, int statusCode);

    ActionResult<ICollection<TModel>> Object<TModel>(IResult result, IEnumerable<TModel> models, int statusCode);

    ActionResult<PagedResult<TModel>> Object<TModel>(PagedResult<TModel> result, int statusCode);
}
