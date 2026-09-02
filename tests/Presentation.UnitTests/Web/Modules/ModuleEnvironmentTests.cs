namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Modules;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BridgingIT.DevKit.Presentation.Web;

[UnitTest("Presentation")]
public class ModuleEnvironmentTests
{
    [Fact]
    public void AddModules_WhenUsingDevKitWebApplicationBuilder_PassesEnvironmentToModuleRegister()
    {
        // Arrange & Act
        var builder = DevKitWebApplication.CreateBuilder([])
            .AddModules(modules => modules.WithModule<TrackingWebModule>());
        using var app = builder.Build();
        var module = app.Services.GetRequiredService<IModuleRegistry>().Modules.Single();

        // Assert
        ((TrackingWebModule)module).RegisterEnvironmentName.ShouldBe(builder.Environment.EnvironmentName);
    }

    [Fact]
    public void UseModulesAndMapModules_WhenUsingWebApplication_PassEnvironmentToModules()
    {
        // Arrange
        var builder = DevKitWebApplication.CreateBuilder([])
            .AddModules(modules => modules.WithModule<TrackingWebModule>());
        using var app = builder.Build();

        // Act
        app.UseModules();
        app.MapModules();

        // Assert
        var module = (TrackingWebModule)app.Services.GetRequiredService<IModuleRegistry>().Modules.Single();
        module.UseEnvironmentName.ShouldBe(app.Environment.EnvironmentName);
        module.MapEnvironmentName.ShouldBe(app.Environment.EnvironmentName);
    }

    public sealed class TrackingWebModule : WebModuleBase
    {
        public string RegisterEnvironmentName { get; private set; }

        public string UseEnvironmentName { get; private set; }

        public string MapEnvironmentName { get; private set; }

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
