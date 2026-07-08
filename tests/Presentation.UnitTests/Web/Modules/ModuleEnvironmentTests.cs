namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Modules;

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BridgingIT.DevKit.Presentation.Web;

[UnitTest("Presentation")]
public class ModuleEnvironmentTests : IDisposable
{
    public ModuleEnvironmentTests()
    {
        ResetModuleState();
    }

    [Fact]
    public void AddModules_WhenUsingDevKitWebApplicationBuilder_PassesEnvironmentToModuleRegister()
    {
        // Arrange & Act
        var builder = DevKitWebApplication.CreateBuilder([])
            .AddModules(modules => modules.WithModule<TrackingWebModule>());

        // Assert
        TrackingWebModule.RegisterEnvironmentName.ShouldBe(builder.Environment.EnvironmentName);
    }

    [Fact]
    public void UseModulesAndMapModules_WhenUsingWebApplication_PassEnvironmentToModules()
    {
        // Arrange
        var builder = DevKitWebApplication.CreateBuilder([])
            .AddModules(modules => modules.WithModule<TrackingWebModule>());
        var app = builder.Build();

        // Act
        app.UseModules();
        app.MapModules();

        // Assert
        TrackingWebModule.UseEnvironmentName.ShouldBe(app.Environment.EnvironmentName);
        TrackingWebModule.MapEnvironmentName.ShouldBe(app.Environment.EnvironmentName);
    }

    public void Dispose()
    {
        ResetModuleState();
    }

    private static void ResetModuleState()
    {
        SetStaticModuleField(typeof(Microsoft.Extensions.DependencyInjection.ModuleExtensions), null);
        SetStaticModuleField(typeof(WebModuleExtensions), null);
        TrackingWebModule.Reset();
    }

    private static void SetStaticModuleField(Type type, object value)
    {
        type.GetField("modules", BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, value);
    }

    public sealed class TrackingWebModule : WebModuleBase
    {
        public static string RegisterEnvironmentName { get; private set; }

        public static string UseEnvironmentName { get; private set; }

        public static string MapEnvironmentName { get; private set; }

        public static void Reset()
        {
            RegisterEnvironmentName = null;
            UseEnvironmentName = null;
            MapEnvironmentName = null;
        }

        public override IServiceCollection Register(
            IServiceCollection services,
            IConfiguration configuration = null,
            IWebHostEnvironment environment = null)
        {
            RegisterEnvironmentName = environment?.EnvironmentName;

            return services;
        }

        public override IApplicationBuilder Use(
            IApplicationBuilder app,
            IConfiguration configuration = null,
            IWebHostEnvironment environment = null)
        {
            UseEnvironmentName = environment?.EnvironmentName;

            return app;
        }

        public override IEndpointRouteBuilder Map(
            IEndpointRouteBuilder app,
            IConfiguration configuration = null,
            IWebHostEnvironment environment = null)
        {
            MapEnvironmentName = environment?.EnvironmentName;

            return app;
        }
    }
}
