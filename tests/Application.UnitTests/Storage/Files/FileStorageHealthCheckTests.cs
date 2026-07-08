// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

[UnitTest("Application")]
public class FileStorageHealthCheckTests
{
    [Fact]
    public async Task HealthCheck_WithRegisteredProviders_ShouldProbeEveryProvider()
    {
        // Arrange
        var factory = Substitute.For<IFileStorageProviderFactory>();
        var firstProvider = Substitute.For<IFileStorageProvider>();
        var secondProvider = Substitute.For<IFileStorageProvider>();

        firstProvider.LocationName.Returns("FirstLocation");
        firstProvider.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        secondProvider.LocationName.Returns("SecondLocation");
        secondProvider.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        factory.GetProviderNames().Returns(["first", "second"]);
        factory.CreateProvider("first").Returns(firstProvider);
        factory.CreateProvider("second").Returns(secondProvider);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(factory);
        services.TryAddFileStorageHealthCheck();

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var report = await serviceProvider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        // Assert
        report.Status.ShouldBe(HealthStatus.Healthy);
        report.Entries.Count.ShouldBe(1);
        report.Entries.Keys.Single().ShouldBe("FileStorage");
        await firstProvider.Received(1).CheckHealthAsync(Arg.Any<CancellationToken>());
        await secondProvider.Received(1).CheckHealthAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HealthCheck_WithFailingProvider_ShouldReportFailedProvider()
    {
        // Arrange
        var factory = Substitute.For<IFileStorageProviderFactory>();
        var healthyProvider = Substitute.For<IFileStorageProvider>();
        var failingProvider = Substitute.For<IFileStorageProvider>();

        healthyProvider.LocationName.Returns("HealthyLocation");
        healthyProvider.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        failingProvider.LocationName.Returns("FailingLocation");
        failingProvider.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new ValidationError("backend unavailable")));
        factory.GetProviderNames().Returns(["healthy", "failing"]);
        factory.CreateProvider("healthy").Returns(healthyProvider);
        factory.CreateProvider("failing").Returns(failingProvider);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(factory);
        services.TryAddFileStorageHealthCheck();

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var report = await serviceProvider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        var entry = report.Entries["FileStorage"];

        // Assert
        report.Status.ShouldBe(HealthStatus.Unhealthy);
        entry.Description.ShouldContain("failing");
        entry.Data["failedProviderCount"].ShouldBe(1);
        entry.Data["providerErrors"].ShouldBeAssignableTo<string[]>()
            .Single()
            .ShouldContain("backend unavailable");
    }
}
