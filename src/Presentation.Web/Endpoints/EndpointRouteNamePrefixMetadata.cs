// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
///     Stores the route name prefix configured for an endpoint group.
/// </summary>
/// <param name="Prefix">The prefix that endpoint implementations can use when building endpoint names.</param>
/// <remarks>
///     This metadata is added to groups when <see cref="EndpointsOptionsBase.RouteNamePrefix" /> is configured. ASP.NET
///     Core does not automatically rewrite endpoint names from group metadata, so endpoint implementations should use
///     <see cref="EndpointsBase.BuildRouteName" /> when assigning names with <c>WithName</c>.
/// </remarks>
public sealed record EndpointRouteNamePrefixMetadata(string Prefix);