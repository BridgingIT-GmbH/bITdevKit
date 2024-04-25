// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.Options;

[Obsolete("Use the options and the builder directly")]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureUtilities(this IServiceCollection services)
    {
        services.AddTransient<ILoggerOptions, OptionsBase>();
        return services;
    }

    public static IServiceCollection ConfigureUtilities(this IServiceCollection services, Builder<LoggerOptionsBuilder, LoggerOptions> optionsBuilder)
    {
        services.AddTransient<ILoggerOptions>(sp => optionsBuilder(new LoggerOptionsBuilder()).Build());
        return services;
    }
}