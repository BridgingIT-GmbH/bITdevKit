// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing;

using Application.Auditing;
using Application.Messaging;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Messaging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class AuditOutboxTrailTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SerializedOutbox_AuthenticatesOriginalAttributionAndOwnsDeferredTransport(bool tamper)
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-outbox-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            var actor = Substitute.For<ICurrentUserAccessor>();
            actor.IsAuthenticated.Returns(true);
            actor.UserId.Returns("alice");
            actor.UserName.Returns("Alice");
            services.AddSingleton(actor);
            services.AddAuditing();
            services.AddDbContext<CustomerOutboxContext>(options => options.UseSqlite($"Data Source={path};Pooling=False"));
            var options = new OutboxMessageOptions { AutoSave = true, ProcessingMode = OutboxMessageProcessingMode.Interval, Serializer = new SystemTextJsonSerializer() };
            new MessagingBuilderContext(services).WithInProcessBroker(new InProcessMessageBrokerConfiguration { ProcessDelay = 0 }).WithAuditing()
                .WithBehavior(provider => new OutboxMessagePublisherBehavior<CustomerOutboxContext>(provider.GetRequiredService<ILoggerFactory>(), provider, options: options));
            await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            await using (var schema = host.CreateAsyncScope()) await schema.ServiceProvider.GetRequiredService<CustomerOutboxContext>().Database.EnsureCreatedAsync();
            var broker = host.GetRequiredService<IMessageBrokerRuntime>();
            await broker.Subscribe<CustomerNotification, CustomerNotificationHandler>();
            var message = new CustomerNotification();
            Guid senderId;
            await using (var caller = host.CreateAsyncScope())
            {
                var root = caller.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.notify"));
                senderId = root.AuditId;
                await broker.Publish(message);
                root.Complete();
                await root.DisposeAsync();
            }

            var sender = (await Records(host)).ShouldHaveSingleItem();
            sender.AuditId.ShouldBe(senderId);
            var dispatchId = sender.Actions.ShouldHaveSingleItem().Id;
            await using (var persisted = host.CreateAsyncScope())
            {
                var context = persisted.ServiceProvider.GetRequiredService<CustomerOutboxContext>();
                var row = await context.OutboxMessages.SingleAsync();
                row.ProcessedDate.ShouldBeNull();
                var token = AuditHandoffMetadata.Read(row.Properties);
                token.ShouldNotBeNullOrWhiteSpace();
                var link = host.GetRequiredService<IAuditExecutionLinkProtector>().Unprotect(token, "Messaging", row.MessageId);
                link.CausationAuditId.ShouldBe(senderId);
                link.CausationActionId.ShouldBe(dispatchId);
                if (tamper)
                {
                    row.Properties[AuditHandoffMetadata.PropertyName] = "forged-token";
                    await context.SaveChangesAsync();
                }
            }

            var worker = new OutboxMessageWorker<CustomerOutboxContext>(host.GetRequiredService<ILoggerFactory>(), host, broker, options);
            await worker.ProcessAsync(message.MessageId);
            await host.GetRequiredService<AuditRuntime>().WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
            var records = await Records(host);
            records.Length.ShouldBe(3);
            var transport = records.Single(record => record.Kind == "MessageTransport");
            var consumer = records.Single(record => record.Kind == "MessageConsumer");
            transport.Actor.Kind.ShouldBe("Service");
            consumer.Actor.Kind.ShouldBe("Service");
            transport.Actions.ShouldHaveSingleItem().ExecutionStatus.ShouldBe("TransportAccepted");
            consumer.Actions.ShouldHaveSingleItem().ExecutionStatus.ShouldBe("Invoked");
            if (tamper)
            {
                transport.CausationAuditId.ShouldBeNull();
                transport.OriginAuditId.ShouldBe(transport.AuditId);
                consumer.OriginAuditId.ShouldNotBe(senderId);
                consumer.InitiatingActor?.UserId.ShouldNotBe("alice");
            }
            else
            {
                transport.OriginAuditId.ShouldBe(senderId);
                transport.CausationActionId.ShouldBe(dispatchId);
                consumer.OriginAuditId.ShouldBe(senderId);
                consumer.CausationActionId.ShouldBe(dispatchId);
                transport.InitiatingActor.UserId.ShouldBe("alice");
                consumer.InitiatingActor.UserId.ShouldBe("alice");
            }

            await using var verification = host.CreateAsyncScope();
            (await verification.ServiceProvider.GetRequiredService<CustomerOutboxContext>().OutboxMessages.SingleAsync()).ProcessedDate.ShouldNotBeNull();
            foreach (var record in records) System.Text.Encoding.UTF8.GetString(AuditRecordCodec.Encode(record)).ShouldNotContain("private-payload-secret");
        }
        finally { File.Delete(path); }
    }
    private static async Task<AuditRecord[]> Records(ServiceProvider host)
    {
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var selected = host.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        return await Task.WhenAll((await provider.SearchAsync(selected)).Value.Select(async item => (await provider.FindAsync(item.AuditId)).Value));
    }
    public sealed class CustomerOutboxContext(DbContextOptions<CustomerOutboxContext> options) : DbContext(options), IOutboxMessageContext
    {
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OutboxMessage>().Property(row => row.CreatedDate).HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
        }
    }
    public sealed class CustomerNotification : MessageBase
    {
        public string Body { get; set; } = "private-payload-secret";
    }
    public sealed class CustomerNotificationHandler : IMessageHandler<CustomerNotification>
    {
        public Task Handle(CustomerNotification message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
