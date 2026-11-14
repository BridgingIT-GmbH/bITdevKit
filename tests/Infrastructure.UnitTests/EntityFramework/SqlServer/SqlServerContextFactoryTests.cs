// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.SqlServer;

using System.Data;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Checks optional independent SQL Server context factories without opening database connections.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~SqlServerContextFactoryTests</code></example>
public sealed class SqlServerContextFactoryTests
{
    private const string ConnectionString = "Server=localhost;Database=FactoryOptions;Integrated Security=true;TrustServerCertificate=true";

    /// <summary>All SQL registration overloads configure lazy singleton factories and preserve independently owned contexts and scoped options.</summary>
    /// <param name="registration">The existing options, builder-delegate or connection-string registration path.</param>
    [Theory]
    [InlineData("options")]
    [InlineData("builder")]
    [InlineData("string")]
    public async Task OptIn_UsesSharedNativeConfigurationAndFreshCallerOwnedContexts(string registration)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var dependencyResolutions = 0;
        services.AddSingleton(_ => { dependencyResolutions++; return new SingletonDependency(); });
        var configurationCalls = 0;
        var builder = Add(services, registration, options =>
        {
            configurationCalls++;
            options.EnableRetryOnFailure();
            options.CommandTimeout(17).MigrationsAssembly(typeof(ProbeContext).Assembly.GetName().Name)
                .MigrationsHistoryTable("__FactoryHistory", "factory");
        });
        builder.WithDbContextFactory().ShouldBeSameAs(builder);
        builder.WithDbContextFactory().ShouldBeSameAs(builder);
        services.Any(descriptor => descriptor.ServiceType == typeof(SqlServerDbContextFactoryWarning<ProbeContext>)).ShouldBeFalse();
        services.Count(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<ProbeContext>)).ShouldBe(1);
        services.Single(descriptor => descriptor.ServiceType == typeof(ProbeContext)).Lifetime.ShouldBe(ServiceLifetime.Scoped);
        services.Single(descriptor => descriptor.ServiceType == typeof(DbContextOptions<ProbeContext>)).Lifetime.ShouldBe(ServiceLifetime.Scoped);
        configurationCalls.ShouldBe(0);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        configurationCalls.ShouldBe(0);
        dependencyResolutions.ShouldBe(0);

        // Act
        var sut = provider.GetRequiredService<IDbContextFactory<ProbeContext>>();
        configurationCalls.ShouldBeGreaterThan(0);
        var configured = configurationCalls;
        dependencyResolutions.ShouldBe(0);
        await using var first = sut.CreateDbContext();
        await using var second = await sut.CreateDbContextAsync();

        // Assert
        provider.GetRequiredService<IDbContextFactory<ProbeContext>>().ShouldBeSameAs(sut);
        first.ShouldNotBeSameAs(second);
        first.Dependency.ShouldBeSameAs(second.Dependency);
        dependencyResolutions.ShouldBe(1);
        configurationCalls.ShouldBe(configured);
        first.NativeOptions.ShouldBeSameAs(second.NativeOptions);
        AssertConnection(first);
        first.Database.GetCommandTimeout().ShouldBe(17);
        first.Database.CreateExecutionStrategy().RetriesOnFailure.ShouldBeTrue();
        first.Database.GetDbConnection().State.ShouldBe(ConnectionState.Closed);
        var native = first.NativeOptions.Extensions.OfType<RelationalOptionsExtension>().Single();
        native.MigrationsAssembly.ShouldBe(typeof(ProbeContext).Assembly.GetName().Name);
        native.MigrationsHistoryTableName.ShouldBe("__FactoryHistory");
        native.MigrationsHistoryTableSchema.ShouldBe("factory");
        await first.DisposeAsync();
        first.Disposed.ShouldBeTrue();
        second.Disposed.ShouldBeFalse();

        await using var scope = provider.CreateAsyncScope();
        var scoped = scope.ServiceProvider.GetRequiredService<ProbeContext>();
        scoped.ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<ProbeContext>());
        scoped.NativeOptions.ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<DbContextOptions<ProbeContext>>());
        scoped.NativeOptions.ShouldNotBeSameAs(second.NativeOptions);
        AssertConnection(scoped);
        scoped.Database.GetCommandTimeout().ShouldBe(17);
        scoped.Database.CreateExecutionStrategy().RetriesOnFailure.ShouldBeTrue();
        second.Disposed.ShouldBeFalse();
    }

    /// <summary>The optional factory omits request interceptors while scoped business contexts retain their interceptor and scoped dependency lifetimes.</summary>
    [Fact]
    public async Task Factory_ExcludesScopedInterceptorsAndLeavesBusinessScopesIndependent()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<SingletonDependency>();
        services.AddScoped<ScopedDependency>();
        var options = new SqlServerOptions
        {
            ConnectionString = ConnectionString, InterceptorTypes = [typeof(ScopedInterceptor)],
            LoggerEnabled = true, SensitiveDataLoggingEnabled = true, DetailedErrorsEnabled = true,
            IdempotentMigrationsEnabled = true
        };
        services.AddSqlServerDbContext<ProbeContext>(options).WithDbContextFactory();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        // Act
        var sut = provider.GetRequiredService<IDbContextFactory<ProbeContext>>();
        await using var independent = sut.CreateDbContext();
        ScopedDependency scopedDependency;
        ProbeContext business;
        await using (var scope = provider.CreateAsyncScope())
        {
            business = scope.ServiceProvider.GetRequiredService<ProbeContext>();
            var interceptor = scope.ServiceProvider.GetRequiredService<ScopedInterceptor>();
            scopedDependency = interceptor.Dependency;
            var businessOptions = business.NativeOptions.FindExtension<CoreOptionsExtension>();
            businessOptions.Interceptors.ShouldContain(interceptor);
            (independent.NativeOptions.FindExtension<CoreOptionsExtension>().Interceptors ?? []).ShouldBeEmpty();
            businessOptions.LoggerFactory.ShouldBeSameAs(provider.GetRequiredService<ILoggerFactory>());
            independent.NativeOptions.FindExtension<CoreOptionsExtension>().LoggerFactory.ShouldBeSameAs(businessOptions.LoggerFactory);
            independent.GetService<IMigrationsSqlGenerator>().ShouldBeOfType<SqlServerIdempotentMigrationsSqlGenerator>();
            independent.Database.GetDbConnection().State.ShouldBe(ConnectionState.Closed);
            scopedDependency.Disposed.ShouldBeFalse();
        }

        // Assert
        business.Disposed.ShouldBeTrue();
        scopedDependency.Disposed.ShouldBeTrue();
        independent.Disposed.ShouldBeFalse();
    }

    /// <summary>Pre-cancellation creates no context or constructor dependency.</summary>
    [Fact]
    public async Task Factory_PreCancelledCreation_DoesNotActivateDependencies()
    {
        var services = new ServiceCollection();
        var activated = 0;
        services.AddSingleton(_ => { activated++; return new SingletonDependency(); });
        services.AddSqlServerDbContext<ProbeContext>(ConnectionString).WithDbContextFactory();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var sut = provider.GetRequiredService<IDbContextFactory<ProbeContext>>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => sut.CreateDbContextAsync(cancellation.Token));

        activated.ShouldBe(0);
    }

    /// <summary>Singleton factories never capture scoped constructor dependencies when scope validation is enabled.</summary>
    [Fact]
    public async Task Factory_ScopedConstructorDependency_IsRejected()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        services.AddSqlServerDbContext<ScopedConstructorContext>(ConnectionString).WithDbContextFactory();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var sut = provider.GetRequiredService<IDbContextFactory<ScopedConstructorContext>>();

        var exception = Should.Throw<InvalidOperationException>(() => sut.CreateDbContext());

        exception.Message.ShouldContain(nameof(ScopedDependency));
    }

    /// <summary>Existing factory registrations are preserved and one structured startup warning reports the skipped opt-in.</summary>
    [Fact]
    public async Task OptIn_ExistingFactory_PreservesRegistrationAndWarnsOnceAtStartup()
    {
        var services = new ServiceCollection();
        using var activationServices = new ServiceCollection().BuildServiceProvider();
        var existing = new SqlServerDbContextFactory<ProbeContext>(activationServices,
            new DbContextOptionsBuilder<ProbeContext>().UseSqlServer(ConnectionString).Options);
        services.AddSingleton<IDbContextFactory<ProbeContext>>(existing);
        var logger = new RecordingLogger<SqlServerDbContextFactoryWarning<ProbeContext>>();
        services.AddSingleton<ILogger<SqlServerDbContextFactoryWarning<ProbeContext>>>(logger);
        var builder = services.AddSqlServerDbContext<ProbeContext>(ConnectionString);

        builder.WithDbContextFactory().ShouldBeSameAs(builder);
        builder.WithDbContextFactory().ShouldBeSameAs(builder);

        services.Single(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<ProbeContext>)).ImplementationInstance.ShouldBeSameAs(existing);
        logger.Warnings.ShouldBeEmpty();
        await using var provider = services.BuildServiceProvider();
        var diagnostic = provider.GetServices<IHostedService>().OfType<SqlServerDbContextFactoryWarning<ProbeContext>>().ShouldHaveSingleItem();
        await diagnostic.StartAsync(default);
        await diagnostic.StartAsync(default);
        logger.Warnings.ShouldHaveSingleItem().ShouldContain("ExistingFactory");
        provider.GetRequiredService<IDbContextFactory<ProbeContext>>().ShouldBeSameAs(existing);
    }

    /// <summary>Ordinary SQL registration continues to declare only its original context/options lifetime until explicitly opted in.</summary>
    /// <param name="registration">The existing SQL registration overload.</param>
    [Theory]
    [InlineData("options")]
    [InlineData("builder")]
    [InlineData("string")]
    public void Registration_WithoutOptIn_DeclaresNoFactory(string registration)
    {
        var services = new ServiceCollection();

        Add(services, registration);

        services.Any(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<ProbeContext>)).ShouldBeFalse();
        services.Single(descriptor => descriptor.ServiceType == typeof(ProbeContext)).Lifetime.ShouldBe(ServiceLifetime.Scoped);
        services.Single(descriptor => descriptor.ServiceType == typeof(DbContextOptions<ProbeContext>)).Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    /// <summary>A missing native callback skips optional registration and reports one startup warning without changing context services.</summary>
    [Fact]
    public async Task OptIn_MissingNativeConfiguration_SkipsRegistrationAndWarnsAtStartup()
    {
        var services = new ServiceCollection();
        var logger = new RecordingLogger<SqlServerDbContextFactoryWarning<ProbeContext>>();
        services.AddSingleton<ILogger<SqlServerDbContextFactoryWarning<ProbeContext>>>(logger);
        var builder = new SqlServerDbContextBuilderContext<ProbeContext>(services);

        builder.WithDbContextFactory().ShouldBeSameAs(builder);
        builder.WithDbContextFactory().ShouldBeSameAs(builder);

        services.Any(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<ProbeContext>)).ShouldBeFalse();
        await using var provider = services.BuildServiceProvider();
        var diagnostic = provider.GetServices<IHostedService>().OfType<SqlServerDbContextFactoryWarning<ProbeContext>>().ShouldHaveSingleItem();
        await diagnostic.StartAsync(default);
        logger.Warnings.ShouldHaveSingleItem().ShouldContain("MissingNativeOptions");
    }

    private static void AssertConnection(DbContext context)
    {
        var connection = new SqlConnectionStringBuilder(context.Database.GetConnectionString());
        connection.DataSource.ShouldBe("localhost");
        connection.InitialCatalog.ShouldBe("FactoryOptions");
        connection.IntegratedSecurity.ShouldBeTrue();
        connection.TrustServerCertificate.ShouldBeTrue();
    }

    private static SqlServerDbContextBuilderContext<ProbeContext> Add(ServiceCollection services, string registration,
        Action<Microsoft.EntityFrameworkCore.Infrastructure.SqlServerDbContextOptionsBuilder> configure = null) => registration switch
        {
            "options" => services.AddSqlServerDbContext<ProbeContext>(new SqlServerOptions { ConnectionString = ConnectionString }, configure),
            "builder" => services.AddSqlServerDbContext<ProbeContext>(options => options.UseConnectionString(ConnectionString), configure),
            "string" => services.AddSqlServerDbContext<ProbeContext>(ConnectionString, configure),
            _ => throw new ArgumentException("Unknown SQL registration overload.", nameof(registration))
        };

    private sealed class SingletonDependency;

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        /// <summary>Gets formatted host warning messages produced by the startup diagnostic.</summary>
        public List<string> Warnings { get; } = [];

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Warning) { this.Warnings.Add(formatter(state, exception)); }
        }
    }

    private sealed class ScopedDependency : IAsyncDisposable
    {
        /// <summary>Gets whether its owning asynchronous business scope has completed disposal.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        public ValueTask DisposeAsync() { this.Disposed = true; return ValueTask.CompletedTask; }
    }

    private sealed class ScopedInterceptor(ScopedDependency dependency) : SaveChangesInterceptor
    {
        /// <summary>Gets the business-scope dependency excluded from independent factories.</summary>
        public ScopedDependency Dependency { get; } = dependency;
    }

    private sealed class ScopedConstructorContext(DbContextOptions<ScopedConstructorContext> options, ScopedDependency dependency) : DbContext(options)
    {
        /// <summary>Gets the scoped dependency that a singleton factory cannot resolve from the root.</summary>
        public ScopedDependency Dependency { get; } = dependency;
    }

    private sealed class ProbeContext(DbContextOptions<ProbeContext> options, SingletonDependency dependency) : DbContext(options)
    {
        /// <summary>Gets the immutable options passed to this exact context constructor.</summary>
        public DbContextOptions<ProbeContext> NativeOptions { get; } = options;

        /// <summary>Gets the singleton-compatible constructor dependency resolved by the factory.</summary>
        public SingletonDependency Dependency { get; } = dependency;

        /// <summary>Gets whether actual context disposal has completed.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();
            this.Disposed = true;
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            this.Disposed = true;
        }
    }
}