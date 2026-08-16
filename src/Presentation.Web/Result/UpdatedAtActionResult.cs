// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Mvc;

using Infrastructure;

/// <summary>
/// Represents updated at action result.
/// </summary>
/// <param name="actionName">The action name used by the operation.</param>
/// <param name="controllerName">The controller name used by the operation.</param>
/// <param name="routeValues">The route values used by the operation.</param>
/// <param name="value">The value used by the operation.</param>
public class UpdatedAtActionResult(
    string actionName,
    string controllerName,
    object routeValues,
    [ActionResultObjectValue] object value) : CreatedAtActionResult(actionName, controllerName, routeValues, value)
{ }
