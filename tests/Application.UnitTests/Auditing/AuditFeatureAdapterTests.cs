// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using System.Text;
using Application.Auditing;
using Application.Orchestrations;
using Application.Storage;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

public sealed class AuditFeatureAdapterTests
{
    [Fact]
    public async Task Pipeline_RetryAndSkip_PreservesActualAttemptsAndDistinctOccurrences()
    {
        var services = Services();
        services.AddPipelines().WithPipeline<ImportContext>("CustomerImport", definition => definition
            .AddBehavior<PipelineAuditingBehavior>().AddBehavior<PipelineAuditingBehavior>()
            .AddStep<RetryStep>().AddStep(() => { }, "Skipped", enabled: false));
        await using var host = services.BuildServiceProvider();
        var context = new ImportContext();
        var result = await host.GetRequiredService<IPipelineFactory>().Create<ImportContext>("CustomerImport").ExecuteAsync(context);
        result.IsSuccess.ShouldBeTrue();
        context.Attempts.ShouldBe(2);
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Kind.ShouldBe("Pipeline");
        record.Actions.Count(action => action.Kind == "PipelineStep").ShouldBe(2);
        record.Actions.Select(action => action.Attempt).ShouldBe(new int?[] { 1, 2 });
        record.Actions.Select(action => action.ExecutionStatus).ShouldBe(new[] { "Retry", "Continue" });
        record.Actions.Select(action => action.Id).Distinct().Count().ShouldBe(2);
        record.Outcome.ShouldBe(AuditOutcome.Succeeded);
        AssertSafe(record);
    }

    [Fact]
    public async Task Pipeline_ValidationFailure_CapturesAdmissionWithoutInventingAStep()
    {
        var services = Services();
        services.AddScoped<IValidator<ImportContext>, ImportValidator>();
        services.AddPipelines().WithPipeline<ImportContext>("Rejected", definition => definition.AddBehavior<PipelineAuditingBehavior>().AddStep<RetryStep>());
        await using var host = services.BuildServiceProvider();
        var context = new ImportContext { Reject = true };
        (await host.GetRequiredService<IPipelineFactory>().Create<ImportContext>("Rejected").ExecuteAsync(context)).IsFailure.ShouldBeTrue();
        context.Attempts.ShouldBe(0);
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(AuditOutcome.Rejected);
        record.Actions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("FileStorage")]
    [InlineData("BlobStorage")]
    [InlineData("DocumentStorage")]
    public async Task Storage_Failure_PreservesResultAndExcludesPathsKeysAndValues(string feature)
    {
        await using var host = Services().BuildServiceProvider();
        var scopes = host.GetRequiredService<IServiceScopeFactory>();
        var expected = Result.Failure(new Error("private-error"));
        Task<Result> result;
        switch (feature)
        {
            case "FileStorage":
                var file = Substitute.For<IFileStorageProvider>();
                file.LocationName.Returns("exports");
                file.DeleteFileAsync("private-path", null, default).Returns(expected);
                result = new AuditingFileStorageBehavior(file, scopes).DeleteFileAsync("private-path");
                break;
            case "BlobStorage":
                var blob = Substitute.For<IBlobStoreClient>();
                blob.DeleteAsync(Arg.Any<BlobKey>(), null, default).Returns(expected);
                result = new AuditingBlobStoreClientBehavior(blob, scopes, "exports").DeleteAsync(new("private-container", "private-key"));
                break;
            default:
                var documents = Substitute.For<IDocumentStoreClient<Customer>>();
                documents.DeleteAsync(Arg.Any<DocumentKey>(), null, default).Returns(expected);
                result = new AuditingDocumentStoreClientBehavior<Customer>(documents, scopes).DeleteAsync(new("private-partition", "private-key"));
                break;
        }

        (await result).ShouldBe(expected);
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Kind.ShouldBe(feature);
        record.Outcome.ShouldBe(AuditOutcome.Failed);
        AssertSafe(record);
    }

    [Fact]
    public async Task Storage_ReadsAreOptInAndReturnedStreamsAreUnchanged()
    {
        await using var host = Services().BuildServiceProvider();
        var inner = Substitute.For<IFileStorageProvider>();
        using var stream = new UnreadStream();
        inner.ReadFileAsync(Arg.Any<string>(), null, default).Returns(Result<Stream>.Success(stream));
        var scopes = host.GetRequiredService<IServiceScopeFactory>();
        (await new AuditingFileStorageBehavior(inner, scopes).ReadFileAsync("private-path")).Value.ShouldBeSameAs(stream);
        (await Records(host)).ShouldBeEmpty();
        (await new AuditingFileStorageBehavior(inner, scopes, new() { SelectRead = operation => operation == "read" }).ReadFileAsync("private-path")).Value.ShouldBeSameAs(stream);
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Details["source.executionStatus"].Data.GetString().ShouldBe("Acquired");
        AssertSafe(record);
    }

    [Fact]
    public async Task Document_PartialBatch_IsFailedDespiteSuccessfulOuterResult()
    {
        await using var host = Services().BuildServiceProvider();
        var inner = Substitute.For<IDocumentStoreClient<Customer>>();
        var expected = Result<DocumentBatchResult<DocumentKey>>.Success(new() { FailedKeys = [new("private", "private")] });
        inner.DeleteManyAsync(Arg.Any<IReadOnlyCollection<DocumentDelete>>(), default).Returns(expected);
        var sut = new AuditingDocumentStoreClientBehavior<Customer>(inner, host.GetRequiredService<IServiceScopeFactory>());
        (await sut.DeleteManyAsync([])).ShouldBe(expected);
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(AuditOutcome.Failed);
        record.Details["source.executionStatus"].Data.GetString().ShouldBe("PartialBatch");
        AssertSafe(record);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Document_GetMany_RespectsReadSelectionAndPartialResults(bool selected, bool partial)
    {
        await using var host = Services().BuildServiceProvider();
        var inner = Substitute.For<IDocumentStoreClient<Customer>>();
        IReadOnlyList<DocumentKey> keys = [new("private", "first"), new("private", "second")];
        var success = Result<DocumentEntry<Customer>>.Success(new() { Key = keys[0], Value = new() });
        var expected = Result<IReadOnlyList<Result<DocumentEntry<Customer>>>>.Success(
            [success, partial ? Result<DocumentEntry<Customer>>.Failure() : success]);
        inner.GetManyAsync(keys, default).Returns(expected);
        var sut = new AuditingDocumentStoreClientBehavior<Customer>(inner, host.GetRequiredService<IServiceScopeFactory>(),
            new() { SelectRead = operation => selected && operation == "get-many" });
        (await sut.GetManyAsync(keys)).ShouldBe(expected);
        await inner.Received(1).GetManyAsync(keys, default);
        var records = await Records(host);
        if (!selected) { records.ShouldBeEmpty(); return; }

        var record = records.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(partial ? AuditOutcome.Failed : AuditOutcome.Succeeded);
        record.Details["storage.operation"].Data.GetString().ShouldBe("get-many");
        if (partial) record.Details["source.executionStatus"].Data.GetString().ShouldBe("PartialBatch");
        AssertSafe(record);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Storage_BuildersKeepOneOuterOccurrenceIncludingRejectedInput(bool auditFirst)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (auditFirst) services.AddAuditing();
        services.AddBlobStorage().WithAuditing().WithAuditing().WithBehavior<AuditingBlobStoreClientBehavior>().WithInMemoryClient("exports");
        services.AddDocumentStorage().WithAuditing<Customer>().WithAuditing<Customer>().WithBehavior<AuditingDocumentStoreClientBehavior<Customer>>()
            .RegisterProvider<Customer>(_ => new InMemoryDocumentStoreProvider());
        services.AddFileStorage(factory => factory.RegisterProvider("exports", builder => builder.UseInMemory("exports").WithAuditing().WithAuditing()));
        if (!auditFirst) services.AddAuditing();
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = host.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredKeyedService<IBlobStoreClient>("exports").DeleteAsync(new("", ""))).IsFailure.ShouldBeTrue();
        (await scope.ServiceProvider.GetRequiredService<IDocumentStoreClient<Customer>>().DeleteAsync(new("", ""))).IsFailure.ShouldBeTrue();
        (await host.GetRequiredService<IFileStorageProviderFactory>().CreateProvider("exports").DeleteFileAsync("private-missing")).IsFailure.ShouldBeTrue();
        var records = await Records(host);
        records.Length.ShouldBe(3);
        records.ShouldAllBe(record => record.Actions.IsEmpty);
        records.Select(record => record.Kind).Distinct().Count().ShouldBe(3);
        records.ShouldAllBe(record => record.Outcome == AuditOutcome.Failed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoOpOrDisabled_SkipsReadSelectorsAndResourceProjections(bool disabled)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (disabled) services.AddAuditing(options => options.Enabled(false));
        await using var host = services.BuildServiceProvider();
        var options = new StorageAuditOptions { SelectRead = _ => throw new InvalidOperationException("selector"), Resource = (_, _, _) => throw new InvalidOperationException("resource") };
        var inner = Substitute.For<IFileStorageProvider>();
        var expected = Result.Success();
        inner.FileExistsAsync("private-path", null, default).Returns(expected);
        (await new AuditingFileStorageBehavior(inner, host.GetRequiredService<IServiceScopeFactory>(), options).FileExistsAsync("private-path")).ShouldBe(expected);
        if (disabled) (await Records(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Orchestration_Retry_RecordsEachActualAttemptWithinOneFiniteSlice()
    {
        await using var host = Orchestrations();
        var result = await host.GetRequiredService<IOrchestrationService>().ExecuteAsync<RetryWorkflow, Data>(new());
        result.IsSuccess.ShouldBeTrue();
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Kind.ShouldBe("Orchestration");
        record.Details["source.executionStatus"].Data.GetString().ShouldBe("Completed");
        record.Actions.Select(action => action.Attempt).ShouldBe(new int?[] { 1, 2 });
        record.Actions.Select(action => action.ExecutionStatus).ShouldBe(new[] { "Retry", "Continue" });
        AssertSafe(record);
    }

    [Fact]
    public async Task Orchestration_DurableWait_ClosesAndLinksIndependentResume()
    {
        await using var host = Orchestrations();
        var service = host.GetRequiredService<IOrchestrationService>();
        var firstResult = await host.GetRequiredService<IOrchestrationExecutor>().ExecuteAsync<WaitingWorkflow, Data>(new(), CancellationToken.None);
        var first = (await Records(host)).ShouldHaveSingleItem();
        first.Details["source.executionStatus"].Data.GetString().ShouldBe("Waiting");
        await using var caller = host.CreateAsyncScope();
        var audit = caller.ServiceProvider.GetRequiredService<IAuditContext>();
        var parent = audit.BeginScope(builder => builder.Key("unrelated-caller"));
        (await service.SignalAsync(firstResult.InstanceId, "Approve")).IsSuccess.ShouldBeTrue();
        var resumed = (await Records(host)).Single(record => record.AuditId != first.AuditId);
        resumed.AuditId.ShouldNotBe(parent.AuditId);
        resumed.CausationAuditId.ShouldBe(first.AuditId);
        resumed.CorrelationId.ShouldBe(first.CorrelationId);
        resumed.Actor.Kind.ShouldBe("Service");
        resumed.Details["source.executionStatus"].Data.GetString().ShouldBe("Completed");
        resumed.Actions.ShouldHaveSingleItem().ExecutionStatus.ShouldBe("Continue");
        parent.Complete();
        await parent.DisposeAsync();
        AssertSafe(resumed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pipeline_BackgroundWork_IsIsolatedEvenWhenPipelineCaptureIsOmitted(bool selected)
    {
        var services = Services();
        services.AddPipelines().WithPipeline<ImportContext>("Background", definition => definition.AddBehavior<PipelineAuditingBehavior>(selected).AddStep<NestedAuditStep>());
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var caller = host.CreateAsyncScope();
        var audit = caller.ServiceProvider.GetRequiredService<IAuditContext>();
        var parent = audit.BeginScope(builder => builder.Key("customers.submit"));
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await host.GetRequiredService<IPipelineFactory>().Create<ImportContext>("Background").ExecuteAndForgetAsync(new ImportContext(),
            options => options.WhenCompleted(_ => { completed.TrySetResult(); return ValueTask.CompletedTask; }));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var worker = (await Records(host)).ShouldHaveSingleItem();
        worker.AuditId.ShouldNotBe(parent.AuditId);
        worker.Actor.Kind.ShouldBe("Service");
        worker.Kind.ShouldBe(selected ? "Pipeline" : "Business");
        worker.CausationAuditId.ShouldBe(selected ? parent.AuditId : null);
        parent.Complete();
        await parent.DisposeAsync();
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuditing();
        return services;
    }
    private static ServiceProvider Orchestrations()
    {
        var services = Services();
        services.AddSingleton(new OrchestrationExecutionSettings { EnableBackgroundExecution = false });
        services.AddOrchestrations().AliveEnabled(false).WithAuditingBehavior().WithAuditingBehavior()
            .WithBehavior<AuditingOrchestrationBehavior>().WithOrchestration<RetryWorkflow>().WithOrchestration<WaitingWorkflow>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    public static async Task<AuditRecord[]> Records(IServiceProvider host)
    {
        var storage = host.GetRequiredService<IAuditingStorageProvider>();
        var plan = host.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        return await Task.WhenAll((await storage.SearchAsync(plan)).Value.Select(async record => (await storage.FindAsync(record.AuditId)).Value));
    }
    private static void AssertSafe(AuditRecord record) => Encoding.UTF8.GetString(AuditRecordCodec.Encode(record)).ShouldNotContain("private");
    public sealed class Customer { public string Name { get; set; } = "private-value"; }
    public sealed class ImportContext : PipelineContextBase { public int Attempts { get; set; } public bool Reject { get; set; } }
    public sealed class ImportValidator : AbstractValidator<ImportContext> { public ImportValidator() => this.RuleFor(value => value.Reject).Equal(false); }
    public sealed class RetryStep : PipelineStep<ImportContext>
    {
        protected override PipelineControl Execute(ImportContext context, Result result, PipelineExecutionOptions options)
            => ++context.Attempts == 1 ? PipelineControl.Retry(result) : PipelineControl.Continue(result);
    }
    public sealed class NestedAuditStep(IAuditContext audit) : AsyncPipelineStep<ImportContext>
    {
        protected override async ValueTask<PipelineControl> ExecuteAsync(ImportContext context, Result result, PipelineExecutionOptions options, CancellationToken cancellationToken)
        {
            await using var scope = audit.BeginScope(builder => builder.Key("customers.nested"));
            scope.Complete();
            return PipelineControl.Continue(result);
        }
    }
    public sealed class Data : IOrchestrationData { public int Attempts { get; set; } }
    public sealed class RetryWorkflow : Orchestration<Data>
    {
        protected override void Define(IOrchestrationBuilder<Data> builder) => builder.State("Start", state => state
            .Activity((context, _) => Task.FromResult(++context.Data.Attempts == 1 ? OrchestrationOutcome.Retry() : OrchestrationOutcome.Continue()), name: "Work").Complete());
    }
    public sealed class WaitingWorkflow : Orchestration<Data>
    {
        protected override void Define(IOrchestrationBuilder<Data> builder) => builder
            .State("Waiting", state => state.WaitForSignal("Approve", signal => signal.Activity((_, _) => Task.FromResult(OrchestrationOutcome.Continue()), "Approve").TransitionTo("Done")))
            .State("Done", state => state.Complete());
    }
    public sealed class UnreadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Capture consumed a stream.");
        public override int Read(Span<byte> buffer) => throw new InvalidOperationException("Capture consumed a stream.");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Capture consumed a stream.");
    }
}
