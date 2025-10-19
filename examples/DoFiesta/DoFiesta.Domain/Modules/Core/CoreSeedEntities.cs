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
        FakeUsers.Starwars[0].Id, // luke skywalker
        FakeUsers.Starwars[1].Id, // yoda
        FakeUsers.Starwars[2].Id, // obi wan
    ];

    public static class TodoItems
    {
        public static TodoItem[] Create()
        {
            return
            [
                new TodoItem
                {
                    Title = "Design Microservices Architecture",
                    Description = "Define service boundaries and communication patterns for new microservices architecture",
                    Category = "Architecture",
                    Status = TodoStatus.InProgress,
                    Priority = TodoPriority.Low,
                    DueDate = DateTime.UtcNow.AddDays(5),
                    UserId = UserIds[0],
                    Steps =
                    [
                        new TodoStep { Description = "Define domain boundaries", Status = TodoStatus.Completed },
                        new TodoStep { Description = "Design event schema", Status = TodoStatus.InProgress },
                        new TodoStep { Description = "Document service contracts", Status = TodoStatus.New }
                    ]
                },
                new TodoItem
                {
                    Title = "API Gateway Implementation",
                    Description = "Implement API Gateway using Ocelot with JWT authentication",
                    Category = "Infrastructure",
                    Status = TodoStatus.New,
                    Priority = TodoPriority.Medium,
                    DueDate = DateTime.UtcNow.AddDays(3),
                    UserId = UserIds[0],
                    Steps =
                    [
                        new TodoStep { Description = "Setup Ocelot configuration", Status = TodoStatus.New },
                        new TodoStep { Description = "Implement rate limiting", Status = TodoStatus.New },
                        new TodoStep { Description = "Add JWT validation", Status = TodoStatus.New }
                    ]
                },
                new TodoItem
                {
                    Title = "Domain Model Refactoring",
                    Description = "Refactor core domain models to implement DDD patterns",
                    Category = "Development",
                    Status = TodoStatus.InProgress,
                    Priority = TodoPriority.High,
                    DueDate = DateTime.UtcNow.AddDays(4),
                    UserId = UserIds[0],
                    Steps =
                    [
                        new TodoStep { Description = "Implement aggregates", Status = TodoStatus.Completed },
                        new TodoStep { Description = "Add value objects", Status = TodoStatus.InProgress },
                        new TodoStep { Description = "Update repositories", Status = TodoStatus.New }
                    ]
                },
                new TodoItem
                {
                   Title = "Elasticsearch Integration",
                   Description = "Implement search infrastructure using Elasticsearch and Kibana",
                   Category = "Infrastructure",
                   Status = TodoStatus.New,
                   Priority = TodoPriority.Low,
                   DueDate = DateTime.UtcNow.AddDays(5),
                   UserId = UserIds[0],
                   Steps =
                   [
                       new TodoStep { Description = "Configure ES cluster", Status = TodoStatus.New },
                       new TodoStep { Description = "Implement indexing", Status = TodoStatus.New },
                       new TodoStep { Description = "Add search queries", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Query Optimization",
                   Description = "Optimize EF Core queries and implement caching strategy",
                   Category = "Performance",
                   Status = TodoStatus.InProgress,
                   Priority = TodoPriority.High,
                   DueDate = DateTime.UtcNow.AddDays(2),
                   UserId = UserIds[0],
                   Steps =
                   [
                       new TodoStep { Description = "Profile slow queries", Status = TodoStatus.Completed },
                       new TodoStep { Description = "Implement Redis cache", Status = TodoStatus.InProgress },
                       new TodoStep { Description = "Add query hints", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Message Queue Setup",
                   Description = "Implement RabbitMQ for asynchronous processing",
                   Category = "Infrastructure",
                   Status = TodoStatus.New,
                   Priority = TodoPriority.Low,
                   DueDate = DateTime.UtcNow.AddDays(4),
                   UserId = UserIds[0],
                   Steps =
                   [
                       new TodoStep { Description = "Setup RabbitMQ cluster", Status = TodoStatus.New },
                       new TodoStep { Description = "Implement consumers", Status = TodoStatus.New },
                       new TodoStep { Description = "Add dead letter queues", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Authentication Service",
                   Description = "Implement OAuth2/OIDC authentication service with IdentityServer",
                   Category = "Security",
                   Status = TodoStatus.InProgress,
                   Priority = TodoPriority.High,
                   DueDate = DateTime.UtcNow.AddDays(3),
                   UserId = UserIds[1],
                   Steps =
                   [
                       new TodoStep { Description = "Configure OIDC flows", Status = TodoStatus.Completed },
                       new TodoStep { Description = "Add token validation", Status = TodoStatus.InProgress },
                       new TodoStep { Description = "Implement refresh logic", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "GraphQL Implementation",
                   Description = "Add GraphQL API layer using HotChocolate",
                   Category = "Development",
                   Status = TodoStatus.New,
                   Priority = TodoPriority.Medium,
                   DueDate = DateTime.UtcNow.AddDays(6),
                   UserId = UserIds[1],
                   Steps =
                   [
                       new TodoStep { Description = "Define schema", Status = TodoStatus.New },
                       new TodoStep { Description = "Add resolvers", Status = TodoStatus.New },
                       new TodoStep { Description = "Implement dataloader", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Kubernetes Deployment",
                   Description = "Setup K8s cluster with monitoring and autoscaling",
                   Category = "DevOps",
                   Status = TodoStatus.InProgress,
                   Priority = TodoPriority.High,
                   DueDate = DateTime.UtcNow.AddDays(4),
                   UserId = UserIds[1],
                   Steps =
                   [
                       new TodoStep { Description = "Configure AKS cluster", Status = TodoStatus.Completed },
                       new TodoStep { Description = "Setup Prometheus", Status = TodoStatus.InProgress },
                       new TodoStep { Description = "Implement HPA", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Observability Setup",
                   Description = "Implement distributed tracing using OpenTelemetry",
                   Category = "Monitoring",
                   Status = TodoStatus.New,
                   Priority = TodoPriority.High,
                   DueDate = DateTime.UtcNow.AddDays(5),
                   UserId = UserIds[1],
                   Steps =
                   [
                       new TodoStep { Description = "Setup Jaeger", Status = TodoStatus.New },
                       new TodoStep { Description = "Add trace contexts", Status = TodoStatus.New },
                       new TodoStep { Description = "Configure sampling", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "CI/CD Pipeline",
                   Description = "Implement GitOps workflow with Azure DevOps",
                   Category = "DevOps",
                   Status = TodoStatus.InProgress,
                   Priority = TodoPriority.Low,
                   DueDate = DateTime.UtcNow.AddDays(3),
                   UserId = UserIds[1],
                   Steps =
                   [
                       new TodoStep { Description = "Setup build agents", Status = TodoStatus.Completed },
                       new TodoStep { Description = "Configure ArgoCD", Status = TodoStatus.InProgress },
                       new TodoStep { Description = "Add smoke tests", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Event Store Setup",
                   Description = "Implement event sourcing using EventStoreDB",
                   Category = "Architecture",
                   Status = TodoStatus.New,
                   Priority = TodoPriority.High,
                   DueDate = DateTime.UtcNow.AddDays(7),
                   UserId = UserIds[2],
                   Steps =
                   [
                       new TodoStep { Description = "Setup event store", Status = TodoStatus.New },
                       new TodoStep { Description = "Implement projections", Status = TodoStatus.New },
                       new TodoStep { Description = "Add snapshots", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Load Testing",
                   Description = "Implement performance testing using k6 and Grafana",
                   Category = "Testing",
                   Status = TodoStatus.InProgress,
                   Priority = TodoPriority.Medium,
                   DueDate = DateTime.UtcNow.AddDays(5),
                   UserId = UserIds[2],
                   Steps =
                   [
                       new TodoStep { Description = "Write test scripts", Status = TodoStatus.Completed },
                       new TodoStep { Description = "Setup metrics", Status = TodoStatus.InProgress },
                       new TodoStep { Description = "Configure dashboards", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Security Hardening",
                   Description = "Implement security best practices and penetration testing",
                   Category = "Security",
                   Status = TodoStatus.New,
                   Priority = TodoPriority.High,
                   DueDate = DateTime.UtcNow.AddDays(5),
                   UserId = UserIds[2],
                   Steps =
                   [
                       new TodoStep { Description = "Setup WAF rules", Status = TodoStatus.New },
                       new TodoStep { Description = "Implement rate limiting", Status = TodoStatus.New },
                       new TodoStep { Description = "Add CSRF protection", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "CQRS Implementation",
                   Description = "Separate read and write operations using MediatR",
                   Category = "Architecture",
                   Status = TodoStatus.InProgress,
                   Priority = TodoPriority.Medium,
                   DueDate = DateTime.UtcNow.AddDays(6),
                   UserId = UserIds[2],
                   Steps =
                   [
                       new TodoStep { Description = "Create command handlers", Status = TodoStatus.Completed },
                       new TodoStep { Description = "Implement queries", Status = TodoStatus.InProgress },
                       new TodoStep { Description = "Add event handlers", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Service Mesh",
                   Description = "Implement Istio service mesh for microservices",
                   Category = "Infrastructure",
                   Status = TodoStatus.New,
                   Priority = TodoPriority.High,
                   DueDate = DateTime.UtcNow.AddDays(8),
                   UserId = UserIds[2],
                   Steps =
                   [
                       new TodoStep { Description = "Setup Istio gateway", Status = TodoStatus.New },
                       new TodoStep { Description = "Configure mTLS", Status = TodoStatus.New },
                       new TodoStep { Description = "Add traffic policies", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Database Sharding",
                   Description = "Implement database sharding for horizontal scaling",
                   Category = "Performance",
                   Status = TodoStatus.InProgress,
                   Priority = TodoPriority.High,
                   DueDate = DateTime.UtcNow.AddDays(7),
                   UserId = UserIds[2],
                   Steps =
                   [
                       new TodoStep { Description = "Design shard key", Status = TodoStatus.Completed },
                       new TodoStep { Description = "Implement router", Status = TodoStatus.InProgress },
                       new TodoStep { Description = "Add rebalancing", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "API Versioning",
                   Description = "Implement API versioning and documentation",
                   Category = "Development",
                   Status = TodoStatus.New,
                   Priority = TodoPriority.Medium,
                   DueDate = DateTime.UtcNow.AddDays(4),
                   UserId = UserIds[2],
                   Steps =
                   [
                       new TodoStep { Description = "Add version headers", Status = TodoStatus.New },
                       new TodoStep { Description = "Update Swagger", Status = TodoStatus.New },
                       new TodoStep { Description = "Create contracts", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Integration Testing",
                   Description = "Implement integration tests using TestContainers",
                   Category = "Testing",
                   Status = TodoStatus.InProgress,
                   Priority = TodoPriority.High,
                   DueDate = DateTime.UtcNow.AddDays(5),
                   UserId = UserIds[2],
                   Steps =
                   [
                       new TodoStep { Description = "Setup test containers", Status = TodoStatus.Completed },
                       new TodoStep { Description = "Write API tests", Status = TodoStatus.InProgress },
                       new TodoStep { Description = "Add data fixtures", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Caching Strategy",
                   Description = "Implement distributed caching with Redis",
                   Category = "Performance",
                   Status = TodoStatus.New,
                   Priority = TodoPriority.High,
                   DueDate = DateTime.UtcNow.AddDays(4),
                   UserId = UserIds[2],
                   Steps =
                   [
                       new TodoStep { Description = "Setup Redis cluster", Status = TodoStatus.New },
                       new TodoStep { Description = "Implement backplane", Status = TodoStatus.New },
                       new TodoStep { Description = "Add cache policies", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Domain Events",
                   Description = "Implement domain events using MediatR",
                   Category = "Architecture",
                   Status = TodoStatus.InProgress,
                   Priority = TodoPriority.Medium,
                   DueDate = DateTime.UtcNow.AddDays(6),
                   UserId = UserIds[1],
                   Steps =
                   [
                       new TodoStep { Description = "Create event handlers", Status = TodoStatus.Completed },
                       new TodoStep { Description = "Add notifications", Status = TodoStatus.InProgress },
                       new TodoStep { Description = "Implement outbox", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Feature Flags",
                   Description = "Implement feature toggles using LaunchDarkly",
                   Category = "Development",
                   Status = TodoStatus.New,
                   Priority = TodoPriority.Medium,
                   DueDate = DateTime.UtcNow.AddDays(3),
                   UserId = UserIds[1],
                   Steps =
                   [
                       new TodoStep { Description = "Setup SDK", Status = TodoStatus.New },
                       new TodoStep { Description = "Add flag contexts", Status = TodoStatus.New },
                       new TodoStep { Description = "Implement rollouts", Status = TodoStatus.New }
                   ]
                },
                new TodoItem
                {
                   Title = "Vault Integration",
                   Description = "Implement HashiCorp Vault for secrets management",
                   Category = "Security",
                   Status = TodoStatus.InProgress,
                   Priority = TodoPriority.High,
                   DueDate = DateTime.UtcNow.AddDays(4),
                   UserId = UserIds[1],
                   Steps =
                   [
                       new TodoStep { Description = "Setup Vault server", Status = TodoStatus.Completed },
                       new TodoStep { Description = "Configure policies", Status = TodoStatus.InProgress },
                       new TodoStep { Description = "Add key rotation", Status = TodoStatus.New }
                   ]
                }
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
                    StartDate = new DateTime(2024, 11, 1, 0, 0, 0),
                    EndDate = null
                },

                new Subscription
                {
                    UserId = UserIds[1],
                    Plan = SubscriptionPlan.Team,
                    Status = SubscriptionStatus.Active,
                    BillingCycle = SubscriptionBillingCycle.Yearly,
                    StartDate = new DateTime(2025, 1, 15, 0, 0, 0),
                    EndDate = null
                },

                new Subscription
                {
                    UserId = UserIds[2],
                    Plan = SubscriptionPlan.Basic,
                    Status = SubscriptionStatus.Active,
                    BillingCycle = SubscriptionBillingCycle.Monthly,
                    StartDate = new DateTime(2024, 12, 5, 0, 0, 0),
                    EndDate = null
                }
        ];
    }
}
}
