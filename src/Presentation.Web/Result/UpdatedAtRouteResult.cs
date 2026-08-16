// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Mvc;

using Infrastructure;

/// <summary>
/// Represents updated at route result.
/// </summary>
public class UpdatedAtRouteResult : CreatedAtRouteResult
{
    /// <summary>
    /// Initializes a new instance of the <c>UpdatedAtRouteResult</c> class.
    /// </summary>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <param name="value">The value used by the operation.</param>
    public UpdatedAtRouteResult(object routeValues, [ActionResultObjectValue] object value)
        : base(routeValues, value) { }

    /// <summary>
    /// Initializes a new instance of the <c>UpdatedAtRouteResult</c> class.
    /// </summary>
    /// <param name="routeName">The route name used by the operation.</param>
    /// <param name="routeValues">The route values used by the operation.</param>
    /// <param name="value">The value used by the operation.</param>
    public UpdatedAtRouteResult(string routeName, object routeValues, [ActionResultObjectValue] object value)
        : base(routeName, routeValues, value) { }
}
