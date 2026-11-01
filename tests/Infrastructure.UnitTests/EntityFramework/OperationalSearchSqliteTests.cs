// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;
using BridgingIT.DevKit.Infrastructure.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class OperationalSearchSqliteTests
{
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

    private sealed class SearchContext(DbContextOptions<SearchContext> options) : DbContext(options), IMessagingContext, INotificationEmailContext
    {
        public DbSet<BrokerMessage> BrokerMessages { get; set; }

        public DbSet<EmailMessageEntity> NotificationsEmails { get; set; }

        public DbSet<EmailMessageAttachmentEntity> NotificationsEmailAttachments { get; set; }
    }
}
