// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using Serilog;
using Xunit.Abstractions;

public class CustomWebApplicationFactory<TEntryPoint>(
    ITestOutputHelper output = null,
    Action<IServiceCollection> services = null,
    bool fakeAuthenticationEnabled = false,
    string environment = "Development") : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly ITestOutputHelper output = output;
    private readonly string environment = environment;
    private readonly bool fakeAuthenticationEnabled = fakeAuthenticationEnabled;
    private readonly Action<IServiceCollection> services = services;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(this.environment);
        builder.ConfigureAppConfiguration((ctx, cnf) =>
        {
            cnf.SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
               .AddEnvironmentVariables();
        });
        builder.ConfigureLogging(ctx => ctx // TODO: webapp logs are not visible in test log anymore (serilog?)
            .Services.AddSingleton<ILoggerProvider>(sp => new XunitLoggerProvider(this.output)));

        builder.UseSerilog(); // comes before Program.cs > ConfigureLogging
        var loggerConfiguration = new LoggerConfiguration().MinimumLevel.Information();
        if (this.output is not null)
        {
            loggerConfiguration.WriteTo.TestOutput(this.output, Serilog.Events.LogEventLevel.Information);
        }

        Log.Logger = loggerConfiguration.CreateLogger().ForContext<CustomWebApplicationFactory<TEntryPoint>>();

        builder.ConfigureServices(services =>
        {
            this.services?.Invoke(services);

            if (this.fakeAuthenticationEnabled)
            {
                services.AddAuthentication(options => // add a fake authentication handler
                {
                    options.DefaultAuthenticateScheme = FakeAuthenticationHandler.SchemeName; // use the fake handler instead of the jwt handler (Startup)
                    options.DefaultScheme = FakeAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, FakeAuthenticationHandler>(FakeAuthenticationHandler.SchemeName, null);
            }
        });

        return base.CreateHost(builder);
    }
}