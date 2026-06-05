// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>
/// Marks an endpoint module as a dashboard plugin endpoint.
/// </summary>
/// <example>
/// <code>
/// public class JobsDashboardEndpoints : EndpointsBase, IDashboardEndpoints
/// {
///     public override void Map(IEndpointRouteBuilder app) { }
/// }
/// </code>
/// </example>
public interface IDashboardEndpoints : IEndpoints
{
}
