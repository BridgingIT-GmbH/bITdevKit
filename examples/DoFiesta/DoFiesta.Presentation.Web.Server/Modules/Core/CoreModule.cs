// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using Application.Modules.Core;
using Common;
using DevKit.Domain.Repositories;
using Domain.Model;
using FluentValidation;
using Infrastructure;

public class CoreModule : WebModuleBase
{
    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        var moduleConfiguration = this.Configure<CoreModuleConfiguration, CoreModuleConfiguration.Validator>(services, configuration);

        // jobs
        // services.AddJobScheduling()
        //     .WithScopedJob<EchoJob>(CronExpressions.Every5Minutes); // .WithSingletonJob<EchoJob>(CronExpressions.Every5Minutes)

        // dbcontext
        services.AddSqlServerDbContext<CoreDbContext>(o => o
                .UseConnectionString(moduleConfiguration.ConnectionStrings["Default"])
                .UseLogger()
                .UseSimpleLogger())
            .WithDatabaseCreatorService(o => o.DeleteOnStartup());

        services.AddInMemoryDbContext<CoreDbContext>()
            .WithDatabaseCreatorService(o => o
                //.Enabled(environment?.IsDevelopment() == true)
                .DeleteOnStartup(false));

        // permissions
        services.AddIdentity(o => // rename .AddAuthorization
        {
            o.WithEntityPermissions<CoreDbContext>(o =>
            {
                // Register entities that need permission checks + auth policies
                o.AddEntity<TodoItem>(Permission.Read, Permission.Write, Permission.List, Permission.Delete) // allowed permissions -> auth policies
                    .AddDefaultPermissions<TodoItem>(Permission.Read/*, Permission.Write*/, Permission.List) // default permissions if user/group has no grants
                    .UseDefaultPermissionProvider<TodoItem>();
            });

            o.EnableEvaluationEndpoints();
            o.EnableManagementEndpoints(c => c.RequireRoles = [Role.Administrators]);
        });

        // TodoItem repository
        services.AddEntityFrameworkRepository<TodoItem, CoreDbContext>()
            .WithBehavior<RepositoryTracingBehavior<TodoItem>>()
            .WithBehavior<RepositoryLoggingBehavior<TodoItem>>();

        services.AddEntityFrameworkRepository<Subscription, CoreDbContext>()
            .WithBehavior<RepositoryTracingBehavior<Subscription>>()
            .WithBehavior<RepositoryLoggingBehavior<Subscription>>();

        return services;
    }

    public override IApplicationBuilder Use(
        IApplicationBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        return app;
    }

    public override IEndpointRouteBuilder Map(
        IEndpointRouteBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        // TODO: map the city endpoints here

        return app;
    }
}