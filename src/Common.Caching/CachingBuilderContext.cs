// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class CachingBuilderContext(IServiceCollection services, IConfiguration configuration = null)
{
    public IServiceCollection Services { get; } = services;

    public IConfiguration Configuration { get; } = configuration;
}