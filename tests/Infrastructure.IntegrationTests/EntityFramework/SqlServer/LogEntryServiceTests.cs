// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.SqlServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class LogEntryServiceTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly ITestOutputHelper output;
    private readonly StubDbContext context;

    public LogEntryServiceTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.output = output;

        // register the services
        this.fixture.Services.AddSingleton<LogEntryMaintenanceQueue>();
        this.fixture.Services.AddScoped<ILogEntryService, LogEntryService<StubDbContext>>();
        //this.fixture.Services.AddHostedService<LogEntryPurgeService<StubDbContext>>();

        this.fixture.Services.AddSqlServerDbContext<StubDbContext>(this.fixture.SqlConnectionString)
            .WithDatabaseCreatorService(o => o.DeleteOnStartup());

        this.context = this.fixture.EnsureSqlServerDbContext();
        this.context.SaveChanges();
    }

    private void SeedLogEntries()
    {
        this.context.LogEntries.RemoveRange(this.context.LogEntries); // Clean up first

        var now = DateTimeOffset.UtcNow;
        var entries = new List<LogEntry>
        {
            // Original 5 entries
            new() { Message = "Info log", Level = "Information", TimeStamp = now.AddMinutes(-10), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" },
            new() { Message = "Error log", Level = "Error", TimeStamp = now.AddMinutes(-5), TraceId = "trace-2", CorrelationId = "corr-2", LogKey = "key-2", ModuleName = "mod-2", ThreadId = "thread-2", ShortTypeName = "type-2" },
            new() { Message = "Debug log", Level = "Debug", TimeStamp = now.AddMinutes(-2), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" },
            new() { Message = "Warning log", Level = "Warning", TimeStamp = now.AddMinutes(-1), TraceId = "trace-3", CorrelationId = "corr-3", LogKey = "key-3", ModuleName = "mod-3", ThreadId = "thread-3", ShortTypeName = "type-3" },
            new() { Message = "Contains special search", Level = "Information", TimeStamp = now, TraceId = "trace-4", CorrelationId = "corr-4", LogKey = "key-4", ModuleName = "mod-4", ThreadId = "thread-4", ShortTypeName = "type-4" },

            // Many more diverse entries
            new() { Message = "Verbose log", Level = "Verbose", TimeStamp = now.AddMinutes(-30), TraceId = "trace-5", CorrelationId = "corr-5", LogKey = "key-5", ModuleName = "mod-5", ThreadId = "thread-5", ShortTypeName = "type-5" },
            new() { Message = "Fatal error occurred", Level = "Fatal", TimeStamp = now.AddMinutes(-25), TraceId = "trace-6", CorrelationId = "corr-6", LogKey = "key-6", ModuleName = "mod-6", ThreadId = "thread-6", ShortTypeName = "type-6" },
            new() { Message = "Another info log", Level = "Information", TimeStamp = now.AddMinutes(-20), TraceId = "trace-7", CorrelationId = "corr-7", LogKey = "key-7", ModuleName = "mod-7", ThreadId = "thread-7", ShortTypeName = "type-7" },
            new() { Message = "Debugging session started", Level = "Debug", TimeStamp = now.AddMinutes(-19), TraceId = "trace-8", CorrelationId = "corr-8", LogKey = "key-8", ModuleName = "mod-8", ThreadId = "thread-8", ShortTypeName = "type-8" },
            new() { Message = "Warning: disk space low", Level = "Warning", TimeStamp = now.AddMinutes(-18), TraceId = "trace-9", CorrelationId = "corr-9", LogKey = "key-9", ModuleName = "mod-9", ThreadId = "thread-9", ShortTypeName = "type-9" },
            new() { Message = "Error: connection lost", Level = "Error", TimeStamp = now.AddMinutes(-17), TraceId = "trace-10", CorrelationId = "corr-10", LogKey = "key-10", ModuleName = "mod-10", ThreadId = "thread-10", ShortTypeName = "type-10" },
            new() { Message = "Verbose: entering method", Level = "Verbose", TimeStamp = now.AddMinutes(-16), TraceId = "trace-11", CorrelationId = "corr-11", LogKey = "key-11", ModuleName = "mod-11", ThreadId = "thread-11", ShortTypeName = "type-11" },
            new() { Message = "Fatal: system crash", Level = "Fatal", TimeStamp = now.AddMinutes(-15), TraceId = "trace-12", CorrelationId = "corr-12", LogKey = "key-12", ModuleName = "mod-12", ThreadId = "thread-12", ShortTypeName = "type-12" },
            new() { Message = "Info: user login", Level = "Information", TimeStamp = now.AddMinutes(-14), TraceId = "trace-13", CorrelationId = "corr-13", LogKey = "key-13", ModuleName = "mod-13", ThreadId = "thread-13", ShortTypeName = "type-13" },
            new() { Message = "Debug: variable x=42", Level = "Debug", TimeStamp = now.AddMinutes(-13), TraceId = "trace-14", CorrelationId = "corr-14", LogKey = "key-14", ModuleName = "mod-14", ThreadId = "thread-14", ShortTypeName = "type-14" },
            new() { Message = "Warning: high memory usage", Level = "Warning", TimeStamp = now.AddMinutes(-12), TraceId = "trace-15", CorrelationId = "corr-15", LogKey = "key-15", ModuleName = "mod-15", ThreadId = "thread-15", ShortTypeName = "type-15" },
            new() { Message = "Error: null reference", Level = "Error", TimeStamp = now.AddMinutes(-11), TraceId = "trace-16", CorrelationId = "corr-16", LogKey = "key-16", ModuleName = "mod-16", ThreadId = "thread-16", ShortTypeName = "type-16" },
            new() { Message = "Verbose: leaving method", Level = "Verbose", TimeStamp = now.AddMinutes(-9), TraceId = "trace-17", CorrelationId = "corr-17", LogKey = "key-17", ModuleName = "mod-17", ThreadId = "thread-17", ShortTypeName = "type-17" },
            new() { Message = "Fatal: out of memory", Level = "Fatal", TimeStamp = now.AddMinutes(-8), TraceId = "trace-18", CorrelationId = "corr-18", LogKey = "key-18", ModuleName = "mod-18", ThreadId = "thread-18", ShortTypeName = "type-18" },
            new() { Message = "Info: scheduled task", Level = "Information", TimeStamp = now.AddMinutes(-7), TraceId = "trace-19", CorrelationId = "corr-19", LogKey = "key-19", ModuleName = "mod-19", ThreadId = "thread-19", ShortTypeName = "type-19" },
            new() { Message = "Debug: breakpoint hit", Level = "Debug", TimeStamp = now.AddMinutes(-6), TraceId = "trace-20", CorrelationId = "corr-20", LogKey = "key-20", ModuleName = "mod-20", ThreadId = "thread-20", ShortTypeName = "type-20" },
            new() { Message = "Warning: deprecated API", Level = "Warning", TimeStamp = now.AddMinutes(-4), TraceId = "trace-21", CorrelationId = "corr-21", LogKey = "key-21", ModuleName = "mod-21", ThreadId = "thread-21", ShortTypeName = "type-21" },
            new() { Message = "Error: timeout", Level = "Error", TimeStamp = now.AddMinutes(-3), TraceId = "trace-22", CorrelationId = "corr-22", LogKey = "key-22", ModuleName = "mod-22", ThreadId = "thread-22", ShortTypeName = "type-22" },
            new() { Message = "Verbose: finished processing", Level = "Verbose", TimeStamp = now.AddMinutes(-2), TraceId = "trace-23", CorrelationId = "corr-23", LogKey = "key-23", ModuleName = "mod-23", ThreadId = "thread-23", ShortTypeName = "type-23" },
            new() { Message = "Fatal: kernel panic", Level = "Fatal", TimeStamp = now.AddMinutes(-1), TraceId = "trace-24", CorrelationId = "corr-24", LogKey = "key-24", ModuleName = "mod-24", ThreadId = "thread-24", ShortTypeName = "type-24" },
            new() { Message = "Info: batch complete", Level = "Information", TimeStamp = now, TraceId = "trace-25", CorrelationId = "corr-25", LogKey = "key-25", ModuleName = "mod-25", ThreadId = "thread-25", ShortTypeName = "type-25" },

            // Add more for higher volume and variety
            new() { Message = "Debug: test entry 1", Level = "Debug", TimeStamp = now.AddSeconds(-10), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" },
            new() { Message = "Debug: test entry 2", Level = "Debug", TimeStamp = now.AddSeconds(-9), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" },
            new() { Message = "Debug: test entry 3", Level = "Debug", TimeStamp = now.AddSeconds(-8), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" },
            new() { Message = "Debug: test entry 4", Level = "Debug", TimeStamp = now.AddSeconds(-7), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" },
            new() { Message = "Debug: test entry 5", Level = "Debug", TimeStamp = now.AddSeconds(-6), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" },
            new() { Message = "Debug: test entry 6", Level = "Debug", TimeStamp = now.AddSeconds(-5), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" },
            new() { Message = "Debug: test entry 7", Level = "Debug", TimeStamp = now.AddSeconds(-4), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" },
            new() { Message = "Debug: test entry 8", Level = "Debug", TimeStamp = now.AddSeconds(-3), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" },
            new() { Message = "Debug: test entry 9", Level = "Debug", TimeStamp = now.AddSeconds(-2), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" },
            new() { Message = "Debug: test entry 10", Level = "Debug", TimeStamp = now.AddSeconds(-1), TraceId = "trace-1", CorrelationId = "corr-1", LogKey = "key-1", ModuleName = "mod-1", ThreadId = "thread-1", ShortTypeName = "type-1" }
        };

        this.context.LogEntries.AddRange(entries);
        this.context.SaveChanges();
    }

    [Fact]
    public void LogEntryQueryService_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var sut = this.fixture.ServiceProvider.GetService<ILogEntryService>();

        // Assert
        sut.ShouldNotBeNull();
    }

    [Fact]
    public async Task QueryLogEntriesAsync_ShouldReturnAllEntries_WhenNoFilter()
    {
        // Arrange
        this.SeedLogEntries();
        var sut = this.fixture.ServiceProvider.GetRequiredService<ILogEntryService>();
        var request = new LogEntryQueryRequest { PageSize = 100 };

        // Act
        var result = await sut.QueryAsync(request);

        // Assert
        // There are 36 entries seeded in total
        result.Items.Count.ShouldBe(36);
    }

    [Fact]
    public async Task QueryLogEntriesAsync_ShouldFilterByLevel()
    {
        this.SeedLogEntries();
        var sut = this.fixture.ServiceProvider.GetRequiredService<ILogEntryService>();
        var request = new LogEntryQueryRequest { Level = LogLevel.Error, PageSize = 100 };

        var result = await sut.QueryAsync(request);

        result.Items.Count.ShouldBe(8); // error and fatal
        result.Items.All(e => e.Level == "Error" || e.Level == "Fatal").ShouldBeTrue();
    }

    [Fact]
    public async Task QueryLogEntriesAsync_ShouldFilterByTimeRange()
    {
        this.SeedLogEntries();
        var sut = this.fixture.ServiceProvider.GetRequiredService<ILogEntryService>();
        var now = DateTimeOffset.UtcNow;
        var request = new LogEntryQueryRequest
        {
            StartTime = now.AddMinutes(-3),
            EndTime = now,
            PageSize = 100
        };

        var result = await sut.QueryAsync(request);

        // Entries in the last 3 minutes:
        // "Debug log" (-2), "Verbose: finished processing" (-2), "Warning log" (-1), "Fatal: kernel panic" (-1), "Contains special search" (0), "Info: batch complete" (0),
        // "Debug: test entry 1" to "Debug: test entry 10" (all within last 10 seconds)
        // Total: 2 (Debug log, Verbose: finished processing) + 2 (Warning log, Fatal: kernel panic) + 2 (Contains special search, Info: batch complete) + 10 = 16
        result.Items.Count.ShouldBe(16);
        result.Items.All(e => e.TimeStamp >= request.StartTime && e.TimeStamp <= request.EndTime).ShouldBeTrue();
    }

    [Fact]
    public async Task QueryLogEntriesAsync_ShouldFilterBySearchText()
    {
        this.SeedLogEntries();
        var sut = this.fixture.ServiceProvider.GetRequiredService<ILogEntryService>();
        var request = new LogEntryQueryRequest { SearchText = "special search", PageSize = 100 };

        var result = await sut.QueryAsync(request);

        // Only "Contains special search"
        result.Items.Count.ShouldBe(1);
        result.Items[0].Message.ShouldContain("special search");
    }

    [Fact]
    public async Task QueryLogEntriesAsync_ShouldPaginateResults()
    {
        this.SeedLogEntries();
        var sut = this.fixture.ServiceProvider.GetRequiredService<ILogEntryService>();
        var request = new LogEntryQueryRequest { PageSize = 10 };

        var firstPage = await sut.QueryAsync(request);
        firstPage.Items.Count.ShouldBe(10);
        firstPage.ContinuationToken.ShouldNotBeNull();

        request.ContinuationToken = firstPage.ContinuationToken;
        var secondPage = await sut.QueryAsync(request);
        secondPage.Items.Count.ShouldBe(10);
        secondPage.ContinuationToken.ShouldNotBeNull();

        request.ContinuationToken = secondPage.ContinuationToken;
        var thirdPage = await sut.QueryAsync(request);
        thirdPage.Items.Count.ShouldBe(10);
        thirdPage.ContinuationToken.ShouldNotBeNull();

        request.ContinuationToken = thirdPage.ContinuationToken;
        var fourthPage = await sut.QueryAsync(request);
        fourthPage.Items.Count.ShouldBe(6);
        fourthPage.ContinuationToken.ShouldBeNull();
    }

    [Fact]
    public async Task QueryLogEntriesAsync_ShouldFilterByTraceId()
    {
        this.SeedLogEntries();
        var sut = this.fixture.ServiceProvider.GetRequiredService<ILogEntryService>();
        var request = new LogEntryQueryRequest { TraceId = "trace-1", PageSize = 100 };

        var result = await sut.QueryAsync(request);

        // Entries with TraceId == "trace-1":
        // "Info log", "Debug log", "Debug: test entry 1" to "Debug: test entry 10" (12 entries)
        result.Items.Count.ShouldBe(12);
        result.Items.All(e => e.TraceId == "trace-1").ShouldBeTrue();
    }
}