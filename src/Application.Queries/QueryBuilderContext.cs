// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Represents query builder context.
/// </summary>
/// <param name="services">The service collection to configure.</param>
public class QueryBuilderContext(IServiceCollection services)
{
    /// <summary>
    /// Gets the services.
    /// </summary>
    public IServiceCollection Services { get; } = services;
}
