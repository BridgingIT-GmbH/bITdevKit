// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Diagnostics;
using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

[UnitTest("Application")]
public sealed class TracingBlobStoreClientBehaviorTests
{
    private static readonly BlobKey Key = new("sensitive-container", "sensitive-blob");

    [Fact]
    public async Task ExistsAsync_WithListener_RecordsSuccessfulOperation()
    {
        var stopped = new List<Activity>();
        using var listener = Listen(stopped);
        var inner = Substitute.For<IBlobStoreClient>();
        inner.ExistsAsync(Key).Returns(Task.FromResult(Result<bool>.Success(true)));
        var sut = new TracingBlobStoreClientBehavior(inner, "reports");

        var result = await sut.ExistsAsync(Key);

        result.Value.ShouldBeTrue();
        AssertActivity(stopped.Single(), "exists");
    }

    [Fact]
    public async Task UploadAsync_WithMultipleErrors_RecordsOnlyFirstErrorType()
    {
        var stopped = new List<Activity>();
        using var listener = Listen(stopped);
        var inner = Substitute.For<IBlobStoreClient>();
        var upload = new BlobUpload { Key = Key, Properties = new() { ["user-property"] = "sensitive-property" } };
        var error = new BlobStoreProviderError("sensitive-error");
        inner.UploadAsync(upload).Returns(Task.FromResult(Result<BlobInfo>.Failure(error).WithError(new BlobStoreConflictError("another sensitive error"))));
        var sut = new TracingBlobStoreClientBehavior(inner, "reports");

        var result = await sut.UploadAsync(upload);

        result.Errors.First().ShouldBeSameAs(error);
        AssertActivity(stopped.Single(), "upload", nameof(BlobStoreProviderError));
    }

    [Fact]
    public async Task ListPageAsync_WithThrownException_RecordsTypeAndRethrowsSameException()
    {
        var stopped = new List<Activity>();
        using var listener = Listen(stopped);
        var inner = Substitute.For<IBlobStoreClient>();
        var query = new BlobQuery { Container = Key.Container, ContinuationToken = "sensitive-continuation" };
        var failure = new InvalidOperationException("sensitive-exception");
        inner.ListPageAsync(query).Returns(Task.FromException<Result<BlobPage>>(failure));
        var sut = new TracingBlobStoreClientBehavior(inner, "reports");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => sut.ListPageAsync(query));

        thrown.ShouldBeSameAs(failure);
        AssertActivity(stopped.Single(), "list_page", nameof(InvalidOperationException));
    }

    [Fact]
    public async Task DeleteAsync_WithFailure_RecordsNonGenericResult()
    {
        var stopped = new List<Activity>();
        using var listener = Listen(stopped);
        var inner = Substitute.For<IBlobStoreClient>();
        inner.DeleteAsync(Key).Returns(Task.FromResult(Result.Failure(new BlobStoreNotFoundError(Key))));
        var sut = new TracingBlobStoreClientBehavior(inner, "reports");

        var result = await sut.DeleteAsync(Key);

        result.HasError<BlobStoreNotFoundError>().ShouldBeTrue();
        AssertActivity(stopped.Single(), "delete", nameof(BlobStoreNotFoundError));
    }

    [Fact]
    public async Task ExistsAsync_WithoutListener_ReturnsInnerTaskDirectly()
    {
        var inner = Substitute.For<IBlobStoreClient>();
        var completion = new TaskCompletionSource<Result<bool>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ambient = Activity.Current;
        inner.ExistsAsync(Key).Returns(completion.Task);
        var sut = new TracingBlobStoreClientBehavior(inner, "reports");

        var dispatched = sut.ExistsAsync(Key);

        dispatched.ShouldBeSameAs(completion.Task);
        Activity.Current.ShouldBe(ambient);
        completion.SetResult(Result<bool>.Success(true));
        (await dispatched).Value.ShouldBeTrue();
        await inner.Received(1).ExistsAsync(Key);
    }

    [Fact]
    public async Task ExistsAsync_WithRegisteredRetry_UsesOneActivityAcrossAttempts()
    {
        var stopped = new List<Activity>();
        using var listener = Listen(stopped);
        using var parent = new Activity("caller").Start();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = new List<Activity>();
        var provider = Substitute.For<IBlobStoreProvider>();
        provider.Capabilities.Returns(new BlobStoreProviderCapabilities());
        provider.ExistsAsync(Key, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                attempts.Add(Activity.Current);
                if (attempts.Count == 1)
                {
                    await release.Task;
                    return Result<bool>.Failure(new BlobStoreProviderError("temporary", isTransient: true));
                }

                return Result<bool>.Success(true);
            });
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithMetricsBehavior()
            .WithTracingBehavior()
            .WithTimeoutBehavior()
            .WithRetryBehavior(options => { options.Attempts = 2; options.Backoff = TimeSpan.Zero; })
            .WithUploadConcurrencyBehavior()
            .RegisterClient("reports", _ => provider);
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");

        var pending = sut.ExistsAsync(Key);
        try
        {
            Activity.Current.ShouldBeSameAs(parent);
        }
        finally
        {
            release.TrySetResult();
        }

        (await pending).Value.ShouldBeTrue();
        var activity = stopped.Single();
        attempts.ShouldBe([activity, activity]);
        AssertActivity(activity, "exists");
    }

    private static ActivityListener Listen(List<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BlobTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Add
        };
        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    private static void AssertActivity(Activity activity, string operation, string errorType = null)
    {
        activity.OperationName.ShouldBe($"blobs.{operation}");
        activity.Kind.ShouldBe(ActivityKind.Internal);
        activity.Status.ShouldBe(errorType is null ? ActivityStatusCode.Unset : ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe(errorType);
        activity.GetTagItem("blobs.operation").ShouldBe(operation);
        activity.GetTagItem("blobs.store").ShouldBe("reports");
        activity.TagObjects.Select(tag => tag.Key).ShouldBe(errorType is null
            ? ["blobs.operation", "blobs.store"]
            : ["blobs.operation", "blobs.store", "error.type"]);
        activity.GetTagItem("error.type").ShouldBe(errorType);
        activity.Events.ShouldBeEmpty();
    }
}
