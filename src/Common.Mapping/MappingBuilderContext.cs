﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Configuration;

public class MappingBuilderContext(IServiceCollection services, IConfiguration configuration = null)
{
    public IServiceCollection Services { get; } = services;

    public IConfiguration Configuration { get; } = configuration;
}

public class AutoMapperBuilderContext(IServiceCollection services, IConfiguration configuration = null)
{
    public IServiceCollection Services { get; } = services;

    public IConfiguration Configuration { get; } = configuration;
}

public class MapsterBuilderContext(IServiceCollection services, IConfiguration configuration = null)
{
    public IServiceCollection Services { get; } = services;

    public IConfiguration Configuration { get; } = configuration;
}