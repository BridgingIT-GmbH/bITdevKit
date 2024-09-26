// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Xunit.Abstractions;

// origin: https://danieldonbavand.com/2022/06/13/using-playwright-with-the-webapplicationfactory-to-test-a-blazor-application/
/// <summary>
/// A fixture for creating a Kestrel web application factory for end-to-end testing
/// with a specified entry point.
/// </summary>
/// <typeparam name="TEntryPoint">The type to use as the entry point for the application.</typeparam>
public class KestrelWebApplicationFactoryFixture<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    /// <summary>
    /// Represents the host instance for managing the lifecycle of the Kestrel server used in the test fixture.
    /// </summary>
    private IHost host;

    /// <summary>
    /// Gets the instance of <see cref="ITestOutputHelper"/> used for outputting
    /// log information during the test execution.
    /// </summary>
    /// <remarks>
    /// This property is set by invoking the <see cref="WithOutput"/> method, which
    /// allows for capturing and displaying logs within the test framework.
    /// </remarks>
    public ITestOutputHelper Output { get; private set; }

    /// <summary>
    /// Gets the server address to which the WebApplicationFactory instance is connected.
    /// </summary>
    /// <remarks>
    /// The ServerAddress property ensures that the server is running and returns the base address
    /// of the client options as a string.
    /// </remarks>
    public string ServerAddress
    {
        get
        {
            this.EnsureServer();

            return this.ClientOptions.BaseAddress.ToString();
        }
    }

    /// <summary>
    /// Associates an <see cref="ITestOutputHelper"/> with the
    /// <see cref="KestrelWebApplicationFactoryFixture{TEntryPoint}"/> instance.
    /// </summary>
    /// <param name="output">The output helper to associate with this fixture.</param>
    /// <returns>The current instance of <see cref="KestrelWebApplicationFactoryFixture{TEntryPoint}"/> for chaining purposes.</returns>
    public KestrelWebApplicationFactoryFixture<TEntryPoint> WithOutput(ITestOutputHelper output)
    {
        this.Output = output;

        return this;
    }

    /// <summary>
    /// Creates and configures an <see cref="IHost" /> instance for testing.
    /// </summary>
    /// <param name="builder">The host builder to configure and create the host from.</param>
    /// <returns>The configured host for testing purposes.</returns>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Create the host for TestServer now before we
        // modify the builder to use Kestrel instead.
        var testHost = builder.Build();

        // Modify the host builder to use Kestrel instead
        // of TestServer so we can listen on a real address.
        builder.ConfigureWebHost(webHostBuilder => webHostBuilder.UseKestrel());
        builder.ConfigureLogging(ctx => ctx // TODO: webapp logs are not visible in test log anymore (serilog?)
            .Services.AddSingleton<ILoggerProvider>(sp => new XunitLoggerProvider(this.Output)));

        builder.UseSerilog(); // comes before Program.cs > ConfigureLogging

        var loggerConfiguration = new LoggerConfiguration().MinimumLevel.Information();
        if (this.Output is not null)
        {
            loggerConfiguration.WriteTo.TestOutput(this.Output, LogEventLevel.Information);
        }

        Log.Logger = loggerConfiguration.CreateLogger().ForContext<CustomWebApplicationFactory<TEntryPoint>>();

        // reset the module registrations
        foreach (var module in testHost.Services.GetServices<IModule>().SafeNull())
        {
            module.IsRegistered = false;
        }

        // Create and start the Kestrel server before the test server,
        // otherwise due to the way the deferred host builder works
        // for minimal hosting, the server will not get "initialized
        // enough" for the address it is listening on to be available.
        // See https://github.com/dotnet/aspnetcore/issues/33846.
        this.host = builder.Build();
        this.host.Start();

        // Extract the selected dynamic port out of the Kestrel server
        // and assign it onto the client options for convenience so it
        // "just works" as otherwise it'll be the default http://localhost
        // URL, which won't route to the Kestrel-hosted HTTP server.
        var server = this.host.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();

        this.ClientOptions.BaseAddress = addresses!.Addresses.Select(x => new Uri(x)).Last();

        // Return the host that uses TestServer, rather than the real one.
        // Otherwise the internals will complain about the host's server
        // not being an instance of the concrete type TestServer.
        // See https://github.com/dotnet/aspnetcore/pull/34702.
        testHost.Start();

        return testHost;
    }

    /// <summary>
    /// Disposes of the resources used by the KestrelWebApplicationFactoryFixture.
    /// </summary>
    /// <param name="disposing">Indicates whether the method is being called from the Dispose method (true) or from a finalizer (false).</param>
    protected override void Dispose(bool disposing)
    {
        this.host?.Dispose();
    }

    /// Ensures that the Kestrel server is initialized and running.
    /// This method checks if the server has already been created and started.
    /// If the server is not yet initialized, it forces the WebApplicationFactory
    /// to bootstrap the server by creating a default client. This is necessary
    /// to properly set up the server environment for tests.
    private void EnsureServer()
    {
        if (this.host is null)
        {
#pragma warning disable SA1312 // Variable names should begin with lower-case letter
            // This forces WebApplicationFactory to bootstrap the server
            using var _ = this.CreateDefaultClient();
#pragma warning restore SA1312 // Variable names should begin with lower-case letter
        }
    }
}