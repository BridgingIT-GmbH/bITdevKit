// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.IntegrationTests.Auditing;

using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using Application.Auditing;
using Application.DataPorter;
using Application.Notifications;
using Application.Storage;
using Application.Utilities;
using MailKit;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;

public sealed class BusinessContributorTests
{
    [Fact]
    public async Task Email_QueueWithoutOutbox_IsIgnoredAndDoesNotClaimAcceptance()
    {
        var services = Services();
        var smtp = Smtp();
        services.AddNotificationService<EmailMessage>(configure: builder => builder.WithAuditing());
        services.AddSingleton(smtp);
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<INotificationService<EmailMessage>>().QueueAsync(Email(), default)).IsSuccess.ShouldBeTrue();
        var record = (await Records(host)).ShouldHaveSingleItem();
        Status(record).ShouldBe("Ignored");
        record.Kind.ShouldBe("EmailQueue");
        record.Actions.ShouldBeEmpty();
        await smtp.DidNotReceive().SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
        Safe(record);
    }

    [Fact]
    public async Task Email_DirectRetry_RecordsActualSmtpAcceptanceWithoutRecipientDeliveryClaim()
    {
        var services = Services();
        var smtp = Smtp();
        var attempts = 0;
        smtp.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>()).Returns(_ =>
            ++attempts == 1 ? Task.FromException<string>(new InvalidOperationException("private-smtp-error")) : Task.FromResult("accepted"));
        services.AddNotificationService<EmailMessage>(configure: builder => builder.WithAuditing());
        services.AddSingleton(smtp);
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<INotificationService<EmailMessage>>().SendAsync(Email(), new() { SendImmediately = true }, default);
        result.IsSuccess.ShouldBeTrue();
        attempts.ShouldBe(2);
        var record = (await Records(host)).ShouldHaveSingleItem();
        Status(record).ShouldBe("SmtpAccepted");
        record.Outcome.ShouldBe(AuditOutcome.Succeeded);
        record.Actions.Select(action => action.Attempt).ShouldBe(new int?[] { 1, 2 });
        record.Actions.Select(action => action.Outcome).ShouldBe(new[] { AuditOutcome.Failed, AuditOutcome.Succeeded });
        record.Actions.Last().ExecutionStatus.ShouldBe("SmtpAccepted");
        record.EntityChanges.ShouldBeEmpty();
        Safe(record);
    }

    [Fact]
    public async Task Email_OutboxWorker_IsIndependentAndRetainsAuthenticatedOriginalInitiator()
    {
        var services = Services();
        services.AddNotificationService<EmailMessage>(configure: builder =>
        {
            builder.Options.IsOutboxConfigured = true;
            builder.WithAuditing();
        });
        services.AddSingleton(Smtp());
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using (var scope = host.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<INotificationService<EmailMessage>>().QueueAsync(Email(), default)).IsSuccess.ShouldBeTrue();
        }

        var admission = (await Records(host)).ShouldHaveSingleItem();
        Status(admission).ShouldBe("OutboxAccepted");
        await using var caller = host.CreateAsyncScope();
        var root = caller.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("unrelated.request"));
        var worker = new OutboxNotificationEmailWorker(host.GetRequiredService<ILoggerFactory>(), host);
        await worker.ProcessAsync();
        var execution = (await Records(host)).Single(record => record.Kind == "EmailWorker");
        execution.AuditId.ShouldNotBe(root.AuditId);
        execution.Actor.Kind.ShouldBe("Service");
        execution.CausationAuditId.ShouldBe(admission.AuditId);
        execution.InitiatingActor.UserId.ShouldBe("alice");
        Status(execution).ShouldBe("SmtpAccepted");
        execution.Actions.ShouldContain(action => action.Kind == "EmailSmtpAttempt" && action.Attempt == 1);
        root.Complete();
        await root.DisposeAsync();
        Safe(execution);
    }

    [Fact]
    public async Task DataPorter_AggregatePartialResult_IsFailedWithoutCapturingImportedValues()
    {
        var provider = new ImportProvider { Partial = true };
        var services = Services();
        services.AddSingleton<IDataPorterProvider>(provider);
        services.AddDataPorter().WithAuditing();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        using var input = new MemoryStream([1]);
        var result = await scope.ServiceProvider.GetRequiredService<IDataImporter>().ImportAsync<Customer>(input, new ImportOptions { Format = Format.Csv });
        result.IsSuccess.ShouldBeTrue();
        result.Value.FailedRows.ShouldBe(1);
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(AuditOutcome.Failed);
        Status(record).ShouldBe("Partial");
        record.Details["dataporter.observedRows"].Data.GetInt64().ShouldBe(2);
        record.Details["dataporter.acceptedRows"].Data.GetInt64().ShouldBe(1);
        record.EntityChanges.ShouldBeEmpty();
        Safe(record);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DataPorter_Streaming_ClosesOnlyAfterEnumerationAndUnderlyingCleanup(bool early)
    {
        var provider = new ImportProvider();
        var services = Services();
        services.AddSingleton<IDataPorterProvider>(provider);
        services.AddDataPorter().WithAuditing().WithAuditing();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        using var input = new MemoryStream([1]);
        var rows = scope.ServiceProvider.GetRequiredService<IDataImporter>().ImportAsyncEnumerable<Customer>(input, new ImportOptions { Format = Format.Csv });
        provider.Enumerations.ShouldBe(0);
        await using (var enumerator = rows.GetAsyncEnumerator())
        {
            (await enumerator.MoveNextAsync()).ShouldBeTrue();
            (await Records(host)).ShouldBeEmpty();
            provider.Disposed.ShouldBeFalse();
            if (!early)
            {
                (await enumerator.MoveNextAsync()).ShouldBeTrue();
                (await enumerator.MoveNextAsync()).ShouldBeFalse();
            }
        }

        provider.Disposed.ShouldBeTrue();
        provider.Enumerations.ShouldBe(1);
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(early ? AuditOutcome.Unknown : AuditOutcome.Succeeded);
        record.Completeness.ShouldBe(early ? AuditCompleteness.Incomplete : AuditCompleteness.Complete);
        Status(record).ShouldBe(early ? "Incomplete" : "Completed");
        record.Details["dataporter.observedRows"].Data.GetInt64().ShouldBe(early ? 1 : 2);
        Safe(record);
    }

    [Fact]
    public async Task DataPorter_StreamingCleanupFailure_PreventsSuccessfulCompletion()
    {
        var provider = new ImportProvider { FailCleanup = true };
        var services = Services();
        services.AddSingleton<IDataPorterProvider>(provider);
        services.AddDataPorter().WithAuditing();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        using var input = new MemoryStream([1]);
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var row in scope.ServiceProvider.GetRequiredService<IDataImporter>().ImportAsyncEnumerable<Customer>(input, new ImportOptions { Format = Format.Csv })) { row.IsSuccess.ShouldBeTrue(); }
        });
        provider.Disposed.ShouldBeTrue();
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(AuditOutcome.Failed);
        Safe(record);
    }

    [Fact]
    public async Task DataPorter_CanceledAggregate_PreservesFailureResultAndRecordsCancellation()
    {
        var provider = new ImportProvider { Cancel = true };
        var services = Services();
        services.AddSingleton<IDataPorterProvider>(provider);
        services.AddDataPorter().WithAuditing();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        using var input = new MemoryStream([1]);
        (await scope.ServiceProvider.GetRequiredService<IDataImporter>().ImportAsync<Customer>(input, new ImportOptions { Format = Format.Csv })).IsFailure.ShouldBeTrue();
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(AuditOutcome.Canceled);
        Status(record).ShouldBe("Canceled");
        Safe(record);
    }

    [Fact]
    public async Task DataPorter_Export_DoesNotReplayInputAndFinalizesCompression()
    {
        var services = Services();
        services.AddSingleton<IDataPorterProvider, CsvDataPorterProvider>();
        services.AddDataPorter().WithAuditing();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        using var output = new MemoryStream();
        var rows = new OnceSequence<Customer>([new(), new()]);
        var result = await scope.ServiceProvider.GetRequiredService<IDataExporter>().ExportAsync(rows, output,
            new ExportOptions { Format = Format.Csv, Compression = new() { Kind = PayloadCompressionKind.GZip } });
        result.IsSuccess.ShouldBeTrue();
        rows.Enumerations.ShouldBe(1);
        output.Length.ShouldBeGreaterThan(0);
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(AuditOutcome.Succeeded);
        record.Details["dataporter.observedRows"].Data.GetInt64().ShouldBe(2);
        Safe(record);
    }

    [Fact]
    public async Task StartupTasks_RealService_IsolatesAndRecordsActualRetryInvocations()
    {
        var services = Services();
        var lifetime = new Lifetime();
        var state = new State();
        services.AddSingleton<IHostApplicationLifetime>(lifetime);
        services.AddSingleton(state);
        services.AddStartupTasks().WithTask<RetryTask>().WithBehavior<RetryStartupTaskBehavior>().WithBehavior<AuditingStartupTaskBehavior>();
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var caller = host.CreateAsyncScope();
        var root = caller.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.start"));
        var service = host.GetServices<IHostedService>().OfType<StartupTasksService>().Single();
        await service.StartAsync(default);
        lifetime.Started.Cancel();
        root.Complete();
        await root.DisposeAsync();
        await state.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.GetRequiredService<AuditRuntime>().WaitForIdleAsync(timeout.Token);
        await service.StopAsync(default);
        state.Attempts.ShouldBe(3);
        var records = (await Records(host)).Where(record => record.Kind == "StartupTask").ToArray();
        records.Length.ShouldBe(3);
        records.Count(record => record.Outcome == AuditOutcome.Failed).ShouldBe(2);
        records.Count(record => record.Outcome == AuditOutcome.Succeeded).ShouldBe(1);
        records.ShouldAllBe(record => record.Actor.Kind == "Service" && record.CausationAuditId == null);
        foreach (var record in records) Safe(record);
    }

    [Fact]
    public async Task FileMonitoring_ExplicitScan_ProducesLinkedServiceEventAndActualProcessorEvidence()
    {
        var services = Services();
        var state = new State();
        services.AddSingleton(state);
        services.AddFileMonitoring(builder => builder.WithAuditing(new() { Scans = true }).UseInMemory("inbox", location =>
        {
            location.UseOnDemandOnly = true;
            location.UseProcessor<FailingProcessor>().WithBehavior<CatchingCallback>();
            location.UseProcessor<SuccessfulProcessor>();
        }));
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = host.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<ILocationHandler>().Single();
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("private-file-content"));
        (await handler.Provider.WriteFileAsync("private-customer.txt", content)).IsSuccess.ShouldBeTrue();
        var scan = await handler.ScanAsync(new FileScanOptions { CaptureAudit = true });
        scan.Events.ShouldHaveSingleItem();
        await state.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.GetRequiredService<AuditRuntime>().WaitForIdleAsync(timeout.Token);
        var records = await Records(host);
        var detection = records.Single(record => record.Kind == "FileScan");
        detection.Outcome.ShouldBe(AuditOutcome.Succeeded);
        var execution = records.Single(record => record.Kind == "FileEvent");
        execution.Outcome.ShouldBe(AuditOutcome.Failed);
        execution.Actor.Kind.ShouldBe("Service");
        execution.InitiatingActor.UserId.ShouldBe("alice");
        execution.CausationAuditId.ShouldBe(detection.AuditId);
        execution.Actions.Length.ShouldBe(2);
        execution.Actions.ShouldContain(action => action.Outcome == AuditOutcome.Failed && action.HandlerType == typeof(FailingProcessor).FullName);
        execution.Actions.ShouldContain(action => action.Outcome == AuditOutcome.Succeeded && action.HandlerType == typeof(SuccessfulProcessor).FullName);
        state.CallbackObservedFailure.ShouldBeTrue();
        foreach (var record in records) Safe(record);
        await handler.StopAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DataPorter_ByteHelperRejectedBeforeProvider_CapturesExactlyOneAdmission(bool empty)
    {
        var services = Services();
        services.AddDataPorter().WithAuditing();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IDataImporter>().ImportAsync<Customer>(empty ? [] : (byte[])null)).IsFailure.ShouldBeTrue();
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(AuditOutcome.Rejected);
        record.Actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task DataPorter_StreamingSetupRejection_RecordsNoProcessedRows()
    {
        var services = Services();
        services.AddDataPorter().WithAuditing();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        var failures = 0;
        await foreach (var result in scope.ServiceProvider.GetRequiredService<IDataImporter>().ImportAsyncEnumerable<Customer>((Stream)null))
        {
            result.IsFailure.ShouldBeTrue();
            failures++;
        }

        failures.ShouldBe(1);
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(AuditOutcome.Rejected);
        record.Details["dataporter.observedRows"].Data.GetInt64().ShouldBe(0);
    }

    [Fact]
    public async Task DataPorter_CompressionCleanupFailure_PreventsSuccessfulExportRecording()
    {
        var services = Services();
        using var output = new FailingOutput();
        var provider = Substitute.For<IDataExportProvider>();
        provider.Format.Returns(Format.Csv);
        provider.SupportsExport.Returns(true);
        provider.ExportAsync(Arg.Any<IEnumerable<Customer>>(), Arg.Any<Stream>(), Arg.Any<ExportConfiguration>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await call.ArgAt<Stream>(1).WriteAsync(new byte[] { 1, 2, 3 });
            output.FailWrites = true;
            return new ExportResult { BytesWritten = 3, TotalRows = 1, Duration = TimeSpan.Zero, Format = Format.Csv };
        });
        services.AddSingleton<IDataPorterProvider>(provider);
        services.AddDataPorter().WithAuditing();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IDataExporter>().ExportAsync(new[] { new Customer() }, output,
            new ExportOptions { Format = Format.Csv, Compression = new() { Kind = PayloadCompressionKind.GZip } });
        result.IsFailure.ShouldBeTrue();
        output.FailedWrite.ShouldBeTrue();
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(AuditOutcome.Failed);
        Status(record).ShouldBe("Failed");
        Safe(record);
    }

    [Fact]
    public async Task Email_WorkerReturnedFailure_IsObservedEvenAfterSmtpAcceptance()
    {
        var services = Services();
        var message = Email();
        var storage = Substitute.For<INotificationStorageProvider>();
        storage.GetPendingAsync<EmailMessage>(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Result<IEnumerable<EmailMessage>>.Success([message]));
        storage.UpdateAsync(message, Arg.Any<CancellationToken>()).Returns(Result.Failure(new Error("private-store-error")));
        services.AddNotificationService<EmailMessage>(configure: builder =>
        {
            builder.Options.IsOutboxConfigured = true;
            builder.WithAuditing();
        });
        services.AddSingleton(storage);
        services.AddSingleton(Smtp());
        await using var host = services.BuildServiceProvider();
        await new OutboxNotificationEmailWorker(host.GetRequiredService<ILoggerFactory>(), host).ProcessAsync();
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Kind.ShouldBe("EmailWorker");
        record.Outcome.ShouldBe(AuditOutcome.Failed);
        Status(record).ShouldBe("Failed");
        record.Actions.ShouldContain(action => action.Kind == "EmailSmtpAttempt" && action.ExecutionStatus == "SmtpAccepted");
        Safe(record);
    }

    [Fact]
    public async Task FileMonitoring_RoutineUnchangedScan_IsOmittedEvenWhenScanCaptureIsAvailable()
    {
        var services = Services();
        services.AddFileMonitoring(builder => builder.WithAuditing(new() { Scans = true }).UseInMemory("inbox", location => location.UseOnDemandOnly = true));
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<ILocationHandler>().Single();
        (await handler.ScanAsync()).Events.ShouldBeEmpty();
        (await Records(host)).ShouldBeEmpty();
        (await handler.ScanAsync(new FileScanOptions { CaptureAudit = true })).Events.ShouldBeEmpty();
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Kind.ShouldBe("FileScan");
        record.Actions.ShouldBeEmpty();
        await handler.StopAsync();
    }

    [Fact]
    public async Task Email_DisconnectFailure_RetainsKnownAcceptanceAndFailedCleanup()
    {
        var services = Services();
        var smtp = Smtp();
        var disconnects = 0;
        smtp.DisconnectAsync(true, Arg.Any<CancellationToken>()).Returns(_ => ++disconnects == 1 ? Task.FromException(new IOException("private-disconnect")) : Task.CompletedTask);
        services.AddNotificationService<EmailMessage>(configure: builder => builder.WithAuditing());
        services.AddSingleton(smtp);
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<INotificationService<EmailMessage>>().SendAsync(Email(), new(), default)).IsSuccess.ShouldBeTrue();
        var record = (await Records(host)).ShouldHaveSingleItem();
        var failed = record.Actions.Single(action => action.Outcome == AuditOutcome.Failed);
        failed.ExecutionStatus.ShouldBe("SmtpAccepted");
        failed.Details["notification.smtpAccepted"].Data.GetBoolean().ShouldBeTrue();
        failed.Attempt.ShouldBe(1);
        record.Actions.Last().Attempt.ShouldBe(2);
        Safe(record);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Email_SmtpPipelineShortCircuit_PreservesResultWithoutInventingAcceptance(bool success)
    {
        var services = Services();
        var smtp = Smtp();
        var expected = success ? Result.Success() : Result.Failure(new Error("private-rejection"));
        services.AddNotificationService<EmailMessage>(configure: builder => builder.WithAuditing());
        services.AddSingleton(smtp);
        services.AddSingleton<INotificationOperationBehavior>(new SmtpShortCircuit(expected));
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        var actual = await scope.ServiceProvider.GetRequiredService<INotificationService<EmailMessage>>().SendAsync(Email(), new(), default);
        actual.ShouldBe(expected);
        var record = (await Records(host)).ShouldHaveSingleItem();
        Status(record).ShouldBe(success ? "NotSent" : "Failed");
        record.Actions.ShouldBeEmpty();
        await smtp.DidNotReceive().SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
        Safe(record);
    }

    [Fact]
    public async Task DataPorter_Reenumeration_HasIndependentCountersAndOutcomes()
    {
        var provider = new ImportProvider { AllowRepeat = true, StreamFailure = true };
        var services = Services();
        services.AddSingleton<IDataPorterProvider>(provider);
        services.AddDataPorter().WithAuditing();
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        using var input = new MemoryStream([1]);
        var rows = scope.ServiceProvider.GetRequiredService<IDataImporter>().ImportAsyncEnumerable<Customer>(input, new ImportOptions { Format = Format.Csv });
        await foreach (var _ in rows) { }

        var first = (await Records(host)).ShouldHaveSingleItem();
        first.Outcome.ShouldBe(AuditOutcome.Failed);
        provider.RowCount = 0;
        input.Position = 0;
        await foreach (var _ in rows) { }

        var second = (await Records(host)).Single(record => record.AuditId != first.AuditId);
        second.Outcome.ShouldBe(AuditOutcome.Succeeded);
        second.Details["dataporter.observedRows"].Data.GetInt64().ShouldBe(0);
        second.Details["dataporter.failedRows"].Data.GetInt64().ShouldBe(0);
    }

    [Fact]
    public async Task FileMonitoring_UnrequestedCancellation_IsFailedInBothEventAndProcessor()
    {
        var services = Services();
        var state = new State();
        services.AddSingleton(state);
        services.AddFileMonitoring(builder => builder.WithAuditing().UseInMemory("inbox", location =>
        {
            location.UseOnDemandOnly = true;
            location.UseProcessor<UnrequestedCancellationProcessor>().WithBehavior<CatchingCallback>();
            location.UseProcessor<SuccessfulProcessor>();
        }));
        await using var host = services.BuildServiceProvider();
        await using var scope = host.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<ILocationHandler>().Single();
        using var content = new MemoryStream([1]);
        await handler.Provider.WriteFileAsync("private-path.txt", content);
        await handler.ScanAsync();
        await state.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.GetRequiredService<AuditRuntime>().WaitForIdleAsync(timeout.Token);
        var record = (await Records(host)).ShouldHaveSingleItem();
        record.Outcome.ShouldBe(AuditOutcome.Failed);
        record.Actions.ShouldContain(action => action.HandlerType == typeof(UnrequestedCancellationProcessor).FullName && action.Outcome == AuditOutcome.Failed);
        record.Actor.Kind.ShouldBe("Service");
        record.InitiatingActor.ShouldBeNull();
        await handler.StopAsync();
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        var user = Substitute.For<ICurrentUserAccessor>();
        user.IsAuthenticated.Returns(true); user.UserId.Returns("alice"); user.UserName.Returns("Alice");
        services.AddSingleton(user);
        services.AddAuditing();
        return services;
    }
    private static ISmtpClient Smtp()
    {
        var smtp = Substitute.For<ISmtpClient>();
        smtp.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>()).Returns(Task.FromResult("accepted"));
        return smtp;
    }
    private static EmailMessage Email() => new() { Id = Guid.NewGuid(), From = new() { Address = "private-sender@example.com" }, To = ["private-recipient@example.com"], Subject = "private-subject", Body = "private-body" };
    private static string Status(AuditRecord record) => record.Details["source.executionStatus"].Data.GetString();
    private static void Safe(AuditRecord record) => Encoding.UTF8.GetString(AuditRecordCodec.Encode(record)).ShouldNotContain("private");
    private static async Task<AuditRecord[]> Records(IServiceProvider services)
    {
        var provider = services.GetRequiredService<IAuditingStorageProvider>();
        var plan = services.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        return await Task.WhenAll((await provider.SearchAsync(plan)).Value.Select(async item => (await provider.FindAsync(item.AuditId)).Value));
    }
    public sealed class SmtpShortCircuit(Result result) : INotificationOperationBehavior
    {
        public Task<Result> ExecuteAsync(NotificationOperationContext context, Func<Task<Result>> next) => context.Operation == "SmtpAttempt" ? Task.FromResult(result) : next();
    }
    public sealed class UnrequestedCancellationProcessor : IFileEventProcessor
    {
        public string ProcessorName => "CanceledImport";
        public bool IsEnabled { get; set; } = true;
        public IEnumerable<IProcessorBehavior> Behaviors => [];
        public Task ProcessAsync(FileProcessingContext context, CancellationToken token) => throw new OperationCanceledException("private-unsignaled-cancellation");
    }
    public sealed class FailingOutput : MemoryStream
    {
        public bool FailWrites { get; set; }
        public bool FailedWrite { get; private set; }
        public override void Write(byte[] buffer, int offset, int count) { this.Check(); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { this.Check(); base.Write(buffer); }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) { this.Check(); return base.WriteAsync(buffer, cancellationToken); }
        private void Check() { if (!this.FailWrites) return; this.FailedWrite = true; throw new IOException("private-trailer-error"); }
    }
    public sealed class Customer { public string Name { get; set; } = "private-row-value"; }
    public sealed class OnceSequence<T>(IEnumerable<T> values) : IEnumerable<T>
    {
        public int Enumerations { get; private set; }
        public IEnumerator<T> GetEnumerator() { if (++this.Enumerations != 1) throw new InvalidOperationException("Input replayed."); return values.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
    public sealed class ImportProvider : IDataImportProvider
    {
        public Format Format => Format.Csv;
        public IReadOnlyCollection<string> SupportedExtensions => [".csv"];
        public bool SupportsImport => true;
        public bool SupportsExport => false;
        public bool SupportsStreaming => true;
        public bool Partial { get; init; }
        public bool Cancel { get; init; }
        public bool FailCleanup { get; init; }
        public bool AllowRepeat { get; init; }
        public bool StreamFailure { get; set; }
        public int RowCount { get; set; } = 2;
        public bool Disposed { get; private set; }
        public int Enumerations { get; private set; }
        public Task<ImportResult<T>> ImportAsync<T>(Stream inputStream, ImportConfiguration configuration, CancellationToken cancellationToken = default) where T : class, new()
            => this.Cancel ? throw new OperationCanceledException("private-cancel") : Task.FromResult(new ImportResult<T> { Data = [new()], TotalRows = 2, SuccessfulRows = this.Partial ? 1 : 2, FailedRows = this.Partial ? 1 : 0, Duration = TimeSpan.Zero });
        public async IAsyncEnumerable<Result<T>> ImportStreamAsync<T>(Stream inputStream, ImportConfiguration configuration, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, new()
        {
            if (++this.Enumerations != 1 && !this.AllowRepeat) throw new InvalidOperationException("Input replayed.");
            try
            {
                await Task.Yield();
                for (var row = 0; row < this.RowCount; row++) yield return this.StreamFailure && row == 0 ? Result<T>.Failure(new Error("private-row-error")) : Result<T>.Success(new());
            }
            finally
            {
                this.Disposed = true;
                if (this.FailCleanup) throw new InvalidOperationException("private-cleanup");
            }
        }
        public Task<ValidationResult> ValidateAsync<T>(Stream inputStream, ImportConfiguration configuration, CancellationToken cancellationToken = default) where T : class, new() => Task.FromResult(ValidationResult.Success(0));
    }
    public sealed class State
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Attempts;
        public bool CallbackObservedFailure;
    }
    public sealed class RetryTask(State state) : IStartupTask, IRetryStartupTask
    {
        public RetryStartupTaskOptions Options { get; } = new() { Attempts = 3, Backoff = TimeSpan.Zero };
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (++state.Attempts < 3) throw new InvalidOperationException("private-startup-failure");
            state.Completed.TrySetResult();
            return Task.CompletedTask;
        }
    }
    public sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationTokenSource Started { get; } = new();
        public CancellationToken ApplicationStarted => this.Started.Token;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
    public sealed class FailingProcessor : IFileEventProcessor
    {
        public string ProcessorName => "ImportCustomer";
        public bool IsEnabled { get; set; } = true;
        public IEnumerable<IProcessorBehavior> Behaviors => [];
        public Task ProcessAsync(FileProcessingContext context, CancellationToken token) => throw new InvalidOperationException("private-processor-failure");
    }
    public sealed class SuccessfulProcessor(State state) : IFileEventProcessor
    {
        public string ProcessorName => "ConfirmCustomer";
        public bool IsEnabled { get; set; } = true;
        public IEnumerable<IProcessorBehavior> Behaviors => [];
        public Task ProcessAsync(FileProcessingContext context, CancellationToken token) { state.Completed.TrySetResult(); return Task.CompletedTask; }
    }
    public sealed class CatchingCallback(State state) : IProcessorBehavior
    {
        public Task BeforeProcessAsync(FileProcessingContext context, CancellationToken token) => Task.CompletedTask;
        public Task AfterProcessAsync(FileProcessingContext context, Result<bool> result, CancellationToken token) { state.CallbackObservedFailure = result.IsFailure; return Task.CompletedTask; }
    }
}
