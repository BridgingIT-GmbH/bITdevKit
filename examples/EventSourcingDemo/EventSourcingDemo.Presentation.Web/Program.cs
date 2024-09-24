// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Presentation.Web;

using DevKit.Infrastructure.EntityFramework.EventSourcing;
using DevKit.Infrastructure.EntityFramework.EventSourcing.Models;
using Infrastructure.Repositories;
using Microsoft.AspNetCore;
using Microsoft.EntityFrameworkCore;

public class Program
{
    public static void Main(string[] args)
    {
        var host = CreateWebHostBuilder(args).Build();
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<EventStoreDbContext>();
            DbInitializer.InitializeEventStoreDbContext(context);

            var context2 = services.GetRequiredService<EventSourcingDemoDbContext>();
#if DEBUG
            context2.Database.Migrate();
#endif
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while seeding the database EventStore.");
        }

        host.Run();
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args)
    {
        return WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>();
    }
}