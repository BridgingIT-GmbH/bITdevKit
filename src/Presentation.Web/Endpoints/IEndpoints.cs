// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Routing;

/// <summary>
/// Represents an interface for defining api endpoints in the application.
/// </summary>
public interface IEndpoints
{
    /// <summary>
    /// Gets or sets a value indicating whether the endpoints are enabled.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the endpoints are registered.
    /// </summary>
    bool IsRegistered { get; set; }

    /// <summary>
    /// Maps the endpoints to the specified <see cref="IEndpointRouteBuilder"/>.
    /// </summary>
    /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to map the endpoints to.</param>
    void Map(IEndpointRouteBuilder app);
}