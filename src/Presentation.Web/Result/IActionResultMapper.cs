// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Common;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Defines operations for i action result mapper.
/// </summary>
public interface IActionResultMapper
{
    /// <summary>
    /// Executes the ok operation.
    /// </summary>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult Ok(IResult result);

    /// <summary>
    /// Executes the ok operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<TModel> Ok<TModel>(IResult result, TModel model);

    /// <summary>
    /// Executes the ok operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="action">The action to invoke.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<TModel> Ok<TModel>(IResult result, Action<TModel> action)
        where TModel : new();

    /// <summary>
    /// Executes the ok operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="models">The models used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<ICollection<TModel>> Ok<TModel>(IResult result, IEnumerable<TModel> models);

    /// <summary>
    /// Executes the ok operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<ResultPaged<TModel>> Ok<TModel>(ResultPaged<TModel> result);

    /// <summary>
    /// Creates d.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<TModel> Created<TModel>(
        IResult result,
        TModel model,
        string routeName = null,
        object routeValues = null);

    /// <summary>
    /// Creates d.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="action">The action to invoke.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<TModel> Created<TModel>(
        IResult result,
        Action<TModel> action,
        string routeName = null,
        object routeValues = null)
        where TModel : new();

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
    ActionResult<TModel> Created<TModel>(
        IResult result,
        TModel model,
        string actionName,
        string controllerName,
        object routeValues = null);

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
    ActionResult<TModel> Created<TModel>(
        IResult result,
        Action<TModel> action,
        string actionName,
        string controllerName,
        object routeValues = null)
        where TModel : new();

    /// <summary>
    /// Executes the updated operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<TModel> Updated<TModel>(
        IResult result,
        TModel model,
        string routeName = null,
        object routeValues = null);

    /// <summary>
    /// Executes the updated operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="action">The action to invoke.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<TModel> Updated<TModel>(
        IResult result,
        Action<TModel> action,
        string routeName = null,
        object routeValues = null)
        where TModel : new();

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
    ActionResult<TModel> Updated<TModel>(
        IResult result,
        TModel model,
        string actionName,
        string controllerName,
        object routeValues = null);

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
    ActionResult<TModel> Updated<TModel>(
        IResult result,
        Action<TModel> action,
        string actionName,
        string controllerName,
        object routeValues = null)
        where TModel : new();

    /// <summary>
    /// Executes the accepted operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<TModel> Accepted<TModel>(
        IResult result,
        TModel model,
        string routeName = null,
        object routeValues = null);

    /// <summary>
    /// Executes the accepted operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="action">The action to invoke.</param>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<TModel> Accepted<TModel>(
        IResult result,
        Action<TModel> action,
        string routeName = null,
        object routeValues = null)
        where TModel : new();

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
    ActionResult<TModel> Accepted<TModel>(
        IResult result,
        TModel model,
        string actionName,
        string controllerName,
        object routeValues = null);

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
    ActionResult<TModel> Accepted<TModel>(
        IResult result,
        Action<TModel> action,
        string actionName,
        string controllerName,
        object routeValues = null)
        where TModel : new();

    /// <summary>
    /// Deletes d.
    /// </summary>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult Deleted(IResult result);

    /// <summary>
    /// Deletes d.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<TModel> Deleted<TModel>(IResult result);

    /// <summary>
    /// Executes the no content operation.
    /// </summary>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult NoContent(IResult result);

    /// <summary>
    /// Executes the no content operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<TModel> NoContent<TModel>(IResult result);

    /// <summary>
    /// Executes the object operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="model">The model used by the operation.</param>
    /// <param name="statusCode">The status code used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<TModel> Object<TModel>(IResult result, TModel model, int statusCode);

    /// <summary>
    /// Executes the object operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="models">The models used by the operation.</param>
    /// <param name="statusCode">The status code used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<ICollection<TModel>> Object<TModel>(IResult result, IEnumerable<TModel> models, int statusCode);

    /// <summary>
    /// Executes the object operation.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <param name="statusCode">The status code used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    ActionResult<ResultPaged<TModel>> Object<TModel>(ResultPaged<TModel> result, int statusCode);
}
