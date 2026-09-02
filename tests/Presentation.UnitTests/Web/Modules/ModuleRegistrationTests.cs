namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Modules;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;

[Collection(ModuleActivityListenerTestCollection.Name)]
[UnitTest("Presentation")]
public class ModuleRegistrationTests
{
    private const string FixtureAssemblyName = "BridgingIT.DevKit.Presentation.UnitTests.ModulesFixtures";
    private const string FixtureModuleTypeName =
        "BridgingIT.DevKit.Presentation.UnitTests.ModulesFixtures.ExternalTrackingWebModule";

    [Fact]
    public void WithModule_GenericTypeLoadedDuringCallback_UsesOneInstanceForCompleteLifecycle()
    {
        // Arrange
        var loadContext = new AssemblyLoadContext($"module-fixture-{Guid.NewGuid():N}", true);
        loadContext.Resolving += ResolveFromDefaultContext;
        Type moduleType = null;

        var builder = DevKitWebApplication.CreateBuilder([])
            .AddModules(context =>
            {
                loadContext.Assemblies.ShouldBeEmpty();
                var assembly = loadContext.LoadFromAssemblyPath(GetFixtureAssemblyPath());
                moduleType = assembly.GetType(FixtureModuleTypeName, true);
                InvokeGenericWithModule(context, moduleType);
            });
        using var app = builder.Build();
        var registry = app.Services.GetRequiredService<IModuleRegistry>();
        var module = registry.Modules.Single();

        // Act
        app.UseModules();
        app.MapModules();

        // Assert
        module.GetType().ShouldBe(moduleType);
        app.Services.GetServices<IModule>().Single().ShouldBeSameAs(module);
        GetProperty<int>(module, "RegisterCount").ShouldBe(1);
        GetProperty<int>(module, "UseCount").ShouldBe(1);
        GetProperty<int>(module, "MapCount").ShouldBe(1);

        var instanceId = GetProperty<Guid>(module, "InstanceId");
        GetProperty<Guid?>(module, "RegisterInstanceId").ShouldBe(instanceId);
        GetProperty<Guid?>(module, "UseInstanceId").ShouldBe(instanceId);
        GetProperty<Guid?>(module, "MapInstanceId").ShouldBe(instanceId);
        loadContext.Unload();
    }

    [Fact]
    public void WithModule_ExplicitUndiscoveredInstance_RegistersLifecycleAndContextAccess()
    {
        // Arrange
        var module = new TrackingWebModule("instance");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddModules(builder.Configuration, builder.Environment, context => context.WithModule(module));
        using var app = builder.Build();
        var request = new DefaultHttpContext().Request;
        request.Headers[ModuleConstants.ModuleNameKey] = module.Name;

        // Act
        app.UseModules();
        app.MapModules();
        var resolvedModule = app.Services.GetServices<IRequestModuleContextAccessor>().Find(request);

        // Assert
        app.Services.GetRequiredService<IModuleRegistry>().Modules.Single().ShouldBeSameAs(module);
        app.Services.GetServices<IModule>().Single().ShouldBeSameAs(module);
        resolvedModule.ShouldBeSameAs(module);
        module.RegisterCount.ShouldBe(1);
        module.UseCount.ShouldBe(1);
        module.MapCount.ShouldBe(1);
    }

    [Fact]
    public void WithModule_ValidRuntimeType_CreatesAndRegistersModule()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = services.AddModules(new ConfigurationBuilder().Build(), _ => { });

        // Act
        context.WithModule(typeof(TrackingWebModule));

        // Assert
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IModuleRegistry>().Modules.Single().GetType().ShouldBe(typeof(TrackingWebModule));
        provider.GetServices<IModule>().Count().ShouldBe(1);
    }

    public static TheoryData<Type, string> InvalidModuleTypes => new()
    {
        { typeof(string), "must implement" },
        { typeof(IModule), "cannot be an interface" },
        { typeof(AbstractModule), "cannot be abstract" },
        { typeof(OpenGenericModule<>), "cannot contain open generic parameters" }
    };

    [Theory]
    [MemberData(nameof(InvalidModuleTypes))]
    public void WithModule_InvalidRuntimeType_ThrowsDescriptiveException(Type moduleType, string expectedMessage)
    {
        // Arrange
        var context = new ServiceCollection().AddModules(new ConfigurationBuilder().Build(), _ => { });

        // Act
        var exception = Should.Throw<ArgumentException>(() => context.WithModule(moduleType));

        // Assert
        exception.Message.ShouldContain(moduleType.FullName);
        exception.Message.ShouldContain(expectedMessage);
    }

    [Fact]
    public void WithModule_NullRuntimeType_ThrowsArgumentNullException()
    {
        var context = new ServiceCollection().AddModules(new ConfigurationBuilder().Build(), _ => { });

        Should.Throw<ArgumentNullException>(() => context.WithModule((Type)null));
    }

    [Fact]
    public void WithModule_TypeWithoutSupportedConstructor_ThrowsDescriptiveException()
    {
        // Arrange
        var context = new ServiceCollection().AddModules(new ConfigurationBuilder().Build(), _ => { });

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => context.WithModule(typeof(ModuleWithoutDefaultConstructor)));

        // Assert
        exception.Message.ShouldContain(typeof(ModuleWithoutDefaultConstructor).FullName);
        exception.Message.ShouldContain("public parameterless constructor");
    }

    [Fact]
    public void AddModules_TwoHostsAndSharedInstance_KeepsRegistriesIndependentAndRegistersPerHost()
    {
        // Arrange
        var sharedModule = new TrackingWebModule("shared");
        var hostAModule = new HostAModule();
        var hostBModule = new HostBModule();
        var builderA = WebApplication.CreateBuilder();
        var builderB = WebApplication.CreateBuilder();

        // Act
        builderA.Services.AddModules(builderA.Configuration, builderA.Environment, modules => modules
            .WithModule(sharedModule)
            .WithModule(hostAModule));
        builderB.Services.AddModules(builderB.Configuration, builderB.Environment, modules => modules
            .WithModule(sharedModule)
            .WithModule(hostBModule));
        using var appA = builderA.Build();
        using var appB = builderB.Build();
        var registryA = appA.Services.GetRequiredService<IModuleRegistry>();
        var registryB = appB.Services.GetRequiredService<IModuleRegistry>();

        // Assert
        registryA.ShouldNotBeSameAs(registryB);
        registryA.Modules.ShouldContain(hostAModule);
        registryA.Modules.ShouldNotContain(hostBModule);
        registryB.Modules.ShouldContain(hostBModule);
        registryB.Modules.ShouldNotContain(hostAModule);
        sharedModule.RegisterCount.ShouldBe(2);
        appA.Services.GetServices<IModule>().Count().ShouldBe(2);
        appB.Services.GetServices<IModule>().Count().ShouldBe(2);
    }

    [Fact]
    public void WithModule_RepeatedSelections_AreIdempotentAndKeepDeterministicOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = services.AddModules(new ConfigurationBuilder().Build(), _ => { });

        // Act
        context.WithModule<AlphaModule>();
        var firstInstance = (AlphaModule)services.Single(
            descriptor => descriptor.ServiceType == typeof(IModule)).ImplementationInstance;
        context.WithModule(firstInstance)
            .WithModule(firstInstance)
            .WithModule<AlphaModule>()
            .WithModule<ZuluModule>()
            .WithModule<BravoModule>();
        context.WithModule(typeof(AlphaModule));
        using var provider = services.BuildServiceProvider();

        // Assert
        firstInstance.RegisterCount.ShouldBe(1);
        provider.GetServices<IModule>().Select(module => module.Name)
            .ShouldBe(["bravo", "alpha", "zulu"]);
        provider.GetRequiredService<IModuleRegistry>().Modules.Select(module => module.Name)
            .ShouldBe(["bravo", "alpha", "zulu"]);
        services.Count(descriptor => descriptor.ServiceType == typeof(IModule)).ShouldBe(3);
        services.Count(descriptor => descriptor.ServiceType == typeof(ActivitySource)).ShouldBe(5);
    }

    [Fact]
    public void AddModules_RepeatedAssemblyDiscovery_IsIdempotent()
    {
        // Arrange
        var fixtureAssembly = Assembly.LoadFrom(GetFixtureAssemblyPath());
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddModules(configuration, null, fixtureAssembly);
        services.AddModules(configuration, null, fixtureAssembly);
        using var provider = services.BuildServiceProvider();
        var module = provider.GetRequiredService<IModuleRegistry>().Modules.Single();

        // Assert
        GetProperty<int>(module, "RegisterCount").ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IModule)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(ActivitySource)).ShouldBe(3);
    }

    [Fact]
    public void WithModule_DifferentInstanceOfRegisteredType_ThrowsWithoutAddingDiRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = services.AddModules(new ConfigurationBuilder().Build(), _ => { });
        context.WithModule(new AlphaModule());

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => context.WithModule(new AlphaModule()));

        // Assert
        exception.Message.ShouldContain(typeof(AlphaModule).FullName);
        exception.Message.ShouldContain("different instance");
        services.Count(descriptor => descriptor.ServiceType == typeof(IModule)).ShouldBe(1);
    }

    [Fact]
    public void WithModule_DifferentTypesWithSameName_ThrowsDescriptiveException()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = services.AddModules(new ConfigurationBuilder().Build(), _ => { });
        context.WithModule(new DuplicateNameModuleA());

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => context.WithModule(new DuplicateNameModuleB()));

        // Assert
        exception.Message.ShouldContain("duplicate");
        exception.Message.ShouldContain(typeof(DuplicateNameModuleA).FullName);
        exception.Message.ShouldContain(typeof(DuplicateNameModuleB).FullName);
        services.Count(descriptor => descriptor.ServiceType == typeof(IModule)).ShouldBe(1);
    }

    [Fact]
    public void AddModules_CallbackRegistration_AppliesDisabledStateBeforeCompleteLifecycle()
    {
        // Arrange
        var configuration = CreateDisabledConfiguration("disabled");
        var module = new TrackingWebModule("disabled");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddModules(configuration, builder.Environment, context => context.WithModule(module));
        using var app = builder.Build();

        // Act
        app.UseModules(configuration, app.Environment);
        app.MapModules(configuration, app.Environment);

        // Assert
        module.EnabledDuringRegister.ShouldBeFalse();
        module.EnabledDuringUse.ShouldBeFalse();
        module.EnabledDuringMap.ShouldBeFalse();
        app.Services.GetRequiredService<IModuleRegistry>().Modules.ShouldContain(module);
    }

    [Fact]
    public void AddModules_AssemblyDiscovery_AppliesDisabledStateBeforeCompleteLifecycle()
    {
        // Arrange
        var configuration = CreateDisabledConfiguration("externaltrackingweb");
        var fixtureAssembly = Assembly.LoadFrom(GetFixtureAssemblyPath());
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddModules(configuration, builder.Environment, fixtureAssembly);
        using var app = builder.Build();
        var module = app.Services.GetRequiredService<IModuleRegistry>().Modules.Single();

        // Act
        app.UseModules(configuration, app.Environment);
        app.MapModules(configuration, app.Environment);

        // Assert
        GetProperty<bool?>(module, "EnabledDuringRegister").ShouldBe(false);
        GetProperty<bool?>(module, "EnabledDuringUse").ShouldBe(false);
        GetProperty<bool?>(module, "EnabledDuringMap").ShouldBe(false);
    }

    [Fact]
    public async Task AddModules_RepeatedCallsAndConcurrentHosts_InstallOneActivityCallback()
    {
        // Arrange
        var sink = new CollectingSink();
        var originalLogger = Log.Logger;
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        Log.Logger = logger;
        using var hostA = CreateHostWithRepeatedModuleRegistration();
        using var hostB = CreateHostWithRepeatedModuleRegistration();
        var activityName = $"module-listener-{Guid.NewGuid():N}";

        try
        {
            await hostA.StartAsync();
            await hostB.StartAsync();

            // Act
            using (var source = new ActivitySource($"module-test-source-{Guid.NewGuid():N}"))
            using (var activity = source.StartActivity(activityName))
            {
                activity.ShouldNotBeNull();
                activity.AddBaggage("module-test", "value");
            }

            // Assert
            hostA.Services.GetServices<IHostedService>().Count().ShouldBe(1);
            hostB.Services.GetServices<IHostedService>().Count().ShouldBe(1);
            sink.Events.Count(logEvent => logEvent.RenderMessage().Contains(activityName, StringComparison.Ordinal))
                .ShouldBe(2);
        }
        finally
        {
            await hostB.StopAsync();
            await hostA.StopAsync();
            Log.Logger = originalLogger;
        }
    }

    private static IHost CreateHostWithRepeatedModuleRegistration()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddModules(builder.Configuration, _ => { });
        builder.Services.AddModules(builder.Configuration, _ => { });
        return builder.Build();
    }

    private static IConfiguration CreateDisabledConfiguration(string moduleName)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                [$"Modules:{moduleName}:Enabled"] = "false"
            })
            .Build();
    }

    private static string GetFixtureAssemblyPath()
    {
        return Path.Combine(AppContext.BaseDirectory, $"{FixtureAssemblyName}.dll");
    }

    private static Assembly ResolveFromDefaultContext(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        return AssemblyLoadContext.Default.Assemblies.FirstOrDefault(
            assembly => AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName));
    }

    private static void InvokeGenericWithModule(ModuleBuilderContext context, Type moduleType)
    {
        typeof(ModuleRegistrationTests)
            .GetMethod(nameof(WithModuleGeneric), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(moduleType)
            .Invoke(null, [context]);
    }

    private static void WithModuleGeneric<TModule>(ModuleBuilderContext context)
        where TModule : class, IModule
    {
        context.WithModule<TModule>();
    }

    private static T GetProperty<T>(object instance, string name)
    {
        return (T)instance.GetType().GetProperty(name).GetValue(instance);
    }

    public class TrackingWebModule : WebModuleBase
    {
        public TrackingWebModule()
            : this("tracking")
        {
        }

        public TrackingWebModule(string name, int priority = 99)
            : base(name, priority)
        {
        }

        public int RegisterCount { get; private set; }

        public int UseCount { get; private set; }

        public int MapCount { get; private set; }

        public bool EnabledDuringRegister { get; private set; }

        public bool EnabledDuringUse { get; private set; }

        public bool EnabledDuringMap { get; private set; }

        public override IServiceCollection Register(
            IServiceCollection services,
            IConfiguration configuration = null,
            IWebHostEnvironment environment = null)
        {
            this.RegisterCount++;
            this.EnabledDuringRegister = this.Enabled;
            return services;
        }

        public override IApplicationBuilder Use(
            IApplicationBuilder app,
            IConfiguration configuration = null,
            IWebHostEnvironment environment = null)
        {
            this.UseCount++;
            this.EnabledDuringUse = this.Enabled;
            return app;
        }

        public override IEndpointRouteBuilder Map(
            IEndpointRouteBuilder app,
            IConfiguration configuration = null,
            IWebHostEnvironment environment = null)
        {
            this.MapCount++;
            this.EnabledDuringMap = this.Enabled;
            return app;
        }
    }

    public sealed class AlphaModule : TrackingWebModule
    {
        public AlphaModule()
            : base("alpha", 20)
        {
        }
    }

    public sealed class BravoModule : TrackingWebModule
    {
        public BravoModule()
            : base("bravo", 10)
        {
        }
    }

    public sealed class ZuluModule : TrackingWebModule
    {
        public ZuluModule()
            : base("zulu", 20)
        {
        }
    }

    public sealed class HostAModule : TrackingWebModule
    {
        public HostAModule()
            : base("host-a")
        {
        }
    }

    public sealed class HostBModule : TrackingWebModule
    {
        public HostBModule()
            : base("host-b")
        {
        }
    }

    public sealed class DuplicateNameModuleA : TrackingWebModule
    {
        public DuplicateNameModuleA()
            : base("duplicate")
        {
        }
    }

    public sealed class DuplicateNameModuleB : TrackingWebModule
    {
        public DuplicateNameModuleB()
            : base("DUPLICATE")
        {
        }
    }

    public abstract class AbstractModule : ModuleBase
    {
    }

    public sealed class OpenGenericModule<T> : TrackingWebModule
    {
    }

    public sealed class ModuleWithoutDefaultConstructor : TrackingWebModule
    {
        public ModuleWithoutDefaultConstructor(string name)
            : base(name)
        {
        }
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public ConcurrentBag<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            this.Events.Add(logEvent);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ModuleActivityListenerTestCollection
{
    public const string Name = "Module activity listener";
}
