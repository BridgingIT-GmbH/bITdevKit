﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Provides extension methods for registering identity and entity permission services in the DI container.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentity(
        this IServiceCollection services,
        Action<IdentityOptionsBuilder> configure, IConfiguration configuration = null)
    {
        var builder = new IdentityOptionsBuilder(services, configuration);
        configure(builder);

        return services;
    }
}

public class IdentityOptionsBuilder(IServiceCollection services, IConfiguration configuration = null)
{
    public IServiceCollection Services { get; } = services;

    public IConfiguration Configuration { get; } = configuration;
}
