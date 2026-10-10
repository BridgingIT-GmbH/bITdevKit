// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Profiling;

using System.Text.Json;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Queueing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Constants = BridgingIT.DevKit.Application.Messaging.Constants;

/// <summary>Checks optional handler capture, consumer ownership and executing-node queries against the shared recorder.</summary>
/// <example>Runs direct behaviors and real messaging/queueing broker pipelines.</example>
public sealed class BrokerHandlerProfilingBehaviorTests
{
    /// <summary>Checks publish metadata survives a durable JSON boundary and scopes nested handler work independently of the worker.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Broker_SerializedPublisherCorrelation_ProfilesAndPropagatesThroughHandler(
        bool queue,
        bool fail
    )
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var id = Guid.Empty;
        Func<Task> publishChild = null;
        IDictionary<string, object> childProperties = null;
        var handler = new Handler(
            async (_, _) =>
            {
                CorrelationId.Current.ShouldBe("publisher-correlation");
                id = profiling.Current.Id;
                await Task.Yield();
                CorrelationId.Current.ShouldBe("publisher-correlation");
                await publishChild();
                if (fail)
                {
                    throw new InvalidOperationException("handler failure");
                }
            }
        );

        if (queue)
        {
            var factory = Substitute.For<IQueueMessageHandlerFactory>();
            factory
                .Create(typeof(Handler))
                .Returns(_ => new QueueMessageHandlerFactoryResult(handler));
            var sut = new TestQueueBroker(factory, new QueueHandlerProfilingBehavior(profiling));
            await sut.Subscribe<TestQueueMessage, Handler>();
            var message = new TestQueueMessage();
            using (CorrelationId.BeginScope("publisher-correlation"))
            {
                await sut.Enqueue(message);
            }

            var restored = new TestQueueMessage { Properties = RoundTrip(message.Properties) };
            publishChild = async () =>
            {
                var child = new TestQueueMessage();
                await sut.Enqueue(child);
                childProperties = child.Properties;
            };
            using (CorrelationId.BeginScope("worker-correlation"))
            {
                QueueProcessingResult? result = null;
                await sut.Process(
                    new QueueMessageRequest(restored, value => result = value, default)
                );
                result.ShouldBe(
                    fail ? QueueProcessingResult.Failed : QueueProcessingResult.Succeeded
                );
                CorrelationId.Current.ShouldBe("worker-correlation");
            }
        }
        else
        {
            var factory = Substitute.For<IMessageHandlerFactory>();
            factory.Create(typeof(Handler)).Returns(_ => new MessageHandlerFactoryResult(handler));
            var sut = new TestMessageBroker(
                factory,
                new MessageHandlerProfilingBehavior(profiling)
            );
            await sut.Subscribe<TestMessage, Handler>();
            var message = new TestMessage();
            using (CorrelationId.BeginScope("publisher-correlation"))
            {
                await sut.Publish(message);
            }

            var restored = new TestMessage(RoundTrip(message.Properties));
            publishChild = async () =>
            {
                var child = new TestMessage();
                await sut.Publish(child);
                childProperties = child.Properties;
            };
            using (CorrelationId.BeginScope("worker-correlation"))
            {
                bool? result = null;
                await sut.Process(new MessageRequest(restored, value => result = value, default));
                result.ShouldBe(!fail);
                CorrelationId.Current.ShouldBe("worker-correlation");
            }
        }

        id.ShouldNotBe(Guid.Empty);
        var record = await Stored(services, id);
        record.CorrelationId.ShouldBe("publisher-correlation");
        record.Outcome.ShouldBe(
            fail ? OperationProfilingOutcome.Failed : OperationProfilingOutcome.Completed
        );
        childProperties[Constants.CorrelationIdKey].ShouldBe("publisher-correlation");
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Checks a JSON string in message metadata is accepted without arbitrary object stringification.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_JsonStringCorrelation_RecordsPublisherCorrelation(bool queue)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var properties = RoundTrip(
            new Dictionary<string, object> { [Constants.CorrelationIdKey] = "json-publisher" }
        );
        properties[Constants.CorrelationIdKey].ShouldBeOfType<JsonElement>();
        object message = queue
            ? new TestQueueMessage { Properties = properties }
            : new TestMessage(properties);
        var id = Guid.Empty;
        await Handle(
            queue,
            profiling,
            () =>
            {
                id = profiling.Current.Id;
                return Task.CompletedTask;
            },
            message: message
        );

        (await Stored(services, id)).CorrelationId.ShouldBe("json-publisher");
    }

    /// <summary>Checks publishing without an ambient scope assigns an ID before transport and preserves it for consumers.</summary>
    /// <example><code>await test.Broker_NewOrigin_AssignsShortCorrelationBeforePublishing(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Broker_NewOrigin_AssignsShortCorrelationBeforePublishing(bool queue)
    {
        using var services = await Services();
        using var origin = CorrelationId.BeginScope(null);
        var profiling = services.GetRequiredService<IOperationProfiler>();
        string identifier = null;
        var id = Guid.Empty;
        var handler = new Handler(
            (_, _) =>
            {
                CorrelationId.Current.ShouldBe(identifier);
                id = profiling.Current.Id;
                return Task.CompletedTask;
            }
        );
        if (queue)
        {
            var factory = Substitute.For<IQueueMessageHandlerFactory>();
            factory
                .Create(typeof(Handler))
                .Returns(_ => new QueueMessageHandlerFactoryResult(handler));
            var broker = new TestQueueBroker(factory, new QueueHandlerProfilingBehavior(profiling));
            await broker.Subscribe<TestQueueMessage, Handler>();
            var message = new TestQueueMessage();
            await broker.Enqueue(message);
            identifier = CorrelationId.ReadFrom(message.Properties);
            await broker.Process(
                new QueueMessageRequest(
                    new TestQueueMessage { Properties = RoundTrip(message.Properties) },
                    _ => { },
                    default
                )
            );
        }
        else
        {
            var factory = Substitute.For<IMessageHandlerFactory>();
            factory.Create(typeof(Handler)).Returns(_ => new MessageHandlerFactoryResult(handler));
            var broker = new TestMessageBroker(
                factory,
                new MessageHandlerProfilingBehavior(profiling)
            );
            await broker.Subscribe<TestMessage, Handler>();
            var message = new TestMessage();
            await broker.Publish(message);
            identifier = CorrelationId.ReadFrom(message.Properties);
            await broker.Process(
                new MessageRequest(
                    new TestMessage(RoundTrip(message.Properties)),
                    _ => { },
                    default
                )
            );
        }

        identifier.ShouldNotBeNullOrEmpty();
        identifier.Length.ShouldBe(12);
        (await Stored(services, id)).CorrelationId.ShouldBe(identifier);
        CorrelationId.Current.ShouldBeNull();
    }

    private static IDictionary<string, object> RoundTrip(IDictionary<string, object> properties) =>
        JsonSerializer.Deserialize<Dictionary<string, object>>(
            JsonSerializer.Serialize(properties)
        );

    /// <summary>Checks uncorrelated messages do not inherit a worker's unrelated Activity or ambient identifier.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Broker_UncorrelatedMessage_DoesNotInheritWorkerCorrelation(bool queue)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var activity = new System.Diagnostics.Activity("worker").Start();
        activity.SetBaggage(CorrelationId.ActivityBaggageName, "worker-activity-correlation");
        using var ambient = CorrelationId.BeginScope("worker-correlation");
        var id = Guid.Empty;
        string generatedCorrelation = null;
        var handler = new Handler(
            async (_, _) =>
            {
                await Task.Yield();
                generatedCorrelation = CorrelationId.Current;
                generatedCorrelation.ShouldNotBeNullOrEmpty();
                generatedCorrelation.Length.ShouldBe(12);
                generatedCorrelation.ShouldNotBe("worker-correlation");
                generatedCorrelation.ShouldNotBe("worker-activity-correlation");
                id = profiling.Current.Id;
            }
        );
        if (queue)
        {
            var factory = Substitute.For<IQueueMessageHandlerFactory>();
            factory
                .Create(typeof(Handler))
                .Returns(_ => new QueueMessageHandlerFactoryResult(handler));
            var sut = new TestQueueBroker(factory, new QueueHandlerProfilingBehavior(profiling));
            await sut.Subscribe<TestQueueMessage, Handler>();
            QueueProcessingResult? result = null;
            await sut.Process(
                new QueueMessageRequest(new TestQueueMessage(), value => result = value, default)
            );
            result.ShouldBe(QueueProcessingResult.Succeeded);
        }
        else
        {
            var factory = Substitute.For<IMessageHandlerFactory>();
            factory.Create(typeof(Handler)).Returns(_ => new MessageHandlerFactoryResult(handler));
            var sut = new TestMessageBroker(
                factory,
                new MessageHandlerProfilingBehavior(profiling)
            );
            await sut.Subscribe<TestMessage, Handler>();
            bool? result = null;
            await sut.Process(
                new MessageRequest(new TestMessage(), value => result = value, default)
            );
            result.ShouldBe(true);
        }

        id.ShouldNotBe(Guid.Empty);
        (await Stored(services, id)).CorrelationId.ShouldBe(generatedCorrelation);
        CorrelationId.Current.ShouldBe("worker-correlation");
    }

    /// <summary>Checks typed/instance registration is idempotent without hidden profiling dependencies.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registration_OmittedProfiling_PreservesOriginalTask(bool queue)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        if (queue)
        {
            collection
                .AddQueueing()
                .WithBehavior<QueueHandlerProfilingBehavior>()
                .WithBehavior<QueueHandlerProfilingBehavior>()
                .WithBehavior(new QueueHandlerProfilingBehavior());
        }
        else
        {
            collection
                .AddMessaging()
                .WithBehavior<MessageHandlerProfilingBehavior>()
                .WithBehavior<MessageHandlerProfilingBehavior>()
                .WithBehavior(new MessageHandlerProfilingBehavior());
        }

        using var services = collection.BuildServiceProvider();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task Next()
        {
            calls++;
            return pending.Task;
        }

        Task actual;
        if (queue)
        {
            var behavior = services.GetServices<IQueueHandlerBehavior>().ShouldHaveSingleItem();
            actual = behavior.Handle(new TestQueueMessage(), default, new Handler(), Next);
        }
        else
        {
            var behavior = services.GetServices<IMessageHandlerBehavior>().ShouldHaveSingleItem();
            actual = behavior.Handle(new TestMessage(), default, new Handler(), Next);
        }

        actual.ShouldBeSameAs(pending.Task);
        calls.ShouldBe(1);
        pending.SetResult();
        await actual;
        services.GetService<IOperationProfiler>().ShouldBeNull();
        services.GetService<IProfilingStorageProvider>().ShouldBeNull();
        services.GetService<OperationProfilingWriterService>().ShouldBeNull();
    }

    /// <summary>Checks independent roots contain bounded type metadata and the executing process, not a producer node.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_IndependentCall_RecordsConsumerNodeAndSupportsFiltering(bool queue)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var producerNodeId = Guid.NewGuid();
        var properties = new Dictionary<string, object>
        {
            [Constants.CorrelationIdKey] = "producer-correlation",
            ["nodeId"] = producerNodeId,
            ["secret"] = "private message payload",
        };
        object message = queue
            ? new TestQueueMessage { Properties = properties }
            : new TestMessage(properties);
        var id = Guid.Empty;
        var calls = 0;
        await Handle(
            queue,
            profiling,
            () =>
            {
                id = profiling.Current.Id;
                calls++;
                return Task.CompletedTask;
            },
            message: message
        );

        calls.ShouldBe(1);
        var record = await Stored(services, id);
        record.Key.ShouldBe(Key(queue));
        record.Kind.ShouldBe(queue ? "QueueHandler" : "MessageHandler");
        record.CorrelationId.ShouldBe("producer-correlation");
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record.Node.ShouldBe(
            services.GetRequiredService<IProfilingNodeIdentityProvider>().GetNode()
        );
        record.Node.Identity.Id.ShouldNotBe(producerNodeId);
        record.Node.HostName.ShouldNotBeNullOrWhiteSpace();
        record.Node.ProcessId.ShouldBe(Environment.ProcessId);
        record.Node.ProcessStartedUtc.Offset.ShouldBe(TimeSpan.Zero);
        record
            .Dimensions.Single(d => d.Key == Prefix(queue) + ".messageType")
            .Value.Scalar.ShouldBe(
                (queue ? typeof(TestQueueMessage) : typeof(TestMessage)).FullName
            );
        record
            .Dimensions.Single(d => d.Key == Prefix(queue) + ".handlerType")
            .Value.Scalar.ShouldBe(typeof(Handler).FullName);
        record.Dimensions.ShouldNotContain(d => d.Value.Scalar.Contains("private"));
        var queries = services.GetRequiredService<IOperationProfilingQueryService>();
        var consumer = await queries.QueryAsync(new() { NodeId = record.Node.Identity.Id });
        consumer.IsSuccess.ShouldBeTrue();
        consumer.Value.Records.ShouldHaveSingleItem().Id.ShouldBe(id);
        var producer = await queries.QueryAsync(new() { NodeId = producerNodeId });
        producer.IsSuccess.ShouldBeTrue();
        producer.Value.Records.ShouldBeEmpty();
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Checks repeated parallel invocations and nested segments aggregate without taking ownership of the caller.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_JoinedParallelCalls_PreservesOwnerAndStructuredPaths(bool queue)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var owner = profiling.BeginOperation("caller");
        using var dispatch = profiling.BeginSegment("Dispatch");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var tasks = Enumerable
            .Range(0, 3)
            .Select(_ =>
                Handle(
                    queue,
                    profiling,
                    async () =>
                    {
                        Interlocked.Increment(ref calls);
                        await profiling.RunSegmentAsync("Read", async (_, _) => await gate.Task);
                    }
                )
            )
            .ToArray();
        calls.ShouldBe(3);
        gate.SetResult();
        await Task.WhenAll(tasks);
        profiling.Current.ShouldBeSameAs(owner);
        owner.IsRecording.ShouldBeTrue();
        dispatch.Complete();
        dispatch.Dispose();
        owner.Complete();
        owner.Dispose();

        var record = await Stored(services, owner.Id);
        record.Segments.Count.ShouldBe(3);
        record.Segments.Single(s => s.Key == Key(queue)).Statistics.Count.ShouldBe(3);
        var inner = record.Segments.Single(s => s.Key == "Read");
        inner.Path.Components.ShouldBe(["Dispatch", Key(queue), "Read"]);
        inner.Statistics.Count.ShouldBe(3);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
    }

    /// <summary>Checks omitted capture and null messages leave business exceptions and task identity untouched.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_NullMessage_ReturnsOriginalBusinessTask(bool queue)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var task = Task.CompletedTask;
        var actual = queue
            ? new QueueHandlerProfilingBehavior(profiling).Handle(null, default, null, () => task)
            : new MessageHandlerProfilingBehavior(profiling).Handle<IMessage>(
                null,
                default,
                null,
                () => task
            );
        actual.ShouldBeSameAs(task);
        await actual;
        services
            .GetRequiredService<OperationProfilingCompletionQueue>()
            .Snapshot()
            .Count.ShouldBe(0);
    }

    /// <summary>Checks suppressed or disabled direct behavior execution creates no replacement root.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Handle_SuppressedOrDisabled_ExecutesOnceWithoutCapture(
        bool queue,
        bool disabled
    )
    {
        using var services = await Services(!disabled);
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var suppression = disabled ? null : profiling.Suppress();
        var calls = 0;
        await Handle(
            queue,
            profiling,
            () =>
            {
                calls++;
                profiling.Current.IsRecording.ShouldBeFalse();
                return Task.CompletedTask;
            }
        );
        calls.ShouldBe(1);
        services
            .GetRequiredService<OperationProfilingCompletionQueue>()
            .Snapshot()
            .Count.ShouldBe(0);
    }

    /// <summary>Checks synchronous and asynchronous handler failures are rethrown unchanged and sanitized in capture.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Handle_ThrownException_PreservesIdentityAndRestoresContext(
        bool queue,
        bool asynchronous
    )
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var expected = new InvalidOperationException("private failure details");
        var id = Guid.Empty;
        var calls = 0;
        var actual = await Should.ThrowAsync<InvalidOperationException>(() =>
            Handle(
                queue,
                profiling,
                () =>
                {
                    id = profiling.Current.Id;
                    calls++;
                    return asynchronous ? Task.FromException(expected) : throw expected;
                }
            )
        );
        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
        var record = await Stored(services, id);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        record.Failure.Message.ShouldBeNull();
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Checks only requested cancellation carrying the handler token is classified as canceled.</summary>
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task Handle_Cancellation_PreservesExceptionAndClassifiesMatchingToken(
        bool queue,
        bool canceled,
        bool matches
    )
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var caller = new CancellationTokenSource();
        using var other = new CancellationTokenSource();
        if (canceled)
        {
            caller.Cancel();
        }

        other.Cancel();
        var expected = new OperationCanceledException(matches ? caller.Token : other.Token);
        var id = Guid.Empty;
        OperationCanceledException actual = null;
        try
        {
            await Handle(
                queue,
                profiling,
                () =>
                {
                    id = profiling.Current.Id;
                    return Task.FromException(expected);
                },
                caller.Token
            );
        }
        catch (OperationCanceledException exception)
        {
            actual = exception;
        }

        actual.ShouldBeSameAs(expected);
        (await Stored(services, id)).Outcome.ShouldBe(
            canceled && matches
                ? OperationProfilingOutcome.Canceled
                : OperationProfilingOutcome.Failed
        );
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Checks arbitrary message metadata is never stringified and oversized correlation identifiers do not suppress capture.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Handle_UnusableCorrelationMetadata_DoesNotChangeExecution(
        bool queue,
        bool oversized
    )
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var properties = new Dictionary<string, object>
        {
            [Constants.CorrelationIdKey] = oversized
                ? new string('x', 129)
                : new UnprintableValue(),
        };
        object message = queue
            ? new TestQueueMessage { Properties = properties }
            : new TestMessage(properties);
        var id = Guid.Empty;
        await Handle(
            queue,
            profiling,
            () =>
            {
                id = profiling.Current.Id;
                return Task.CompletedTask;
            },
            message: message
        );
        (await Stored(services, id)).CorrelationId.ShouldBeNull();
    }

    /// <summary>Checks profiling startup, metadata, completion, failure and restoration faults never replace business execution.</summary>
    [Theory]
    [InlineData(false, "current")]
    [InlineData(false, "start")]
    [InlineData(false, "metadata")]
    [InlineData(false, "complete")]
    [InlineData(false, "fail")]
    [InlineData(false, "cancel")]
    [InlineData(false, "dispose")]
    [InlineData(false, "boundary-start")]
    [InlineData(false, "boundary-dispose")]
    [InlineData(true, "current")]
    [InlineData(true, "start")]
    [InlineData(true, "metadata")]
    [InlineData(true, "complete")]
    [InlineData(true, "fail")]
    [InlineData(true, "cancel")]
    [InlineData(true, "dispose")]
    [InlineData(true, "boundary-start")]
    [InlineData(true, "boundary-dispose")]
    public async Task Handle_ObservationFault_PreservesExactlyOnceBusinessWork(
        bool queue,
        string stage
    )
    {
        var profiling = Substitute.For<IOperationProfiler>();
        var scope = Substitute.For<IProfilingOperationScope>();
        scope.IsRecording.Returns(true);
        profiling.BeginOperation(Arg.Any<OperationProfilingStartRequest>()).Returns(scope);
        var fault = new InvalidOperationException("profiling fault");
        switch (stage)
        {
            case "current":
                profiling.Current.Returns(_ => throw fault);
                break;
            case "start":
                profiling
                    .BeginOperation(Arg.Any<OperationProfilingStartRequest>())
                    .Returns(_ => throw fault);
                break;
            case "metadata":
                scope
                    .When(s => s.SetDimension(Arg.Any<string>(), Arg.Any<object>()))
                    .Do(_ => throw fault);
                break;
            case "complete":
                scope.When(s => s.Complete()).Do(_ => throw fault);
                break;
            case "fail":
                scope.When(s => s.Fail(Arg.Any<Exception>())).Do(_ => throw fault);
                break;
            case "cancel":
                scope.When(s => s.Cancel()).Do(_ => throw fault);
                break;
            case "dispose":
                scope.When(s => s.Dispose()).Do(_ => throw fault);
                break;
            case "boundary-start":
                profiling.BeginExecutionBoundary().Returns(_ => throw fault);
                break;
            case "boundary-dispose":
                var restoration = Substitute.For<IDisposable>();
                restoration.When(s => s.Dispose()).Do(_ => throw fault);
                profiling.BeginExecutionBoundary().Returns(restoration);
                break;
        }

        IProfilingExecutionBoundaryBehavior boundaryBehavior = queue
            ? new QueueHandlerProfilingBehavior(profiling)
            : new MessageHandlerProfilingBehavior(profiling);
        using var boundary = boundaryBehavior.BeginExecutionBoundary();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Exception expected =
            stage == "cancel" ? new OperationCanceledException(cancellation.Token)
            : stage == "fail" ? new InvalidOperationException("business failure")
            : null;
        var calls = 0;
        Exception actual = null;
        try
        {
            await Handle(
                queue,
                profiling,
                () =>
                {
                    calls++;
                    return expected is null ? Task.CompletedTask : Task.FromException(expected);
                },
                cancellation.Token
            );
        }
        catch (Exception exception)
        {
            actual = exception;
        }

        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
    }

    /// <summary>Checks both broker bases isolate handler pipelines even when invoked beneath a producer or suppressed flow.</summary>
    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(false, 2, false)]
    [InlineData(false, 0, true)]
    [InlineData(false, 1, true)]
    [InlineData(false, 2, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, false)]
    [InlineData(true, 2, false)]
    [InlineData(true, 0, true)]
    [InlineData(true, 1, true)]
    [InlineData(true, 2, true)]
    public async Task Broker_Processing_IsolatesConsumerAndPreservesCompletion(
        bool queue,
        int parent,
        bool fail
    )
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var owner = parent == 1 ? profiling.BeginOperation("producer") : null;
        using var suppression = parent == 2 ? profiling.Suppress() : null;
        var inherited = profiling.Current;
        using var cancellation = new CancellationTokenSource();
        var id = Guid.Empty;
        object expectedMessage = queue ? new TestQueueMessage() : new TestMessage();
        var calls = 0;
        var handler = new Handler(
            async (message, token) =>
            {
                message.ShouldBeSameAs(expectedMessage);
                token.ShouldBe(cancellation.Token);
                id = profiling.Current.Id;
                id.ShouldNotBe(Guid.Empty);
                id.ShouldNotBe(owner?.Id ?? Guid.Empty);
                calls++;
                await profiling.RunSegmentAsync(
                    "Read",
                    (_, _) =>
                        fail
                            ? Task.FromException(
                                new InvalidOperationException("private handler failure")
                            )
                            : Task.CompletedTask
                );
            }
        );
        if (queue)
        {
            var factory = Substitute.For<IQueueMessageHandlerFactory>();
            factory
                .Create(typeof(Handler))
                .Returns(_ => new QueueMessageHandlerFactoryResult(handler));
            var broker = new TestQueueBroker(factory, new QueueHandlerProfilingBehavior(profiling));
            await broker.Subscribe<TestQueueMessage, Handler>();
            QueueProcessingResult? result = null;
            await broker.Process(
                new QueueMessageRequest(
                    (IQueueMessage)expectedMessage,
                    value => result = value,
                    cancellation.Token
                )
            );
            result.ShouldBe(fail ? QueueProcessingResult.Failed : QueueProcessingResult.Succeeded);
        }
        else
        {
            var factory = Substitute.For<IMessageHandlerFactory>();
            factory.Create(typeof(Handler)).Returns(_ => new MessageHandlerFactoryResult(handler));
            var broker = new TestMessageBroker(
                factory,
                new MessageHandlerProfilingBehavior(profiling)
            );
            await broker.Subscribe<TestMessage, Handler>();
            bool? result = null;
            await broker.Process(
                new MessageRequest(
                    (IMessage)expectedMessage,
                    value => result = value,
                    cancellation.Token
                )
            );
            result.ShouldBe(!fail);
        }

        calls.ShouldBe(1);
        profiling.Current.ShouldBeSameAs(inherited);
        var record = await Stored(services, id);
        record.Kind.ShouldBe(queue ? "QueueHandler" : "MessageHandler");
        record.Outcome.ShouldBe(
            fail ? OperationProfilingOutcome.Failed : OperationProfilingOutcome.Completed
        );
        record.Segments.ShouldHaveSingleItem().Key.ShouldBe("Read");
        record.Node.ShouldBe(
            services.GetRequiredService<IProfilingNodeIdentityProvider>().GetNode()
        );
        if (owner is not null)
        {
            owner.Complete();
            owner.Dispose();
            (await Stored(services, owner.Id)).Segments.ShouldBeEmpty();
        }
    }

    /// <summary>Checks consumers created in a producer flow can finish after the producer's capture closes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackgroundBroker_ConsumerOutlivesProducer_OwnsIndependentOperation(bool queue)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var owner = profiling.BeginOperation("producer");
        var entered = new TaskCompletionSource<Guid>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler(
            async (_, _) =>
            {
                entered.SetResult(profiling.Current.Id);
                await release.Task;
            }
        );
        IDisposable disposable = null;
        Task processing;
        if (queue)
        {
            var factory = Substitute.For<IQueueMessageHandlerFactory>();
            factory
                .Create(typeof(Handler))
                .Returns(_ => new QueueMessageHandlerFactoryResult(
                    handler,
                    () =>
                    {
                        finished.TrySetResult();
                        return ValueTask.CompletedTask;
                    }
                ));
            var broker = new InProcessQueueBroker(
                new()
                {
                    HandlerFactory = factory,
                    HandlerBehaviors = [new QueueHandlerProfilingBehavior(profiling)],
                    ProcessDelay = 0,
                }
            );
            disposable = broker;
            await broker.Subscribe<TestQueueMessage, Handler>();
            await broker.Enqueue(new TestQueueMessage());
            processing = finished.Task;
        }
        else
        {
            var factory = Substitute.For<IMessageHandlerFactory>();
            factory.Create(typeof(Handler)).Returns(_ => new MessageHandlerFactoryResult(handler));
            var broker = new InProcessMessageBroker(
                new InProcessMessageBrokerOptions
                {
                    HandlerFactory = factory,
                    HandlerBehaviors = [new MessageHandlerProfilingBehavior(profiling)],
                    ProcessDelay = 0,
                }
            );
            await broker.Subscribe<TestMessage, Handler>();
            processing = broker.Publish(new TestMessage());
        }

        try
        {
            var id = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            id.ShouldNotBe(Guid.Empty);
            id.ShouldNotBe(owner.Id);
            profiling.Current.ShouldBeSameAs(owner);
            owner.Complete();
            owner.Dispose();
            release.SetResult();
            await processing.WaitAsync(TimeSpan.FromSeconds(5));
            (await Stored(services, id)).Outcome.ShouldBe(OperationProfilingOutcome.Completed);
            (await Stored(services, owner.Id)).Segments.ShouldBeEmpty();
        }
        finally
        {
            release.TrySetResult();
            disposable?.Dispose();
        }
    }

    private static string Prefix(bool queue) => queue ? "queueing" : "messaging";

    private static string Key(bool queue) =>
        Prefix(queue)
        + ":"
        + (queue ? nameof(TestQueueMessage) : nameof(TestMessage))
        + ":handler:"
        + nameof(Handler);

    private static Task Handle(
        bool queue,
        IOperationProfiler profiling,
        Func<Task> next,
        CancellationToken token = default,
        object message = null
    ) =>
        queue
            ? new QueueHandlerProfilingBehavior(profiling).Handle(
                (IQueueMessage)(message ?? new TestQueueMessage()),
                token,
                new Handler(),
                () => next()
            )
            : new MessageHandlerProfilingBehavior(profiling).Handle(
                (IMessage)(message ?? new TestMessage()),
                token,
                new Handler(),
                () => next()
            );

    private static async Task<ServiceProvider> Services(bool enabled = true)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddProfiling(o => o.Enabled(enabled)).WithOperationProfiling();
        var services = collection.BuildServiceProvider();
        if (enabled)
        {
            await services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        }

        return services;
    }

    private static async Task<OperationProfilingRecord> Stored(IServiceProvider services, Guid id)
    {
        await services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        return (
            await services.GetRequiredService<IOperationProfilingStore>().FindAsync(id)
        ).Value.ShouldNotBeNull();
    }

    private sealed class TestMessage : MessageBase
    {
        /// <summary>Creates a test message with optional producer metadata.</summary>
        /// <example><code>var message = new TestMessage();</code></example>
        public TestMessage(IDictionary<string, object> properties = null)
        {
            if (properties is not null)
            {
                this.Properties = properties;
            }
        }
    }

    private sealed class TestQueueMessage : QueueMessageBase;

    private sealed class UnprintableValue
    {
        /// <summary>Rejects unsafe metadata stringification.</summary>
        /// <example><code>value.ToString(); // throws</code></example>
        public override string ToString() =>
            throw new InvalidOperationException("do not inspect payloads");
    }

    private sealed class Handler(Func<object, CancellationToken, Task> next = null)
        : IMessageHandler<TestMessage>,
            IQueueMessageHandler<TestQueueMessage>
    {
        /// <summary>Forwards the exact message instance and application token.</summary>
        /// <example><code>await handler.Handle(message, token);</code></example>
        public Task Handle(TestMessage message, CancellationToken cancellationToken) =>
            next?.Invoke(message, cancellationToken) ?? Task.CompletedTask;

        /// <summary>Forwards the exact queue message instance and application token.</summary>
        /// <example><code>await handler.Handle(message, token);</code></example>
        public Task Handle(TestQueueMessage message, CancellationToken cancellationToken) =>
            next?.Invoke(message, cancellationToken) ?? Task.CompletedTask;
    }

    private sealed class TestMessageBroker(
        IMessageHandlerFactory factory,
        IMessageHandlerBehavior behavior
    ) : MessageBrokerBase(NullLoggerFactory.Instance, factory, handlerBehaviors: [behavior])
    {
        /// <inheritdoc />
        protected override Task OnPublish(IMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TestQueueBroker(
        IQueueMessageHandlerFactory factory,
        IQueueHandlerBehavior behavior
    ) : QueueBrokerBase(NullLoggerFactory.Instance, factory, handlerBehaviors: [behavior])
    {
        /// <inheritdoc />
        protected override Task OnEnqueue(
            IQueueMessage message,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;
    }
}
