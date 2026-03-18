// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.DataPorter;
using Microsoft.Extensions.Logging;

[UnitTest("Common")]
public class DataPorterServiceRowInterceptionTests
{
    private readonly ConfigurationMerger configurationMerger;

    public DataPorterServiceRowInterceptionTests()
    {
        this.configurationMerger = new ConfigurationMerger(new ProfileRegistry(), new AttributeConfigurationReader());
    }

    [Fact]
    public async Task ExportAsync_WithTypedInterceptors_MutatesRowsInOrder()
    {
        // Arrange
        var mutatorOne = new TestExportRowInterceptor(context =>
        {
            context.Item.Name += "-one";
            return RowInterceptionDecision.Continue();
        });
        var mutatorTwo = new TestExportRowInterceptor(context =>
        {
            context.Item.Name += "-two";
            return RowInterceptionDecision.Continue();
        });
        var interceptors = new TestRowInterceptorsProvider()
            .AddExport(mutatorOne, mutatorTwo);
        var sut = new DataPorterService([new JsonDataPorterProvider()], this.configurationMerger, interceptors);
        var data = new[] { new SimpleEntity { Id = 1, Name = "Item" } };
        await using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(data, stream, new ExportOptions { Format = Format.Json });

        // Assert
        result.ShouldBeSuccess();
        result.Value.SkippedRows.ShouldBe(0);
        result.Value.Warnings.ShouldBeEmpty();
        mutatorOne.BeforeCalls.ShouldBe(1);
        mutatorOne.AfterCalls.ShouldBe(1);
        mutatorTwo.BeforeCalls.ShouldBe(1);
        mutatorTwo.AfterCalls.ShouldBe(1);
        Encoding.UTF8.GetString(stream.ToArray()).ShouldContain("Item-one-two");
    }

    [Fact]
    public async Task ExportAsync_WithTypedInterceptors_SkipsRowsAndReportsSkippedProgress()
    {
        // Arrange
        var mutator = new TestExportRowInterceptor(context =>
        {
            context.Item.Name += "-exported";
            return RowInterceptionDecision.Continue();
        });
        var skipper = new TestExportRowInterceptor(context =>
            context.RowNumber == 2
                ? RowInterceptionDecision.Skip("skip export row 2")
                : RowInterceptionDecision.Continue());
        var progress = new TestProgress<ExportProgressReport>();
        var interceptors = new TestRowInterceptorsProvider()
            .AddExport(mutator, skipper);
        var sut = new DataPorterService([new JsonDataPorterProvider()], this.configurationMerger, interceptors);
        var data = Enumerable.Range(1, 26)
            .Select(i => new SimpleEntity { Id = i, Name = i == 2 ? "SkipMe" : $"Item {i}" })
            .ToArray();
        await using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(data, stream, new ExportOptions { Format = Format.Json, Progress = progress });

        // Assert
        result.ShouldBeSuccess();
        result.Value.TotalRows.ShouldBe(25);
        result.Value.SkippedRows.ShouldBe(1);
        result.Value.Warnings.Count.ShouldBe(1);
        result.Value.Warnings[0].ShouldContain("skip export row 2");
        progress.Reports.ShouldContain(report => !report.IsCompleted && report.ProcessedRows == 25 && report.SkippedRows == 1);
        progress.Reports[^1].IsCompleted.ShouldBeTrue();
        progress.Reports[^1].SkippedRows.ShouldBe(1);
        var payload = Encoding.UTF8.GetString(stream.ToArray());
        payload.ShouldContain("Item 1-exported");
        payload.ShouldNotContain("SkipMe");
    }

    [Fact]
    public async Task ExportAsync_WithTypedInterceptors_AbortsOperation()
    {
        // Arrange
        var aborter = new TestExportRowInterceptor(context =>
            context.RowNumber == 2
                ? RowInterceptionDecision.Abort("abort export row 2")
                : RowInterceptionDecision.Continue());
        var interceptors = new TestRowInterceptorsProvider()
            .AddExport(aborter);
        var sut = new DataPorterService([new JsonDataPorterProvider()], this.configurationMerger, interceptors);
        var data = Enumerable.Range(1, 3)
            .Select(i => new SimpleEntity { Id = i, Name = $"Item {i}" })
            .ToArray();
        await using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(data, stream, new ExportOptions { Format = Format.Json });

        // Assert
        result.ShouldBeFailure();
        result.HasError<ExportInterceptionAbortedError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ImportAsync_WithTypedInterceptors_MutatesRowsInOrder()
    {
        // Arrange
        var mutatorOne = new TestImportRowInterceptor(context =>
        {
            context.Item.Name += "-one";
            return RowInterceptionDecision.Continue();
        });
        var mutatorTwo = new TestImportRowInterceptor(context =>
        {
            context.Item.Name += "-two";
            return RowInterceptionDecision.Continue();
        });
        var interceptors = new TestRowInterceptorsProvider()
            .AddImport(mutatorOne, mutatorTwo);
        var sut = new DataPorterService([new JsonDataPorterProvider()], this.configurationMerger, interceptors);
        var payload = JsonSerializer.Serialize(new[] { new SimpleEntity { Id = 1, Name = "Item" } });
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream, new ImportOptions { Format = Format.Json });

        // Assert
        result.ShouldBeSuccess();
        result.Value.SkippedRows.ShouldBe(0);
        result.Value.Warnings.ShouldBeEmpty();
        result.Value.Data.Count.ShouldBe(1);
        result.Value.Data[0].Name.ShouldBe("Item-one-two");
        mutatorOne.BeforeCalls.ShouldBe(1);
        mutatorOne.AfterCalls.ShouldBe(1);
        mutatorTwo.BeforeCalls.ShouldBe(1);
        mutatorTwo.AfterCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ImportAsync_WithTypedInterceptors_SkipsRowsAndReportsSkippedProgress()
    {
        // Arrange
        var mutator = new TestImportRowInterceptor(context =>
        {
            context.Item.Name += "-imported";
            return RowInterceptionDecision.Continue();
        });
        var skipper = new TestImportRowInterceptor(context =>
            context.RowNumber == 2
                ? RowInterceptionDecision.Skip("skip import row 2")
                : RowInterceptionDecision.Continue());
        var progress = new TestProgress<ImportProgressReport>();
        var interceptors = new TestRowInterceptorsProvider()
            .AddImport(mutator, skipper);
        var sut = new DataPorterService([new JsonDataPorterProvider()], this.configurationMerger, interceptors);
        var payload = JsonSerializer.Serialize(
            Enumerable.Range(1, 26).Select(i => new SimpleEntity
            {
                Id = i,
                Name = i == 2 ? "SkipMe" : $"Item {i}"
            }));
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream, new ImportOptions { Format = Format.Json, Progress = progress });

        // Assert
        result.ShouldBeSuccess();
        result.Value.TotalRows.ShouldBe(26);
        result.Value.SuccessfulRows.ShouldBe(25);
        result.Value.FailedRows.ShouldBe(1);
        result.Value.SkippedRows.ShouldBe(1);
        result.Value.Warnings.Count.ShouldBe(1);
        result.Value.Warnings[0].ShouldContain("skip import row 2");
        result.Value.Data.Count.ShouldBe(25);
        result.Value.Data.ShouldContain(item => item.Name == "Item 1-imported");
        progress.Reports.ShouldContain(report => !report.IsCompleted && report.ProcessedRows == 25 && report.SkippedRows == 1);
        progress.Reports[^1].IsCompleted.ShouldBeTrue();
        progress.Reports[^1].SkippedRows.ShouldBe(1);
    }

    [Fact]
    public async Task ImportAsync_WithTypedInterceptors_AbortsOperation()
    {
        // Arrange
        var aborter = new TestImportRowInterceptor(context =>
            context.RowNumber == 2
                ? RowInterceptionDecision.Abort("abort import row 2")
                : RowInterceptionDecision.Continue());
        var interceptors = new TestRowInterceptorsProvider()
            .AddImport(aborter);
        var sut = new DataPorterService([new JsonDataPorterProvider()], this.configurationMerger, interceptors);
        var payload = JsonSerializer.Serialize(new[]
        {
            new SimpleEntity { Id = 1, Name = "Item 1" },
            new SimpleEntity { Id = 2, Name = "Item 2" }
        });
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream, new ImportOptions { Format = Format.Json });

        // Assert
        result.ShouldBeFailure();
        result.HasError<ImportInterceptionAbortedError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_WithClosedGenericInterceptor_DoesNotRunForOtherType()
    {
        // Arrange
        var orderInterceptor = new TestExportRowInterceptor(context =>
        {
            context.Item.Name += "-intercepted";
            return RowInterceptionDecision.Continue();
        });
        var interceptors = new TestRowInterceptorsProvider()
            .AddExport(orderInterceptor);
        var sut = new DataPorterService([new JsonDataPorterProvider()], this.configurationMerger, interceptors);
        var data = new[] { new OtherEntity { Id = 1, Name = "Person" } };
        await using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(data, stream, new ExportOptions { Format = Format.Json });

        // Assert
        result.ShouldBeSuccess();
        orderInterceptor.BeforeCalls.ShouldBe(0);
        orderInterceptor.AfterCalls.ShouldBe(0);
        Encoding.UTF8.GetString(stream.ToArray()).ShouldContain("Person");
        Encoding.UTF8.GetString(stream.ToArray()).ShouldNotContain("intercepted");
    }

    [Fact]
    public async Task ExportRowInterceptionExecutor_Skip_LogsWarning()
    {
        // Arrange
        var logger = new TestLogger();
        var interceptor = new TestExportRowInterceptor(_ => RowInterceptionDecision.Skip("skip warning"));
        var sut = new ExportRowInterceptionExecutor<SimpleEntity>([interceptor], logger);

        // Act
        var result = await sut.BeforeAsync(new SimpleEntity { Id = 1, Name = "Item" }, 2, Format.Json, "Feed", false);

        // Assert
        result.Outcome.ShouldBe(RowInterceptionOutcome.Skip);
        logger.Messages.ShouldContain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("row interceptor skipped", StringComparison.OrdinalIgnoreCase) &&
            entry.Message.Contains("rowNumber=2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportRowInterceptionExecutor_Abort_LogsWarning()
    {
        // Arrange
        var logger = new TestLogger();
        var interceptor = new TestImportRowInterceptor(_ => RowInterceptionDecision.Abort("abort warning"));
        var sut = new ImportRowInterceptionExecutor<SimpleEntity>([interceptor], logger);

        // Act
        var result = await sut.BeforeAsync(new SimpleEntity { Id = 1, Name = "Item" }, 3, Format.Json, "Feed", true);

        // Assert
        result.Outcome.ShouldBe(RowInterceptionOutcome.Abort);
        logger.Messages.ShouldContain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("row interceptor aborted", StringComparison.OrdinalIgnoreCase) &&
            entry.Message.Contains("rowNumber=3", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TestRowInterceptorsProvider : IRowInterceptorsProvider
    {
        private readonly Dictionary<Type, object> importInterceptors = new();
        private readonly Dictionary<Type, object> exportInterceptors = new();

        public TestRowInterceptorsProvider AddImport<TTarget>(params IImportRowInterceptor<TTarget>[] interceptors)
            where TTarget : class
        {
            this.importInterceptors[typeof(TTarget)] = interceptors.ToArray();
            return this;
        }

        public TestRowInterceptorsProvider AddExport<TSource>(params IExportRowInterceptor<TSource>[] interceptors)
            where TSource : class
        {
            this.exportInterceptors[typeof(TSource)] = interceptors.ToArray();
            return this;
        }

        public IReadOnlyList<IImportRowInterceptor<TTarget>> GetImportInterceptors<TTarget>()
            where TTarget : class
        {
            return this.importInterceptors.TryGetValue(typeof(TTarget), out var interceptors)
                ? (IReadOnlyList<IImportRowInterceptor<TTarget>>)interceptors
                : [];
        }

        public IReadOnlyList<IExportRowInterceptor<TSource>> GetExportInterceptors<TSource>()
            where TSource : class
        {
            return this.exportInterceptors.TryGetValue(typeof(TSource), out var interceptors)
                ? (IReadOnlyList<IExportRowInterceptor<TSource>>)interceptors
                : [];
        }
    }

    private sealed class TestExportRowInterceptor : IExportRowInterceptor<SimpleEntity>
    {
        private readonly Func<ExportRowContext<SimpleEntity>, RowInterceptionDecision> before;

        public TestExportRowInterceptor(
            Func<ExportRowContext<SimpleEntity>, RowInterceptionDecision> before,
            Action<ExportRowContext<SimpleEntity>> after = null)
        {
            this.before = before;
            this.after = after;
        }

        private readonly Action<ExportRowContext<SimpleEntity>> after;

        public int BeforeCalls { get; private set; }

        public int AfterCalls { get; private set; }

        public Task<RowInterceptionDecision> BeforeExportAsync(
            ExportRowContext<SimpleEntity> context,
            CancellationToken cancellationToken = default)
        {
            this.BeforeCalls++;
            return Task.FromResult(this.before(context));
        }

        public Task AfterExportAsync(
            ExportRowContext<SimpleEntity> context,
            CancellationToken cancellationToken = default)
        {
            this.AfterCalls++;
            this.after?.Invoke(context);
            return Task.CompletedTask;
        }
    }

    private sealed class TestImportRowInterceptor : IImportRowInterceptor<SimpleEntity>
    {
        private readonly Func<ImportRowContext<SimpleEntity>, RowInterceptionDecision> before;

        public TestImportRowInterceptor(
            Func<ImportRowContext<SimpleEntity>, RowInterceptionDecision> before,
            Action<ImportRowContext<SimpleEntity>> after = null)
        {
            this.before = before;
            this.after = after;
        }

        private readonly Action<ImportRowContext<SimpleEntity>> after;

        public int BeforeCalls { get; private set; }

        public int AfterCalls { get; private set; }

        public Task<RowInterceptionDecision> BeforeImportAsync(
            ImportRowContext<SimpleEntity> context,
            CancellationToken cancellationToken = default)
        {
            this.BeforeCalls++;
            return Task.FromResult(this.before(context));
        }

        public Task AfterImportAsync(
            ImportRowContext<SimpleEntity> context,
            CancellationToken cancellationToken = default)
        {
            this.AfterCalls++;
            this.after?.Invoke(context);
            return Task.CompletedTask;
        }
    }

    private sealed class OtherEntity
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    private sealed class TestLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            this.Messages.Add((logLevel, formatter(state, exception)));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
