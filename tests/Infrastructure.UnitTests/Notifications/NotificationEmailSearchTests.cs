// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Notifications;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class NotificationEmailSearchTests
{
    [Fact]
    public async Task SearchMessagesAsync_OversizedPage_CapsSizeWithoutMutatingCallerModel()
    {
        using var provider = CreateProvider();
        var sut = new EntityFrameworkNotificationEmailOutboxService<EmailContext>(provider);
        var filter = new FilterModel { Page = 0, PageSize = 5000 };

        var result = await sut.SearchMessagesAsync(filter);

        result.IsSuccess.ShouldBeTrue();
        result.CurrentPage.ShouldBe(1);
        result.PageSize.ShouldBe(1000);
        result.TotalCount.ShouldBe(0);
        result.Value.ShouldBeEmpty();
        filter.Page.ShouldBe(0);
        filter.PageSize.ShouldBe(5000);
    }

    [Fact]
    public async Task SearchMessagesAsync_StatusAndPaging_ReturnsFilteredCountAndPage()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmailContext>();
        context.NotificationsEmails.AddRange(Enumerable.Range(1, 5).Select(index => new EmailMessageEntity
        {
            Id = Guid.NewGuid(),
            Subject = $"Invoice {index}",
            Body = "Body",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(index),
            Status = index == 1 ? EmailMessageStatus.Sent : EmailMessageStatus.Pending,
            IsArchived = index == 5
        }));
        await context.SaveChangesAsync();
        var sut = new EntityFrameworkNotificationEmailOutboxService<EmailContext>(provider);
        var filter = new FilterModel
        {
            Page = 2,
            PageSize = 1,
            Filters = [new("Status", FilterOperator.Equal, EmailMessageStatus.Pending)],
            Orderings = [new() { Field = "Subject" }]
        };

        var result = await sut.SearchMessagesAsync(filter);

        result.TotalCount.ShouldBe(3);
        result.Value.Single().Subject.ShouldBe("Invoice 3");
        result.CurrentPage.ShouldBe(2);
        var allArchives = await sut.SearchMessagesAsync(filter, isArchived: null);
        allArchives.TotalCount.ShouldBe(4);
    }

    [Theory]
    [InlineData("Body")]
    [InlineData("PropertiesJson")]
    public async Task SearchMessagesAsync_ContentField_RejectsFilter(string field)
    {
        using var provider = CreateProvider();
        var sut = new EntityFrameworkNotificationEmailOutboxService<EmailContext>(provider);
        var filter = new FilterModel { Filters = [new(field, FilterOperator.Contains, "secret")] };

        await Should.ThrowAsync<ArgumentException>(() => sut.SearchMessagesAsync(filter));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<EmailContext>(options => options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider();
    }

    private sealed class EmailContext(DbContextOptions<EmailContext> options) : DbContext(options), INotificationEmailContext
    {
        public DbSet<EmailMessageEntity> NotificationsEmails { get; set; }

        public DbSet<EmailMessageAttachmentEntity> NotificationsEmailAttachments { get; set; }
    }
}
