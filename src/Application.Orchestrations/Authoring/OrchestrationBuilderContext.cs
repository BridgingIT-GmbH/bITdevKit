// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the shared builder context for orchestration registration.
/// </summary>
public class OrchestrationBuilderContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationBuilderContext" /> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public OrchestrationBuilderContext(IServiceCollection services)
    {
        this.Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; }
}