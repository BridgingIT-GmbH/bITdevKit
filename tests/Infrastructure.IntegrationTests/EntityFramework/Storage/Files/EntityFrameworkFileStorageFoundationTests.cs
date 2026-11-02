// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using System.Reflection;
using System.Data.Common;
using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[IntegrationTest("Infrastructure")]
[Collection(nameof(IsolatedSqliteTestEnvironmentCollection))]
public class EntityFrameworkFileStorageFoundationTests
{
    [Theory]
    [InlineData("read", true)]
    [InlineData("write", true)]
    [InlineData("read", false)]
    [InlineData("write", false)]
    public async Task FileOperation_DatabaseException_ReportsCancellationOnlyWhenRequested(string operation, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancellationCommandInterceptor(cancellation, cancel);
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<StubDbContext>(options => options.UseSqlite(connection).AddInterceptors(interceptor));
        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<StubDbContext>().Database.EnsureCreatedAsync();
        }

        var logger = Substitute.For<ILogger>();
        var logFactory = Substitute.For<ILoggerFactory>();
        logFactory.CreateLogger(Arg.Any<string>()).Returns(logger);
        var sut = new EntityFrameworkFileStorageProvider<StubDbContext>(serviceProvider, logFactory, "cancellation");
        (await sut.WriteFileAsync("sample", new MemoryStream([1, 2, 3]))).IsSuccess.ShouldBeTrue();
        logger.ClearReceivedCalls();
        interceptor.Armed = true;

        IResult result = operation == "read"
            ? await sut.ReadFileAsync("sample", cancellationToken: cancellation.Token)
            : await sut.WriteFileAsync("sample", new MemoryStream([4]), cancellationToken: cancellation.Token);

        result.Errors.Any(error => error is OperationCancelledError).ShouldBe(cancel);
        result.Errors.Any(error => error is ExceptionError).ShouldBe(!cancel);
        logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "Log")
            .Any(call => Equals(call.GetArguments()[0], LogLevel.Error)).ShouldBe(!cancel);
    }

    [Fact]
    public void StubDbContext_AsFileStorageContext_ExposesRequiredDbSets()
    {
        // Arrange
        using var sut = new StubDbContext(new DbContextOptionsBuilder<StubDbContext>()
            .UseInMemoryDatabase($"file-storage-contract-{Guid.NewGuid():N}")
            .Options);

        // Act
        var context = sut as IFileStorageContext;

        // Assert
        context.ShouldNotBeNull();
        context.StorageFiles.ShouldNotBeNull();
        context.StorageFileContents.ShouldNotBeNull();
        context.StorageDirectories.ShouldNotBeNull();
    }

    [Fact]
    public void AddFileStorage_UseEntityFramework_RegistersProviderWithDefaultDescription()
    {
        // Arrange
        var services = this.CreateServices();
        services.AddFileStorage(factory => factory.RegisterProvider("db", builder =>
        {
            builder.UseEntityFramework<StubDbContext>("DatabaseFiles")
                .WithLifetime(ServiceLifetime.Transient);
        }));

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IFileStorageProviderFactory>();

        // Act
        var sut = factory.CreateProvider("db");

        // Assert
        sut.ShouldNotBeNull();
        sut.ShouldBeOfType<EntityFrameworkFileStorageProvider<StubDbContext>>();
        sut.LocationName.ShouldBe("DatabaseFiles");
        sut.Description.ShouldBe("DatabaseFiles");
    }

    [Fact]
    public void FileStorageProviderFactory_UseEntityFramework_AppliesDescriptionAndInvokesConfigureCallback()
    {
        // Arrange
        var configureCalled = false;
        var services = this.CreateServices();
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var sut = new FileStorageProviderFactory(serviceProvider);

        sut.RegisterProvider("db", builder =>
        {
            builder.UseEntityFramework<StubDbContext>(
                    "DatabaseFiles",
                    "Database-backed file storage",
                    _ => configureCalled = true)
                .WithLifetime(ServiceLifetime.Transient);
        });

        // Act
        var provider = sut.CreateProvider("db");

        // Assert
        configureCalled.ShouldBeTrue();
        provider.ShouldNotBeNull();
        provider.ShouldBeOfType<EntityFrameworkFileStorageProvider<StubDbContext>>();
        provider.LocationName.ShouldBe("DatabaseFiles");
        provider.Description.ShouldBe("Database-backed file storage");
    }

    [Fact]
    public void FileStorageProviderFactory_UseEntityFramework_AppliesConfiguredOptions()
    {
        // Arrange
        var services = this.CreateServices();
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var sut = new FileStorageProviderFactory(serviceProvider);

        sut.RegisterProvider("db", builder =>
        {
            builder.UseEntityFramework<StubDbContext>(
                    "DatabaseFiles",
                    configure: options => options
                        .LeaseDuration(TimeSpan.FromSeconds(45))
                        .RetryCount(7)
                        .RetryBackoff(TimeSpan.FromMilliseconds(500))
                        .PageSize(42)
                        .MaximumBufferedContentSize(2048))
                .WithLifetime(ServiceLifetime.Transient);
        });

        // Act
        var provider = sut.CreateProvider("db");
        var options = this.GetOptions(provider);

        // Assert
        options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(45));
        options.RetryCount.ShouldBe(7);
        options.RetryBackoff.ShouldBe(TimeSpan.FromMilliseconds(500));
        options.PageSize.ShouldBe(42);
        options.MaximumBufferedContentSize.ShouldBe(2048);
        options.LoggerFactory.ShouldBeSameAs(serviceProvider.GetRequiredService<ILoggerFactory>());
    }

    [Fact]
    public async Task FileStorageProviderFactory_UseEntityFramework_SingletonCreationIsScopeSafeAndThreadSafe()
    {
        // Arrange
        var services = this.CreateServices();
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var sut = new FileStorageProviderFactory(serviceProvider);

        sut.RegisterProvider("db", builder =>
        {
            builder.UseEntityFramework<StubDbContext>("DatabaseFiles")
                .WithLifetime(ServiceLifetime.Singleton);
        });

        // Act
        var providers = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => sut.CreateProvider("db"))));

        // Assert
        providers.ShouldNotBeEmpty();
        providers.All(provider => provider is EntityFrameworkFileStorageProvider<StubDbContext>).ShouldBeTrue();
        providers.Skip(1).All(provider => ReferenceEquals(provider, providers[0])).ShouldBeTrue();
    }

    [Fact]
    public async Task EntityFrameworkFileStorageProvider_CheckHealthAsync_WithoutStorageSchema_Fails()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<StubDbContext>(options => options.UseSqlite(connection));

        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var sut = new EntityFrameworkFileStorageProvider<StubDbContext>(
            serviceProvider,
            serviceProvider.GetRequiredService<ILoggerFactory>(),
            "DatabaseFiles");

        // Act
        var result = await sut.CheckHealthAsync(CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<ExceptionError>();
    }

    [Fact]
    public void EntityFrameworkFileStorageOptionsBuilder_Build_UsesExpectedDefaults()
    {
        // Act
        var sut = new EntityFrameworkFileStorageOptionsBuilder().Build();

        // Assert
        sut.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(30));
        sut.RetryCount.ShouldBe(3);
        sut.RetryBackoff.ShouldBe(TimeSpan.FromMilliseconds(250));
        sut.PageSize.ShouldBe(100);
        sut.MaximumBufferedContentSize.ShouldBeNull();
    }

    [Fact]
    public void EntityFrameworkFileStorageOptionsBuilder_Apply_CopiesConfigurationValues()
    {
        // Arrange
        var configuration = new EntityFrameworkFileStorageConfiguration
        {
            LeaseDuration = TimeSpan.FromMinutes(2),
            RetryCount = 5,
            RetryBackoff = TimeSpan.FromSeconds(2),
            PageSize = 128,
            MaximumBufferedContentSize = 4096
        };

        // Act
        var sut = new EntityFrameworkFileStorageOptionsBuilder()
            .Apply(configuration)
            .Build();

        // Assert
        sut.LeaseDuration.ShouldBe(TimeSpan.FromMinutes(2));
        sut.RetryCount.ShouldBe(5);
        sut.RetryBackoff.ShouldBe(TimeSpan.FromSeconds(2));
        sut.PageSize.ShouldBe(128);
        sut.MaximumBufferedContentSize.ShouldBe(4096);
    }

    private ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<StubDbContext>(options => options.UseInMemoryDatabase($"file-storage-{Guid.NewGuid():N}"));

        return services;
    }

    private EntityFrameworkFileStorageOptions GetOptions(IFileStorageProvider provider)
    {
        var optionsProperty = provider.GetType().GetProperty("Options", BindingFlags.Instance | BindingFlags.NonPublic);

        optionsProperty.ShouldNotBeNull();
        optionsProperty.GetValue(provider).ShouldBeOfType<EntityFrameworkFileStorageOptions>();

        return (EntityFrameworkFileStorageOptions)optionsProperty.GetValue(provider);
    }

    private sealed class CancellationCommandInterceptor(CancellationTokenSource cancellation, bool cancel) : DbCommandInterceptor
    {
        internal bool Armed { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            this.FailIfArmed();

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            this.FailIfArmed();

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void FailIfArmed()
        {
            if (!this.Armed)
            {
                return;
            }

            if (cancel)
            {
                cancellation.Cancel();
            }

            throw new SimulatedDatabaseException();
        }
    }

    private sealed class SimulatedDatabaseException() : DbException("The database command was interrupted.");
}