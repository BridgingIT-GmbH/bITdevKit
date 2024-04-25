// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Presentation.Web;

using BridgingIT.DevKit.Common.Options;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSwag.Generation.AspNetCore;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        this.Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddModule(this.Configuration);

        void Action(ILoggingBuilder builder)
        {
            builder.AddFilter("Microsoft", LogLevel.Warning).AddFilter("System", LogLevel.Warning).AddFilter("LoggingConsoleApp.Program", LogLevel.Debug).AddConsole();
        }

        // ReSharper disable once CA2000
        services.AddTransient<ILoggerOptions>(sp =>
            new LoggerOptionsBuilder()
                .LoggerFactory(LoggerFactory.Create(Action)).Build());

        // framework (aspnet)
        services.AddMvc(option => option.EnableEndpointRouting = false);
        services.AddOpenApiDocument(this.ConfigureOpenApiDocument);
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env,
        IRegistrationForEventStoreAggregatesAndEvents registrationForEventStoreAggregatesAndEvents)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseMvc();
        app.UseOpenApi();
        app.UseSwaggerUi();
        registrationForEventStoreAggregatesAndEvents.RegisterAggregatesAndEvents();
    }

    public void ConfigureOpenApiDocument(AspNetCoreOpenApiDocumentGeneratorSettings settings)
    {
        settings.DocumentName = "v1";
        settings.Version = "v1";
        settings.Title = "bITdevKit: Backend API Event Sourcing Demo";
    }
}