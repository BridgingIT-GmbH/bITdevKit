// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Mvc.Infrastructure;

public class UpdatedAtActionResult(string actionName, string controllerName, object routeValues, [ActionResultObjectValue] object value) : CreatedAtActionResult(actionName, controllerName, routeValues, value)
{
}