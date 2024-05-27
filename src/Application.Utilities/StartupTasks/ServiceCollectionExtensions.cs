// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    private static StartupTaskServiceOptions contextOptions;

    public static StartupTasksBuilderContext AddStartupTasks(
        this IServiceCollection services)
    {
        return services.AddStartupTasks(options: null);
    }

    public static StartupTasksBuilderContext AddStartupTasks(
        this IServiceCollection services,
        Builder<StartupTaskServiceOptionsBuilder, StartupTaskServiceOptions> optionsBuilder)
    {
        return services
            .AddStartupTasks(
                optionsBuilder(new StartupTaskServiceOptionsBuilder()).Build());
    }

    public static StartupTasksBuilderContext AddStartupTasks(
        this IServiceCollection services,
        StartupTaskServiceOptions options)
    {
        contextOptions ??= options;

        if (contextOptions is not null)
        {
            services.TryAddSingleton(contextOptions);
        }

        services.AddHostedService<StartupTasksService>();

        return new StartupTasksBuilderContext(services);
    }

    public static StartupTasksBuilderContext WithTask<TTask>(
        this StartupTasksBuilderContext context)
        where TTask : class, IStartupTask
    {
        return context.WithTask<TTask>(new StartupTaskOptions());
    }

    public static StartupTasksBuilderContext WithTask<TTask>(
        this StartupTasksBuilderContext context,
        Builder<StartupTaskOptionsBuilder, StartupTaskOptions> optionsBuilder)
        where TTask : class, IStartupTask
    {
        return context.WithTask<TTask>(optionsBuilder(new StartupTaskOptionsBuilder()).Build());
    }

    public static StartupTasksBuilderContext WithTask<TTask>(
        this StartupTasksBuilderContext context,
        StartupTaskOptions options)
        where TTask : class, IStartupTask
    {
        context.Services.AddSingleton(sp =>
            new StartupTaskDefinition
            {
                TaskType = typeof(TTask),
                Options = options ?? new StartupTaskOptions()
            });
        context.Services.AddScoped<TTask>();

        return context;
    }

    public static StartupTasksBuilderContext WithTask(
        this StartupTasksBuilderContext context,
        Func<IServiceProvider, IStartupTask> implementationFactory)
    {
        return context.WithTask(implementationFactory, new StartupTaskOptions());
    }

    public static StartupTasksBuilderContext WithTask(
        this StartupTasksBuilderContext context,
        Func<IServiceProvider, IStartupTask> implementationFactory,
        Builder<StartupTaskOptionsBuilder, StartupTaskOptions> optionsBuilder)
    {
        return context.WithTask(implementationFactory, optionsBuilder(new StartupTaskOptionsBuilder()).Build());
    }

    public static StartupTasksBuilderContext WithTask(
        this StartupTasksBuilderContext context,
        Func<IServiceProvider, IStartupTask> implementationFactory,
        StartupTaskOptions options)
    {
        if (implementationFactory is not null)
        {
            // Temporarily create an instance to infer the type
            using var serviceProvider = context.Services.BuildServiceProvider();
            var instance = implementationFactory(serviceProvider);
            var implementationType = instance.GetType();

            context.Services.AddSingleton(sp =>
                new StartupTaskDefinition
                {
                    TaskType = implementationType,
                    Options = options ?? new StartupTaskOptions()
                });
            context.Services.AddScoped(implementationType, implementationFactory);
        }

        return context;
    }

    public static StartupTasksBuilderContext WithBehavior<TBehavior>(
        this StartupTasksBuilderContext context,
        IStartupTaskBehavior behavior = null)
        where TBehavior : class, IStartupTaskBehavior
    {
        if (behavior is null)
        {
            context.Services.AddSingleton<IStartupTaskBehavior, TBehavior>();
        }
        else
        {
            context.Services.AddSingleton(typeof(IStartupTaskBehavior), behavior);
        }

        return context;
    }

    public static StartupTasksBuilderContext WithBehavior(
        this StartupTasksBuilderContext context,
        Func<IServiceProvider, IStartupTaskBehavior> implementationFactory)
    {
        if (implementationFactory is not null)
        {
            context.Services.AddSingleton(typeof(IStartupTaskBehavior), implementationFactory);
        }

        return context;
    }

    public static StartupTasksBuilderContext WithBehavior(
        this StartupTasksBuilderContext context,
        IStartupTaskBehavior behavior)
    {
        if (behavior is not null)
        {
            context.Services.AddSingleton(typeof(IStartupTaskBehavior), behavior);
        }

        return context;
    }

    private static Type GetImplementationType(Func<IServiceProvider, IStartupTask> factory)
    {
        // Create an expression representing the delegate
        Expression<Func<IServiceProvider, IStartupTask>> expression = sp => factory(sp);

        // Extract the body of the expression
        if (expression.Body is MethodCallExpression methodCall)
        {
            // Handle the case where the body is a method call
            if (methodCall.Method.ReturnType != typeof(IStartupTask))
            {
                throw new InvalidOperationException("The delegate does not return IStartupTask.");
            }

            // Analyze the method call to get the return type
            return methodCall.Method.ReturnType;
        }
        else if (expression.Body is NewExpression newExpression)
        {
            // Handle the case where the body is a new expression
            return newExpression.Type;
        }
        else if (expression.Body is MemberInitExpression memberInitExpression)
        {
            // Handle the case where the body is a member initialization expression
            return memberInitExpression.NewExpression.Type;
        }
        else
        {
            throw new InvalidOperationException("Unable to determine the implementation type.");
        }
    }
}