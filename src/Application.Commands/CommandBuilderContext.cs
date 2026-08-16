// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

/// <summary>
/// Represents command builder context.
/// </summary>
/// <param name="services">The service collection to configure.</param>
public class CommandBuilderContext(IServiceCollection services)
{
    /// <summary>
    /// Gets the services.
    /// </summary>
    public IServiceCollection Services { get; } = services;
}
