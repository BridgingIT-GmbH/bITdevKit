// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text;
using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.Logging;

[UnitTest("Application")]
public sealed class BlobStoreClientBehaviorTests
{
    [Fact]
    public async Task CreateClient_WithRegisteredBehaviors_WrapsNamedClientsInRegistrationOrder()
    {
        // Arrange
        var events = new List<string>();
        var reportsProvider = CreateProvider();
        var mediaProvider = CreateProvider();
        reportsProvider.ExistsAsync(Arg.Any<BlobKey>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        mediaProvider.ExistsAsync(Arg.Any<BlobKey>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithBehavior((inner, _, name) => new RecordingBlobStoreClientBehavior("outer", name, events, inner))
            .WithBehavior((inner, _, name) => new RecordingBlobStoreClientBehavior("inner", name, events, inner))
            .WithClient("reports", _ => reportsProvider)
            .WithClient("media", _ => mediaProvider);
        using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IBlobStoreClientFactory>();

        // Act
        await factory.CreateClient("reports").ExistsAsync(new BlobKey("reports", "probe"));
        await factory.CreateClient("media").ExistsAsync(new BlobKey("media", "probe"));

        // Assert
        events.ShouldBe([
            "reports:outer:before:exists",
            "reports:inner:before:exists",
            "reports:inner:after:exists",
            "reports:outer:after:exists",
            "media:outer:before:exists",
            "media:inner:before:exists",
            "media:inner:after:exists",
            "media:outer:after:exists"
        ]);
    }

    [Fact]
    public async Task LoggingBehavior_WithSensitiveModels_LogsNamesButNotContentTokensOrPropertyValues()
    {
        // Arrange
        var loggerProvider = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider).SetMinimumLevel(LogLevel.Debug));
        var inner = new ScriptedBlobStoreClient
        {
            Upload = _ => Result<BlobInfo>.Success(new BlobInfo
            {
                Key = new BlobKey("reports", "secret/blob/name.txt"),
                Length = 20
            }),
            List = _ => Result<BlobPage>.Success(new BlobPage())
        };
        var sut = new LoggingBlobStoreClientBehavior(loggerFactory, inner, "reports");

        // Act
        await sut.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "secret/blob/name.txt"),
            Content = new MemoryStream(Encoding.UTF8.GetBytes("super-secret-content")),
            Properties = new PropertyBag
            {
                ["tenant"] = "tenant-42",
                ["secret"] = "secret-property-value"
            }
        });
        await sut.ListPageAsync(new BlobQuery
        {
            Container = "reports",
            Prefix = "secret/blob/",
            ContinuationToken = "raw-continuation-token",
            Take = 10
        });

        // Assert
        var text = string.Join(Environment.NewLine, loggerProvider.Messages);
        text.ShouldContain("blobclient");
        text.ShouldContain("secret/blob/name.txt");
        text.ShouldContain("secret/blob/");
        text.ShouldNotContain("super-secret-content");
        text.ShouldNotContain("raw-continuation-token");
        text.ShouldNotContain("secret-property-value");
        text.ShouldNotContain("tenant-42");
    }

    [Fact]
    public async Task MetricsBehavior_WithOperations_EmitsExpectedLowCardinalityMetrics()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var metrics = new MetricsBlobStoreClientBehavior(
            meterFactory,
            new ScriptedBlobStoreClient
            {
                Upload = _ => Result<BlobInfo>.Success(new BlobInfo
                {
                    Key = new BlobKey("reports", "secret/blob/name.txt"),
                    Length = 7
                }),
                List = _ => Result<BlobPage>.Success(new BlobPage
                {
                    Items =
                    [
                        new BlobInfo { Key = new BlobKey("reports", "a.txt") },
                        new BlobInfo { Key = new BlobKey("reports", "b.txt") }
                    ]
                }),
                Exists = _ => Result<bool>.Failure(new BlobStoreSizeLimitExceededError(12, 10))
            },
            "reports");

        // Act
        await metrics.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "secret/blob/name.txt"),
            Content = new MemoryStream([1, 2, 3]),
            Properties = new PropertyBag { ["tenant"] = "tenant-42" }
        });
        await metrics.ListPageAsync(new BlobQuery
        {
            Container = "reports",
            Prefix = "secret/blob/",
            ContinuationToken = "raw-continuation-token"
        });
        await metrics.ExistsAsync(new BlobKey("reports", "missing.txt"));

        // Assert
        recorder.CounterSum("blobstorage_operations").ShouldBe(3);
        recorder.HistogramCount("blobstorage_operation_duration").ShouldBe(3);
        recorder.CounterSum("blobstorage_operation_failures").ShouldBe(1);
        recorder.CounterSum("blobstorage_bytes").ShouldBe(7);
        recorder.CounterSum("blobstorage_list_items").ShouldBe(2);
        recorder.CounterSum("blobstorage_size_limit_failures").ShouldBe(1);
        recorder.AllTagKeys.ShouldBeSubsetOf(["operation", "store"]);
        recorder.AllTagValues.ShouldNotContain("secret/blob/name.txt");
        recorder.AllTagValues.ShouldNotContain("raw-continuation-token");
        recorder.AllTagValues.ShouldNotContain("tenant-42");
        recorder.AllTagValues.ShouldNotContain("secret-property-value");
        recorder.AllTagKeys.ShouldNotContain("user");
        recorder.AllTagKeys.ShouldNotContain("tenant");
    }

    [Fact]
    public async Task MetricsBehavior_WithRetryAndTimeout_EmitsRetryAndTimeoutMetrics()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var retryAttempts = 0;
        var retryInner = new ScriptedBlobStoreClient
        {
            Exists = _ => ++retryAttempts == 1
                ? Result<bool>.Failure(new BlobStoreProviderError("transient"))
                : Result<bool>.Success(true)
        };
        var retry = new RetryBlobStoreClientBehavior(
            retryInner,
            new RetryBlobStoreClientBehaviorOptions { Attempts = 2, Backoff = TimeSpan.Zero },
            "reports");
        var metricsWithRetry = new MetricsBlobStoreClientBehavior(meterFactory, retry, "reports");
        var timeout = new TimeoutBlobStoreClientBehavior(
            new ScriptedBlobStoreClient
            {
                ExistsHandlerAsync = async (_, token) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                    return Result<bool>.Success(true);
                }
            },
            new TimeoutBlobStoreClientBehaviorOptions { Timeout = TimeSpan.FromMilliseconds(20) },
            "reports");
        var metricsWithTimeout = new MetricsBlobStoreClientBehavior(meterFactory, timeout, "reports");

        // Act
        await metricsWithRetry.ExistsAsync(new BlobKey("reports", "probe"));
        await metricsWithTimeout.ExistsAsync(new BlobKey("reports", "slow"));

        // Assert
        recorder.CounterSum("blobstorage_retries").ShouldBe(1);
        recorder.CounterSum("blobstorage_timeouts").ShouldBe(1);
    }

    [Fact]
    public async Task RetryBehavior_WithTransientProviderFailure_RetriesUntilSuccess()
    {
        // Arrange
        var attempts = 0;
        var inner = new ScriptedBlobStoreClient
        {
            Exists = _ => ++attempts == 1
                ? Result<bool>.Failure(new BlobStoreProviderError("transient"))
                : Result<bool>.Success(true)
        };
        var sut = new RetryBlobStoreClientBehavior(
            inner,
            new RetryBlobStoreClientBehaviorOptions { Attempts = 2, Backoff = TimeSpan.Zero });

        // Act
        var result = await sut.ExistsAsync(new BlobKey("reports", "probe"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        attempts.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(NonRetryableErrors))]
    public async Task RetryBehavior_WithNonRetryableFailure_DoesNotRetry(IResultError error)
    {
        // Arrange
        var attempts = 0;
        var inner = new ScriptedBlobStoreClient
        {
            Exists = _ =>
            {
                attempts++;
                return Result<bool>.Failure(error);
            }
        };
        var sut = new RetryBlobStoreClientBehavior(
            inner,
            new RetryBlobStoreClientBehaviorOptions { Attempts = 3, Backoff = TimeSpan.Zero });

        // Act
        var result = await sut.ExistsAsync(new BlobKey("reports", "probe"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        attempts.ShouldBe(1);
    }

    [Fact]
    public async Task RetryBehavior_WithSeekableUpload_RewindsBeforeRetry()
    {
        // Arrange
        var positions = new List<long>();
        var attempts = 0;
        var inner = new ScriptedBlobStoreClient
        {
            UploadHandlerAsync = async (upload, _) =>
            {
                attempts++;
                positions.Add(upload.Content.Position);
                await upload.Content.CopyToAsync(Stream.Null);
                return attempts == 1
                    ? Result<BlobInfo>.Failure(new BlobStoreProviderError("transient"))
                    : Result<BlobInfo>.Success(new BlobInfo { Key = upload.Key, Length = 3 });
            }
        };
        var sut = new RetryBlobStoreClientBehavior(
            inner,
            new RetryBlobStoreClientBehaviorOptions { Attempts = 2, Backoff = TimeSpan.Zero });

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "retry.bin"),
            Content = new MemoryStream([1, 2, 3])
        });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        positions.ShouldBe([0, 0]);
    }

    [Fact]
    public async Task RetryBehavior_WithNonSeekableUpload_DoesNotRetryByDefault()
    {
        // Arrange
        var attempts = 0;
        var inner = new ScriptedBlobStoreClient
        {
            Upload = _ =>
            {
                attempts++;
                return Result<BlobInfo>.Failure(new BlobStoreProviderError("transient"));
            }
        };
        var sut = new RetryBlobStoreClientBehavior(
            inner,
            new RetryBlobStoreClientBehaviorOptions { Attempts = 3, Backoff = TimeSpan.Zero });

        // Act
        var result = await sut.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "nonseekable.bin"),
            Content = new NonSeekableReadStream([1, 2, 3])
        });

        // Assert
        result.IsFailure.ShouldBeTrue();
        attempts.ShouldBe(1);
    }

    [Fact]
    public async Task TimeoutBehavior_WhenOperationExceedsTimeout_ReturnsTimeoutErrorAndCancelsLinkedToken()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = false;
        var inner = new ScriptedBlobStoreClient
        {
            ExistsHandlerAsync = async (_, token) =>
            {
                started.TrySetResult();

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    cancellationObserved = true;
                    throw;
                }

                return Result<bool>.Success(true);
            }
        };
        var timeProvider = new FakeTimeProvider();
        var sut = new TimeoutBlobStoreClientBehavior(
            inner,
            new TimeoutBlobStoreClientBehaviorOptions { Timeout = TimeSpan.FromMilliseconds(20) },
            timeProvider: timeProvider);

        // Act
        var operation = sut.ExistsAsync(new BlobKey("reports", "slow"));
        await started.Task;
        timeProvider.Advance(TimeSpan.FromMilliseconds(20));
        var result = await operation;

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreTimeoutError>().ShouldBeTrue();
        cancellationObserved.ShouldBeTrue();
    }

    [Fact]
    public async Task TimeoutBehavior_WithCallerCancellation_DoesNotMaskCancellation()
    {
        // Arrange
        var inner = new ScriptedBlobStoreClient
        {
            ExistsHandlerAsync = async (_, token) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);
                return Result<bool>.Success(true);
            }
        };
        var sut = new TimeoutBlobStoreClientBehavior(
            inner,
            new TimeoutBlobStoreClientBehaviorOptions { Timeout = TimeSpan.FromSeconds(1) });
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var action = () => sut.ExistsAsync(new BlobKey("reports", "slow"), cancellationTokenSource.Token);

        // Assert
        await action.ShouldThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TimeoutBehavior_WhenCancellationRequiresCleanup_WaitsForOperationToQuiesce()
    {
        // Arrange
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupCompleted = false;
        var inner = new ScriptedBlobStoreClient
        {
            ExistsHandlerAsync = async (_, token) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved.TrySetResult();
                    await releaseCleanup.Task;
                    cleanupCompleted = true;
                    throw;
                }

                return Result<bool>.Success(true);
            }
        };
        var sut = new TimeoutBlobStoreClientBehavior(
            inner,
            new TimeoutBlobStoreClientBehaviorOptions { Timeout = TimeSpan.FromMilliseconds(20) });

        // Act
        var operation = sut.ExistsAsync(new BlobKey("reports", "slow"));
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var completedBeforeCleanup = operation.IsCompleted;
        releaseCleanup.TrySetResult();
        var result = await operation;

        // Assert
        completedBeforeCleanup.ShouldBeFalse();
        cleanupCompleted.ShouldBeTrue();
        result.HasError<BlobStoreTimeoutError>().ShouldBeTrue();
    }

    [Fact]
    public async Task TimeoutBehavior_WithFakeTimeProvider_UsesInjectedClock()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new ScriptedBlobStoreClient
        {
            ExistsHandlerAsync = async (_, token) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Result<bool>.Success(true);
            }
        };
        var timeProvider = new FakeTimeProvider();
        var sut = new TimeoutBlobStoreClientBehavior(
            inner,
            new TimeoutBlobStoreClientBehaviorOptions { Timeout = TimeSpan.FromMinutes(1) },
            timeProvider: timeProvider);

        // Act
        var operation = sut.ExistsAsync(new BlobKey("reports", "slow"));
        await started.Task;
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        var result = await operation;

        // Assert
        result.HasError<BlobStoreTimeoutError>().ShouldBeTrue();
    }

    public static IEnumerable<object[]> NonRetryableErrors()
    {
        yield return [new BlobStoreValidationError("invalid")];
        yield return [new BlobStoreNotFoundError(new BlobKey("reports", "missing"))];
        yield return [new BlobStoreConflictError("conflict")];
        yield return [new BlobStoreLeaseError("lease")];
        yield return [new BlobStoreSizeLimitExceededError(2, 1)];
        yield return [new BlobStoreIntegrityError("integrity")];
        yield return [new BlobStoreQueryNotSupportedError("unsupported")];
        yield return [new BlobStoreQueryTooBroadError("too broad")];
        yield return [new OperationCancelledError()];
    }

    private static IBlobStoreProvider CreateProvider()
    {
        var provider = Substitute.For<IBlobStoreProvider>();
        provider.Capabilities.Returns(new BlobStoreProviderCapabilities
        {
            SupportsContinuationPaging = true,
            SupportsPrefixListing = true,
            SupportsFullContainerScan = true
        });

        return provider;
    }

    private sealed class RecordingBlobStoreClientBehavior : BlobStoreClientBehaviorBase
    {
        private readonly string behaviorName;
        private readonly string storeName;
        private readonly List<string> events;

        public RecordingBlobStoreClientBehavior(
            string behaviorName,
            string storeName,
            List<string> events,
            IBlobStoreClient inner)
            : base(inner, storeName)
        {
            this.behaviorName = behaviorName;
            this.storeName = storeName;
            this.events = events;
        }

        protected override async Task<Result<T>> ExecuteAsync<T>(
            string operation,
            BlobStoreOperationContext context,
            Func<CancellationToken, Task<Result<T>>> next,
            CancellationToken cancellationToken)
        {
            this.events.Add($"{this.storeName}:{this.behaviorName}:before:{operation}");
            var result = await next(cancellationToken);
            this.events.Add($"{this.storeName}:{this.behaviorName}:after:{operation}");

            return result;
        }

        protected override async Task<Result> ExecuteAsync(
            string operation,
            BlobStoreOperationContext context,
            Func<CancellationToken, Task<Result>> next,
            CancellationToken cancellationToken)
        {
            this.events.Add($"{this.storeName}:{this.behaviorName}:before:{operation}");
            var result = await next(cancellationToken);
            this.events.Add($"{this.storeName}:{this.behaviorName}:after:{operation}");

            return result;
        }
    }

    private sealed class ScriptedBlobStoreClient : IBlobStoreClient
    {
        public Func<BlobUpload, Result<BlobInfo>> Upload { get; init; }

        public Func<BlobUpload, CancellationToken, Task<Result<BlobInfo>>> UploadHandlerAsync { get; init; }

        public Func<BlobKey, Result<BlobDownload>> Download { get; init; }

        public Func<BlobKey, Result<BlobInfo>> GetProperties { get; init; }

        public Func<BlobPropertiesUpdate, Result<BlobInfo>> UpdateProperties { get; init; }

        public Func<BlobKey, Result<bool>> Exists { get; init; }

        public Func<BlobKey, CancellationToken, Task<Result<bool>>> ExistsHandlerAsync { get; init; }

        public Func<BlobQuery, Result<BlobPage>> List { get; init; }

        public Func<BlobKey, Result> Delete { get; init; }

        public Task<Result<BlobInfo>> UploadAsync(BlobUpload upload, CancellationToken cancellationToken = default) =>
            this.UploadHandlerAsync is not null
                ? this.UploadHandlerAsync(upload, cancellationToken)
                : Task.FromResult(this.Upload?.Invoke(upload) ?? Result<BlobInfo>.Success(new BlobInfo { Key = upload.Key }));

        public Task<Result<BlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult(this.Download?.Invoke(key) ?? Result<BlobDownload>.Failure(new BlobStoreNotFoundError(key)));

        public Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult(this.GetProperties?.Invoke(key) ?? Result<BlobInfo>.Success(new BlobInfo { Key = key }));

        public Task<Result<BlobInfo>> UpdatePropertiesAsync(BlobPropertiesUpdate update, CancellationToken cancellationToken = default) =>
            Task.FromResult(this.UpdateProperties?.Invoke(update) ?? Result<BlobInfo>.Success(new BlobInfo { Key = update.Key }));

        public Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            this.ExistsHandlerAsync is not null
                ? this.ExistsHandlerAsync(key, cancellationToken)
                : Task.FromResult(this.Exists?.Invoke(key) ?? Result<bool>.Success(true));

        public Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(this.List?.Invoke(query) ?? Result<BlobPage>.Success(new BlobPage()));

        public Task<Result> DeleteAsync(
            BlobKey key,
            BlobDeleteOptions options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(this.Delete?.Invoke(key) ?? Result.Success());
    }

    private sealed class NonSeekableReadStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentBag<string> messages = [];

        public IReadOnlyCollection<string> Messages => this.messages;

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this.messages);

        public void Dispose() { }
    }

    private sealed class RecordingLogger(ConcurrentBag<string> messages) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose() { }
    }

    private sealed class RecordingMetrics : IDisposable
    {
        private readonly MeterListener listener = new();
        private readonly ConcurrentDictionary<string, ConcurrentBag<long>> counters = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, ConcurrentBag<double>> histograms = new(StringComparer.Ordinal);
        private readonly ConcurrentBag<string> tagKeys = [];
        private readonly ConcurrentBag<string> tagValues = [];

        public RecordingMetrics()
        {
            this.listener.InstrumentPublished = (instrument, listener) =>
            {
                if (string.Equals(instrument.Meter.Name, Metrics.MeterName, StringComparison.Ordinal) &&
                    instrument.Name.StartsWith("blobstorage_", StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            this.listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                this.counters.GetOrAdd(instrument.Name, _ => []).Add(measurement);
                this.RecordTags(tags);
            });
            this.listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                this.histograms.GetOrAdd(instrument.Name, _ => []).Add(measurement);
                this.RecordTags(tags);
            });
            this.listener.Start();
        }

        public IReadOnlyCollection<string> AllTagKeys => this.tagKeys.ToArray();

        public IReadOnlyCollection<string> AllTagValues => this.tagValues.ToArray();

        public long CounterSum(string series) =>
            this.counters.TryGetValue(series, out var values) ? values.Sum() : 0;

        public int HistogramCount(string series) =>
            this.histograms.TryGetValue(series, out var values) ? values.Count : 0;

        public void Dispose() => this.listener.Dispose();

        private void RecordTags(ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            foreach (var tag in tags)
            {
                this.tagKeys.Add(tag.Key);
                this.tagValues.Add(tag.Value?.ToString() ?? string.Empty);
            }
        }
    }
}
