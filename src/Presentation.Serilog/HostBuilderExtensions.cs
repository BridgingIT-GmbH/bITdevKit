// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

public static class HostBuilderExtensions
{
    [Obsolete("Use the new builder.Host.ConfigureLogging(), without the configuration argument")]
    public static IHostBuilder ConfigureLogging(this IHostBuilder builder, IConfiguration configuration)
    {
        return builder.ConfigureLogging();
    }

    public static IHostBuilder ConfigureLogging(this IHostBuilder builder)
    {
        EnsureArg.IsNotNull(builder, nameof(builder));

        Serilog.Debugging.SelfLog.Enable(Console.Error);

        if (Log.Logger.GetType().Name == "SilentLogger") // only setup serilog if not done already
        {
            builder.ConfigureLogging((ctx, c) =>
            {
                var logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(ctx.Configuration)
                    .CreateLogger();

                c.ClearProviders();
                c.AddSerilog(logger);
                //builder.UseSerilog(logger);

                Log.Logger = logger;
            });
        }

        return builder;
    }

    public static IHostBuilder ConfigureLogging(this IHostBuilder builder, Action<LoggerConfiguration> configure)
    {
        EnsureArg.IsNotNull(builder, nameof(builder));
        EnsureArg.IsNotNull(configure, nameof(configure));

        Serilog.Debugging.SelfLog.Enable(Console.Error);

        if (Log.Logger.GetType().Name == "SilentLogger") // only setup serilog if not done already
        {
            builder.ConfigureLogging((ctx, c) =>
            {
                var configuration = new LoggerConfiguration();
                configure.Invoke(configuration);
                var logger = configuration.CreateLogger();

                c.ClearProviders();
                c.AddSerilog(logger);
                //builder.UseSerilog(logger);

                Log.Logger = logger;
            });
        }

        return builder;
    }
}