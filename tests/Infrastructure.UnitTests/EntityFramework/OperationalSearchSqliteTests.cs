// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;
using BridgingIT.DevKit.Infrastructure.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class OperationalSearchSqliteTests
{
    /// <summary>Seek paging retains boundaries across equal timestamps, arrivals, deletion, and status changes.</summary>
    /// <example><code>await suite.SearchMessagesAsync_SeekPaging_PreservesRemainingRows(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SearchMessagesAsync_SeekPaging_PreservesRemainingRows(bool queueing)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var provider = CreateProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SearchContext>();
        await context.Database.EnsureCreatedAsync();
        var timestamp = DateTimeOffset.Parse("2026-11-04T12:00:00Z");
        for (var index = 1; index <= 35; index++)
        {
            context.BrokerMessages.Add(new BrokerMessage { Id = RowId(index), MessageId = $"seek-{index}", Type = "Test", Content = "{}", CreatedDate = timestamp, Status = BrokerMessageStatus.Pending });
            context.QueueMessages.Add(new QueueMessage { Id = RowId(index), MessageId = $"seek-{index}", QueueName = "test", Type = "Test", Content = "{}", CreatedDate = timestamp, Status = QueueMessageStatus.Pending });
        }

        await context.SaveChangesAsync();
        var messages = new EntityFrameworkMessageBrokerStoreService<SearchContext>(provider, new MessageBrokerControlState());
        var queues = new EntityFrameworkQueueBrokerService<SearchContext>(context, new(), new(), new());
        var pending = queueing ? (object)QueueMessageStatus.Pending : BrokerMessageStatus.Pending;
        var seen = new List<Guid>();
        Guid? cursor = null;
        for (var batch = 0; batch < 4; batch++)
        {
            var filter = new FilterModel
            {
                Page = 1, PageSize = 10,
                Orderings = [new() { Field = "Id", Direction = OrderDirection.Descending }],
                Filters = [new("Status", FilterOperator.Equal, pending), new("Id", FilterOperator.LessThanOrEqual, RowId(35))]
            };
            if (cursor.HasValue) filter.Filters.Add(new("Id", FilterOperator.LessThan, cursor.Value));
            var ids = queueing
                ? (await queues.SearchMessagesAsync(filter)).Value.Select(row => row.Id).ToArray()
                : (await messages.SearchMessagesAsync(filter)).Value.Select(row => row.Id).ToArray();
            seen.AddRange(ids);
            cursor = ids.LastOrDefault();
            if (batch != 0) continue;
            context.BrokerMessages.Remove(await context.BrokerMessages.SingleAsync(row => row.Id == RowId(34)));
            context.QueueMessages.Remove(await context.QueueMessages.SingleAsync(row => row.Id == RowId(34)));
            (await context.BrokerMessages.SingleAsync(row => row.Id == RowId(25))).Status = BrokerMessageStatus.Succeeded;
            (await context.QueueMessages.SingleAsync(row => row.Id == RowId(25))).Status = QueueMessageStatus.Succeeded;
            context.BrokerMessages.Add(new BrokerMessage { Id = RowId(100), MessageId = "arrival", Type = "Test", Content = "{}", CreatedDate = timestamp, Status = BrokerMessageStatus.Pending });
            context.QueueMessages.Add(new QueueMessage { Id = RowId(100), MessageId = "arrival", QueueName = "test", Type = "Test", Content = "{}", CreatedDate = timestamp, Status = QueueMessageStatus.Pending });
            await context.SaveChangesAsync();
        }

        seen.Count.ShouldBe(34);
        seen.Distinct().Count().ShouldBe(seen.Count);
        seen.ShouldBe(Enumerable.Range(1, 35).Where(index => index != 25).Reverse().Select(RowId));
    }

    [Fact]
    public async Task SearchBrokerMessagesAsync_RelationalQuery_FiltersBeforeCountAndPage()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var provider = CreateProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SearchContext>();
        await context.Database.EnsureCreatedAsync();
        context.BrokerMessages.AddRange(Enumerable.Range(1, 6).Select(index => new BrokerMessage
        {
            Id = RowId(index),
            MessageId = $"relational-{index}",
            Type = index == 6 ? "OtherMessage" : "InvoiceMessage",
            Content = "{}",
            CreatedDate = DateTimeOffset.UtcNow,
            Status = index == 1 ? BrokerMessageStatus.Succeeded : BrokerMessageStatus.Pending,
            IsArchived = index == 5
        }));
        await context.SaveChangesAsync();
        var service = new EntityFrameworkMessageBrokerStoreService<SearchContext>(provider, new MessageBrokerControlState());
        var filter = new FilterModel
        {
            Page = 2,
            PageSize = 1,
            Filters =
            [
                new("Status", FilterOperator.Equal, BrokerMessageStatus.Pending),
                new("Type", FilterOperator.StartsWith, "Invoice")
            ],
            Orderings = [new() { Field = "Id" }]
        };

        var result = await service.SearchMessagesAsync(filter);

        result.TotalCount.ShouldBe(3);
        result.CurrentPage.ShouldBe(2);
        result.Value.Single().Id.ShouldBe(RowId(3));
        var includingArchived = await service.SearchMessagesAsync(filter, isArchived: null);
        includingArchived.TotalCount.ShouldBe(4);
    }

    [Fact]
    public async Task SearchNotificationEmailsAsync_RelationalQuery_FiltersBeforeCountAndPage()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var provider = CreateProvider(connection);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SearchContext>();
        await context.Database.EnsureCreatedAsync();
        context.NotificationsEmails.AddRange(Enumerable.Range(1, 6).Select(index => new EmailMessageEntity
        {
            Id = RowId(index),
            Subject = index == 6 ? "Other message" : $"Invoice {index}",
            Body = "Body",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = index == 1 ? EmailMessageStatus.Sent : EmailMessageStatus.Pending,
            IsArchived = index == 5
        }));
        await context.SaveChangesAsync();
        var service = new EntityFrameworkNotificationEmailOutboxService<SearchContext>(provider);
        var filter = new FilterModel
        {
            Page = 2,
            PageSize = 1,
            Filters =
            [
                new("Status", FilterOperator.Equal, EmailMessageStatus.Pending),
                new("Subject", FilterOperator.StartsWith, "Invoice")
            ],
            Orderings = [new() { Field = "Id" }]
        };

        var result = await service.SearchMessagesAsync(filter);

        result.TotalCount.ShouldBe(3);
        result.CurrentPage.ShouldBe(2);
        result.Value.Single().Id.ShouldBe(RowId(3));
        var includingArchived = await service.SearchMessagesAsync(filter, isArchived: null);
        includingArchived.TotalCount.ShouldBe(4);
    }

    private static Guid RowId(int index) => new(index, 0, 0, new byte[8]);

    private static ServiceProvider CreateProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddDbContext<SearchContext>(options => options.UseSqlite(connection));
        return services.BuildServiceProvider();
    }

    private sealed class SearchContext(DbContextOptions<SearchContext> options) : DbContext(options), IMessagingContext, INotificationEmailContext, IQueueingContext
    {
        public DbSet<BrokerMessage> BrokerMessages { get; set; }

        public DbSet<QueueMessage> QueueMessages { get; set; }

        public DbSet<EmailMessageEntity> NotificationsEmails { get; set; }

        public DbSet<EmailMessageAttachmentEntity> NotificationsEmailAttachments { get; set; }
    }
}
