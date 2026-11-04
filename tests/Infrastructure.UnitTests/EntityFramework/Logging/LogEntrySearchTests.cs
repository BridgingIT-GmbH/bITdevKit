// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Logging;

using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

public class LogEntrySearchTests
{
    [Fact]
    public async Task SearchAsync_FilteredPage_ExcludesArchivesAndPayloadsWhilePreservingTotal()
    {
        await using var context = CreateContext();
        context.LogEntries.AddRange(
            new LogEntry { Id = 1, Message = "first", Level = "Error", IsArchived = null, LogEventsJson = "{\"private\":true}" },
            new LogEntry { Id = 2, Message = "second", Level = "Error", IsArchived = false, LogEventsJson = "{\"private\":true}" },
            new LogEntry { Id = 3, Message = "archived", Level = "Error", IsArchived = true },
            new LogEntry { Id = 4, Message = "other", Level = "Information" });
        await context.SaveChangesAsync();
        var filter = new FilterModel
        {
            Page = 2, PageSize = 1,
            Filters = [new("Level", FilterOperator.Equal, "Error")],
            Orderings = [new() { Field = "Id" }]
        };
        var sut = CreateService(context);

        var result = await sut.SearchAsync(filter);

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
        result.CurrentPage.ShouldBe(2);
        result.PageSize.ShouldBe(1);
        result.Value.Single().Message.ShouldBe("second");
        result.Value.Single().LogEvents.ShouldBeEmpty();
        filter.Page.ShouldBe(2);
        filter.PageSize.ShouldBe(1);
    }

    [Fact]
    public async Task SearchAsync_StringComparison_ReturnsMatchingEntries()
    {
        await using var context = CreateContext();
        context.LogEntries.AddRange(
            new LogEntry { Id = 1, Message = "Alpha" },
            new LogEntry { Id = 2, Message = "Bravo" },
            new LogEntry { Id = 3, Message = "Charlie" });
        await context.SaveChangesAsync();
        var sut = CreateService(context);

        var result = await sut.SearchAsync(new FilterModel
        {
            Filters = [new("Message", FilterOperator.GreaterThan, "Bravo")]
        });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Value.Single().Message.ShouldBe("Charlie");
    }

    [Theory]
    [InlineData("Unknown", FilterOperator.Equal, "value")]
    [InlineData("LogEvents", FilterOperator.IsNull, null)]
    [InlineData("Message", FilterOperator.Any, "value")]
    public async Task SearchAsync_InvalidCriteria_ReturnsValidationFailure(string field, FilterOperator operation, string value)
    {
        await using var context = CreateContext();
        var sut = CreateService(context);

        var result = await sut.SearchAsync(new FilterModel { Filters = [new(field, operation, value)] });

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ValidationError);
    }

    [Fact]
    public async Task SearchAsync_NullFilter_ReturnsDefaultEmptyPage()
    {
        await using var context = CreateContext();
        var sut = CreateService(context);

        var result = await sut.SearchAsync(null);

        result.IsSuccess.ShouldBeTrue();
        result.CurrentPage.ShouldBe(1);
        result.PageSize.ShouldBe(50);
        result.TotalCount.ShouldBe(0);
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_PageBeyondMatches_ReturnsEmptyPageWithTotal()
    {
        await using var context = CreateContext();
        context.LogEntries.Add(new LogEntry { Message = "entry" });
        await context.SaveChangesAsync();
        var sut = CreateService(context);

        var result = await sut.SearchAsync(new FilterModel { Page = 2, PageSize = 1 });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.CurrentPage.ShouldBe(2);
        result.Value.ShouldBeEmpty();
    }

    private static LogContext CreateContext() => new(new DbContextOptionsBuilder<LogContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static LogEntryService<LogContext> CreateService(LogContext context) => new(
        NullLogger<LogEntryService<LogContext>>.Instance, context, new LogEntryMaintenanceQueue());

    private sealed class LogContext(DbContextOptions<LogContext> options) : DbContext(options), ILoggingContext
    {
        public DbSet<LogEntry> LogEntries { get; set; }
    }
}
