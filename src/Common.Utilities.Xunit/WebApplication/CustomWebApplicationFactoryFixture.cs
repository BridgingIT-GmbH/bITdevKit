// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Xunit.Abstractions;

/// <summary>
/// CustomWebApplicationFactoryFixture is a specialized WebApplicationFactory for
/// creating integration testing environments with customized configurations.
/// </summary>
/// <typeparam name="TEntryPoint">The entry point of the web application.</typeparam>
public class CustomWebApplicationFactoryFixture<TEntryPoint> // https://xunit.net/docs/shared-context#class-fixture
    : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly List<FakeUser> users = [];
    private string environment = "Development";
    private bool fakeAuthenticationEnabled;
    private Action<IServiceCollection> services;

    /// <summary>
    /// Gets the <see cref="ITestOutputHelper"/> instance for output during integration tests.
    /// </summary>
    /// <value>
    /// The <see cref="ITestOutputHelper"/> used for capturing test output logs.
    /// </value>
    public ITestOutputHelper Output { get; private set; }

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> instance that provides access
    /// to the application's service container.
    /// </summary>
    /// <value>
    /// The <see cref="IServiceProvider"/> used for resolving service dependencies.
    /// </value>
    public IServiceProvider ServiceProvider => this.Services.CreateScope().ServiceProvider;

    /// <summary>
    /// Sets the output helper for logging test results in the WebApplicationFactoryFixture instance.
    /// </summary>
    /// <param name="output">The ITestOutputHelper instance used for logging output in tests.</param>
    /// <returns>Returns the current instance of CustomWebApplicationFactoryFixture for method chaining.</returns>
    public CustomWebApplicationFactoryFixture<TEntryPoint> WithOutput(ITestOutputHelper output)
    {
        this.Output = output;

        return this;
    }

    /// <summary>
    /// Sets the environment for the WebApplicationFactoryFixture instance.
    /// </summary>
    /// <param name="environment">The environment name to set (e.g., "Development", "Staging", "Production").</param>
    /// <returns>Returns the current instance of CustomWebApplicationFactoryFixture for method chaining.</returns>
    public CustomWebApplicationFactoryFixture<TEntryPoint> WithEnvironment(string environment)
    {
        this.environment = environment;

        return this;
    }

    /// <summary>
    /// Enables or disables fake authentication for integration tests.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The updated instance of CustomWebApplicationFactoryFixture with the specified fake authentication setting.</returns>
    public CustomWebApplicationFactoryFixture<TEntryPoint> WithFakeAuthentication(FakeUser user)
    {
        this.fakeAuthenticationEnabled = true;
        this.users.Add(user);

        return this;
    }

    /// <summary>
    /// Enables or disables fake authentication for integration tests.
    /// </summary>
    /// <param name="users">The user.</param>
    /// <returns>The updated instance of CustomWebApplicationFactoryFixture with the specified fake authentication setting.</returns>
    public CustomWebApplicationFactoryFixture<TEntryPoint> WithFakeAuthentication(FakeUser[] users)
    {
        this.fakeAuthenticationEnabled = true;
        this.users.AddRange(users);

        return this;
    }

    /// <summary>
    /// Configures the services for the CustomWebApplicationFactoryFixture.
    /// </summary>
    /// <param name="action">An action to configure the service collection.</param>
    /// <returns>The current instance of CustomWebApplicationFactoryFixture for chaining.</returns>
    public CustomWebApplicationFactoryFixture<TEntryPoint> WithServices(Action<IServiceCollection> action)
    {
        this.services = action;

        return this;
    }

    /// <summary>
    /// Creates and configures an instance of the <see cref="IHost"/> for integration testing.
    /// </summary>
    /// <param name="builder">The <see cref="IHostBuilder"/> to configure the host.</param>
    /// <returns>An instance of <see cref="IHost"/> configured for integration testing.</returns>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(this.environment);
        builder.ConfigureAppConfiguration((ctx, cnf) =>
        {
            cnf.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true)
                .AddEnvironmentVariables();
        });
        builder.ConfigureLogging(ctx => ctx // TODO: webapp logs are not visible in test log anymore (serilog?)
            .Services.AddSingleton<ILoggerProvider>(sp => new XunitLoggerProvider(this.Output)));

        builder.UseSerilog(); // comes before Program.cs > ConfigureLogging
        var loggerConfiguration = new LoggerConfiguration().MinimumLevel.Information();
        if (this.Output is not null)
        {
            loggerConfiguration.WriteTo.TestOutput(this.Output, LogEventLevel.Information);
        }

        Log.Logger = loggerConfiguration.CreateLogger().ForContext<CustomWebApplicationFactoryFixture<TEntryPoint>>();

        builder.ConfigureServices(services =>
        {
            this.services?.Invoke(services);

            if (this.fakeAuthenticationEnabled)
            {
                services.AddFakeAuthentication(this.users);
            }
        });

        return base.CreateHost(builder);
    }
}