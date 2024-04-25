// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.Mapping;

public static class ServiceCollectionExtensions
{
    public static AutoMapperBuilderContext WithEntityMapper(
        this AutoMapperBuilderContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        context.Services.AddTransient<IEntityMapper, AutoMapperEntityMapper>();

        return context;
    }

    public static MapsterBuilderContext WithEntityMapper(
        this MapsterBuilderContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        context.Services.AddTransient<IEntityMapper, MapsterEntityMapper>();

        return context;
    }
}