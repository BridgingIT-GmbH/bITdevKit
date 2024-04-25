// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.Mapping;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterAutomapperAsEntityMapper(this IServiceCollection services)
    {
        return services.AddTransient<IEntityMapper, AutoMapperEntityMapper>();
    }
}