// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using BridgingIT.DevKit.Common.Options;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using BridgingIT.DevKit.Examples.EventSourcingDemo.Infrastructure.Repositories;
using BridgingIT.DevKit.Examples.EventSourcingDemo.Presentation.Web;
using BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddModule(builder.Configuration);

builder.Services.AddLogging(logging =>
{
    logging.AddFilter("Microsoft", LogLevel.Warning)
           .AddFilter("System", LogLevel.Warning)
           .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
           .AddConsole();
});

builder.Services.AddTransient<ILoggerOptions>(sp =>
    new LoggerOptionsBuilder()
        .LoggerFactory(LoggerFactory.Create(logging =>
        {
            logging.AddFilter("Microsoft", LogLevel.Warning)
                   .AddFilter("System", LogLevel.Warning)
                   .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                   .AddConsole();
        }))
        .Build());

// Framework (ASP.NET Core)
builder.Services.AddControllers()
    .AddControllersAsServices(); // Replaces AddMvc with EnableEndpointRouting = false

builder.Services.AddOpenApiDocument(settings =>
{
    settings.DocumentName = "v1";
    settings.Version = "v1";
    settings.Title = "bITdevKit: Backend API Event Sourcing Demo";
});

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();
app.UseOpenApi();
app.UseSwaggerUi();

// Database initialization
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var eventStoreContext = services.GetRequiredService<EventStoreDbContext>();
        DbInitializer.InitializeEventStoreDbContext(eventStoreContext);

        var demoContext = services.GetRequiredService<EventSourcingDemoDbContext>();
#if DEBUG
        demoContext.Database.Migrate();
#endif
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database EventStore.");
    }
}

// Register aggregates and events
var registration = app.Services.GetRequiredService<IRegistrationForEventStoreAggregatesAndEvents>();
registration.RegisterAggregatesAndEvents();

app.Run();