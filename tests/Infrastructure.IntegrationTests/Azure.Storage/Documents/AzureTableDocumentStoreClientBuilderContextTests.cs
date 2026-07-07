// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class AzureTableDocumentStoreClientBuilderContextTests : IDisposable
{
    private readonly TestEnvironmentFixture fixture;
    private readonly ITestOutputHelper output;
    private readonly ServiceProvider serviceProvider;

    public AzureTableDocumentStoreClientBuilderContextTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.output = output;

        // register the services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(new XunitLoggerProvider(output)));
        services.AddMediatR();

        services.AddAzureTableServiceClient(o => o
                .UseConnectionString(this.fixture.AzuriteConnectionString));

        services.AddAzureTableDocumentStoreClient<PersonStubDocument>() // no need to setup the client+provider (sql)
            .WithBehavior<LoggingDocumentStoreClientBehavior<PersonStubDocument>>()
            .WithBehavior((inner, sp) =>
                new TimeoutDocumentStoreClientBehavior<PersonStubDocument>(sp.GetRequiredService<ILoggerFactory>(),
                    inner,
                    new TimeoutDocumentStoreClientBehaviorOptions { Timeout = 30.Seconds() }));

        this.serviceProvider = services.BuildServiceProvider();
    }

    //[Fact]
    //public void GetServiceClient_WhenRequested_ShouldNotBeNull()
    //{
    //    // Arrange
    //    // Act
    //    var sut = this.fixture.ServiceProvider.GetService<TableServiceClient>();

    //    // Assert
    //    sut.ShouldNotBeNull();
    //}

    [Fact]
    public void GetDocumentStoreClient_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var sut = this.serviceProvider.GetService<IDocumentStoreClient<PersonStubDocument>>();

        // Assert
        sut.ShouldNotBeNull();
    }

    public void Dispose()
    {
        this.serviceProvider.Dispose();
    }
}
