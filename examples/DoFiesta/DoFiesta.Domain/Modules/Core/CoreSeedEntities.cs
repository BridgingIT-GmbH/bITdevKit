// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

using BridgingIT.DevKit.Common;

public static class CoreSeedEntities
{
    private static readonly string[] UserIds =
    [
        Fakes.UsersStarwars[0].Id, // luke skywalker
        Fakes.UsersStarwars[1].Id, // ypda
        Fakes.UsersStarwars[2].Id, // obi wan
    ];

    public static class TodoItems
    {
        public static TodoItem[] Create()
        {
            return
            [
                // User 1 todos
                new TodoItem
            {
                Title = "Complete Project Proposal",
                Description = "Draft and finalize the Q2 project proposal",
                Status = TodoStatus.InProgress,
                Priority = TodoPriority.High,
                DueDate = DateTimeOffset.UtcNow.AddDays(5),
                UserId = UserIds[0],
                Assignee = EmailAddress.Create("john.doe@example.com"),
                Steps =
                [
                    new TodoStep { Description = "Research market trends", Status = TodoStatus.Completed },
                    new TodoStep { Description = "Create initial draft", Status = TodoStatus.InProgress },
                    new TodoStep { Description = "Review with stakeholders", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "Setup Development Environment",
                Description = "Configure local development setup for new project",
                Status = TodoStatus.New,
                Priority = TodoPriority.Medium,
                DueDate = DateTimeOffset.UtcNow.AddDays(2),
                UserId = UserIds[0],
                Steps =
                [
                    new TodoStep { Description = "Install required software", Status = TodoStatus.New },
                    new TodoStep { Description = "Configure IDE", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "Code Review Sprint 5",
                Description = "Review code changes for sprint 5",
                Status = TodoStatus.New,
                Priority = TodoPriority.High,
                DueDate = DateTimeOffset.UtcNow.AddDays(1),
                UserId = UserIds[0],
                Steps =
                [
                    new TodoStep { Description = "Review authentication module", Status = TodoStatus.New },
                    new TodoStep { Description = "Review API endpoints", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "Prepare Technical Documentation",
                Description = "Create technical documentation for new features",
                Status = TodoStatus.InProgress,
                Priority = TodoPriority.Medium,
                DueDate = DateTimeOffset.UtcNow.AddDays(3),
                UserId = UserIds[0],
                Steps =
                [
                    new TodoStep { Description = "Document API changes", Status = TodoStatus.InProgress },
                    new TodoStep { Description = "Update sequence diagrams", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "Security Audit",
                Description = "Conduct security audit of the application",
                Status = TodoStatus.New,
                Priority = TodoPriority.Critical,
                DueDate = DateTimeOffset.UtcNow.AddDays(4),
                UserId = UserIds[0],
                Assignee = EmailAddress.Create("security.team@example.com"),
                Steps =
                [
                    new TodoStep { Description = "Review security configurations", Status = TodoStatus.New },
                    new TodoStep { Description = "Conduct penetration testing", Status = TodoStatus.New }
                ]
            },

            // User 2 todos
            new TodoItem
            {
                Title = "Refactor Authentication Service",
                Description = "Implement new JWT handling and refresh token logic",
                Status = TodoStatus.InProgress,
                Priority = TodoPriority.High,
                DueDate = DateTimeOffset.UtcNow.AddDays(3),
                UserId = UserIds[1],
                Assignee = EmailAddress.Create("alice.smith@example.com"),
                Steps =
                [
                    new TodoStep { Description = "Update token generation", Status = TodoStatus.Completed },
                    new TodoStep { Description = "Implement refresh token rotation", Status = TodoStatus.InProgress },
                    new TodoStep { Description = "Add unit tests", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "Optimize Database Queries",
                Description = "Profile and optimize slow-running queries in the user management module",
                Status = TodoStatus.New,
                Priority = TodoPriority.Critical,
                DueDate = DateTimeOffset.UtcNow.AddDays(1),
                UserId = UserIds[1],
                Steps =
                [
                    new TodoStep { Description = "Run query performance analysis", Status = TodoStatus.New },
                    new TodoStep { Description = "Implement query caching", Status = TodoStatus.New },
                    new TodoStep { Description = "Add appropriate indices", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "Implement CI/CD Pipeline",
                Description = "Setup automated build and deployment pipeline using Azure DevOps",
                Status = TodoStatus.InProgress,
                Priority = TodoPriority.High,
                DueDate = DateTimeOffset.UtcNow.AddDays(5),
                UserId = UserIds[1],
                Steps =
                [
                    new TodoStep { Description = "Configure build agents", Status = TodoStatus.Completed },
                    new TodoStep { Description = "Setup test automation", Status = TodoStatus.InProgress },
                    new TodoStep { Description = "Configure deployment stages", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "Migrate to .NET 8",
                Description = "Upgrade project from .NET 6 to .NET 8 including all dependencies",
                Status = TodoStatus.New,
                Priority = TodoPriority.Medium,
                DueDate = DateTimeOffset.UtcNow.AddDays(7),
                UserId = UserIds[1],
                Steps =
                [
                    new TodoStep { Description = "Update project files", Status = TodoStatus.New },
                    new TodoStep { Description = "Update NuGet packages", Status = TodoStatus.New },
                    new TodoStep { Description = "Fix breaking changes", Status = TodoStatus.New },
                    new TodoStep { Description = "Run integration tests", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "Implement Domain Events",
                Description = "Add domain event handling for better service decoupling",
                Status = TodoStatus.New,
                Priority = TodoPriority.Medium,
                DueDate = DateTimeOffset.UtcNow.AddDays(4),
                UserId = UserIds[1],
                Assignee = EmailAddress.Create("alice.smith@example.com"),
                Steps =
                [
                    new TodoStep { Description = "Design event structure", Status = TodoStatus.New },
                    new TodoStep { Description = "Implement event dispatcher", Status = TodoStatus.New },
                    new TodoStep { Description = "Add event handlers", Status = TodoStatus.New },
                    new TodoStep { Description = "Write integration tests", Status = TodoStatus.New }
                ]
            },

            // User 3 todos
            new TodoItem
            {
                Title = "Setup Kubernetes Cluster",
                Description = "Configure and deploy production-grade K8s cluster",
                Status = TodoStatus.InProgress,
                Priority = TodoPriority.Critical,
                DueDate = DateTimeOffset.UtcNow.AddDays(2),
                UserId = UserIds[2],
                Assignee = EmailAddress.Create("devops.lead@example.com"),
                Steps =
                [
                    new TodoStep { Description = "Setup master nodes", Status = TodoStatus.Completed },
                    new TodoStep { Description = "Configure worker nodes", Status = TodoStatus.InProgress },
                    new TodoStep { Description = "Setup monitoring", Status = TodoStatus.New },
                    new TodoStep { Description = "Configure auto-scaling", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "Implement CQRS Pattern",
                Description = "Separate read and write operations for better scalability",
                Status = TodoStatus.New,
                Priority = TodoPriority.High,
                DueDate = DateTimeOffset.UtcNow.AddDays(6),
                UserId = UserIds[2],
                Steps =
                [
                    new TodoStep { Description = "Design command handlers", Status = TodoStatus.New },
                    new TodoStep { Description = "Implement query handlers", Status = TodoStatus.New },
                    new TodoStep { Description = "Setup event sourcing", Status = TodoStatus.New },
                    new TodoStep { Description = "Implement read models", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "API Performance Optimization",
                Description = "Improve API response times and implement caching",
                Status = TodoStatus.InProgress,
                Priority = TodoPriority.High,
                DueDate = DateTimeOffset.UtcNow.AddDays(4),
                UserId = UserIds[2],
                Steps =
                [
                    new TodoStep { Description = "Implement Redis caching", Status = TodoStatus.Completed },
                    new TodoStep { Description = "Add response compression", Status = TodoStatus.InProgress },
                    new TodoStep { Description = "Optimize database queries", Status = TodoStatus.New },
                    new TodoStep { Description = "Load test optimization", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "Implement Event Sourcing",
                Description = "Add event sourcing for critical business operations",
                Status = TodoStatus.New,
                Priority = TodoPriority.Medium,
                DueDate = DateTimeOffset.UtcNow.AddDays(8),
                UserId = UserIds[2],
                Assignee = EmailAddress.Create("architect@example.com"),
                Steps =
                [
                    new TodoStep { Description = "Design event store schema", Status = TodoStatus.New },
                    new TodoStep { Description = "Implement event store", Status = TodoStatus.New },
                    new TodoStep { Description = "Add event replay functionality", Status = TodoStatus.New },
                    new TodoStep { Description = "Create snapshot mechanism", Status = TodoStatus.New }
                ]
            },
            new TodoItem
            {
                Title = "GraphQL Integration",
                Description = "Add GraphQL support to existing REST APIs",
                Status = TodoStatus.New,
                Priority = TodoPriority.Medium,
                DueDate = DateTimeOffset.UtcNow.AddDays(5),
                UserId = UserIds[2],
                Steps =
                [
                    new TodoStep { Description = "Setup Hot Chocolate", Status = TodoStatus.New },
                    new TodoStep { Description = "Define GraphQL schema", Status = TodoStatus.New },
                    new TodoStep { Description = "Implement resolvers", Status = TodoStatus.New },
                    new TodoStep { Description = "Add GraphQL subscriptions", Status = TodoStatus.New }
                ]
            },
        ];
        }
    }

    public static class Subscriptions
    {
        public static Subscription[] Create()
        {
            return
            [
                new Subscription
                {
                    UserId = UserIds[0],
                    Plan = SubscriptionPlan.Enterprise,
                    Status = SubscriptionStatus.Active,
                    BillingCycle = SubscriptionBillingCycle.Monthly,
                    StartDate = new DateTimeOffset(2024, 11, 1, 0, 0, 0, TimeSpan.Zero),
                    EndDate = null
                },

                new Subscription
                {
                    UserId = UserIds[1],
                    Plan = SubscriptionPlan.Team,
                    Status = SubscriptionStatus.Active,
                    BillingCycle = SubscriptionBillingCycle.Yearly,
                    StartDate = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero),
                    EndDate = null
                },

                new Subscription
                {
                    UserId = UserIds[2],
                    Plan = SubscriptionPlan.Basic,
                    Status = SubscriptionStatus.Active,
                    BillingCycle = SubscriptionBillingCycle.Monthly,
                    StartDate = new DateTimeOffset(2024, 12, 5, 0, 0, 0, TimeSpan.Zero),
                    EndDate = null
                }
            ];
        }
    }
}
