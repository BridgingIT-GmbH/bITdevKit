// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Mvc.Infrastructure;

public class UpdatedAtRouteResult : CreatedAtRouteResult
{
    public UpdatedAtRouteResult(object routeValues, [ActionResultObjectValue] object value)
        : base(routeValues, value)
    {
    }

    public UpdatedAtRouteResult(string routeName, object routeValues, [ActionResultObjectValue] object value)
        : base(routeName, routeValues, value)
    {
    }
}