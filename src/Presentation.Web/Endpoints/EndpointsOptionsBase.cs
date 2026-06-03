// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
///     Provides shared configuration used when endpoint modules are grouped and mapped.
/// </summary>
/// <remarks>
///     These options are consumed by <see cref="EndpointsBase.MapGroup" /> to create a Minimal API route group, apply
///     tags, optionally hide the group from API descriptions, and attach authorization requirements.
/// </remarks>
public abstract class EndpointsOptionsBase
{
    /// <summary>
    ///     Gets or sets whether the endpoint module should map its routes.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the route prefix used when creating the endpoint group.
    /// </summary>
    public string GroupPath { get; set; } = "/api";

    /// <summary>
    ///     Gets or sets the OpenAPI tag applied to the endpoint group.
    /// </summary>
    public string GroupTag { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets whether authorization is required for the endpoint group.
    /// </summary>
    public bool RequireAuthorization { get; set; }

    /// <summary>
    ///     Gets or sets whether the endpoint group is omitted from generated API descriptions.
    /// </summary>
    public bool ExcludeFromDescription { get; set; }

    /// <summary>
    ///     Gets or sets the roles required when authorization is enabled for the endpoint group.
    /// </summary>
    public string[] RequireRoles { get; set; } = []; // roles

    /// <summary>
    ///     Gets or sets the authorization policy required when authorization is enabled for the endpoint group.
    /// </summary>
    public string RequirePolicy { get; set; }
}