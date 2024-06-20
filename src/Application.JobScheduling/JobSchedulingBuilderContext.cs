// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class JobSchedulingBuilderContext(IServiceCollection services, IConfiguration configuration = null, JobSchedulingOptions options = null)
{
    public IServiceCollection Services { get; } = services;

    public IConfiguration Configuration { get; } = configuration;

    public JobSchedulingOptions Options { get; } = options;
}