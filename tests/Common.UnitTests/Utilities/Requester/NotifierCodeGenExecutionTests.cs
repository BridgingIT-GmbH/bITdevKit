// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Common")]
public class NotifierCodeGenExecutionTests
{
    [Fact]
    public async Task GeneratedEvent_PublishAsync_ExecutesGeneratedAndManualHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GeneratedNotifierProbe>();
        services.AddNotifier()
            .AddHandlers();

        var provider = services.BuildServiceProvider();
        var notifier = provider.GetRequiredService<INotifier>();
        var probe = provider.GetRequiredService<GeneratedNotifierProbe>();

        var result = await notifier.PublishAsync(new GeneratedUserRegisteredEvent { Email = "user@example.com" });

        result.IsSuccess.ShouldBeTrue();
        probe.GeneratedAuditCalls.ShouldBe(1);
        probe.GeneratedEmailCalls.ShouldBe(1);
        probe.ManualCalls.ShouldBe(1);
    }

    [Fact]
    public async Task GeneratedEvent_PublishAsyncConcurrent_ExecutesGeneratedAndManualHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GeneratedNotifierProbe>();
        services.AddNotifier()
            .AddHandlers();

        var provider = services.BuildServiceProvider();
        var notifier = provider.GetRequiredService<INotifier>();
        var probe = provider.GetRequiredService<GeneratedNotifierProbe>();

        var result = await notifier.PublishAsync(
            new GeneratedUserRegisteredEvent { Email = "user@example.com" },
            new PublishOptions { ExecutionMode = ExecutionMode.Concurrent });

        result.IsSuccess.ShouldBeTrue();
        probe.GeneratedAuditCalls.ShouldBe(1);
        probe.GeneratedEmailCalls.ShouldBe(1);
        probe.ManualCalls.ShouldBe(1);
    }

    [Fact]
    public async Task GeneratedEvent_WithValidationBehavior_UsesGeneratedValidator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GeneratedNotifierProbe>();
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(ValidationPipelineBehavior<,>));

        var provider = services.BuildServiceProvider();
        var notifier = provider.GetRequiredService<INotifier>();
        var probe = provider.GetRequiredService<GeneratedNotifierProbe>();

        var invalidResult = await notifier.PublishAsync(new GeneratedValidatedEvent { Message = string.Empty });
        var validResult = await notifier.PublishAsync(new GeneratedValidatedEvent { Message = "hello" });

        invalidResult.IsFailure.ShouldBeTrue();
        invalidResult.Errors.ShouldNotBeEmpty();
        probe.ValidatedCalls.ShouldBe(1);
        validResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task GeneratedEvent_WithPropertyValidationAttributesAndValidateMethod_UsesBothRuleSources()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(ValidationPipelineBehavior<,>));

        var provider = services.BuildServiceProvider();
        var notifier = provider.GetRequiredService<INotifier>();

        var shortNameResult = await notifier.PublishAsync(new GeneratedMixedValidatedEvent { Message = "hi" });
        var emptyNameResult = await notifier.PublishAsync(new GeneratedMixedValidatedEvent { Message = string.Empty });
        var validResult = await notifier.PublishAsync(new GeneratedMixedValidatedEvent { Message = "hello" });

        shortNameResult.IsFailure.ShouldBeTrue();
        emptyNameResult.IsFailure.ShouldBeTrue();
        validResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task GeneratedEvent_WithRetryBehavior_AppliesToEachGeneratedHandler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GeneratedNotifierRetryService>();
        services.AddNotifier()
            .AddHandlers()
            .WithBehavior(typeof(RetryPipelineBehavior<,>));

        var provider = services.BuildServiceProvider();
        var notifier = provider.GetRequiredService<INotifier>();
        var retryService = provider.GetRequiredService<GeneratedNotifierRetryService>();

        var result = await notifier.PublishAsync(new GeneratedRetryEvent());

        result.IsSuccess.ShouldBeTrue();
        retryService.Attempts["first"].ShouldBe(2);
        retryService.Attempts["second"].ShouldBe(2);
    }
}

public sealed class GeneratedNotifierProbe
{
    public int GeneratedAuditCalls { get; set; }

    public int GeneratedEmailCalls { get; set; }

    public int ManualCalls { get; set; }

    public int ValidatedCalls { get; set; }
}

[Event]
public partial class GeneratedUserRegisteredEvent
{
    public string Email { get; set; }

    [Handle]
    private Result Audit(GeneratedNotifierProbe probe)
    {
        probe.GeneratedAuditCalls++;
        return Success();
    }

    [Handle]
    private async Task<Result> SendEmailAsync(
        GeneratedNotifierProbe probe,
        CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken);
        probe.GeneratedEmailCalls++;
        return Success();
    }
}

public class GeneratedUserRegisteredEventManualHandler(GeneratedNotifierProbe probe) : NotificationHandlerBase<GeneratedUserRegisteredEvent>
{
    protected override Task<Result> HandleAsync(GeneratedUserRegisteredEvent notification, PublishOptions options, CancellationToken cancellationToken)
    {
        probe.ManualCalls++;
        return Task.FromResult(Result.Success());
    }
}

[Event]
public partial class GeneratedValidatedEvent
{
    public string Message { get; set; }

    [Validate]
    private static void Validate(InlineValidator<GeneratedValidatedEvent> validator)
    {
        validator.RuleFor(x => x.Message).NotEmpty().WithMessage("Message is required.");
    }

    [Handle]
    private Result Handle(GeneratedNotifierProbe probe)
    {
        probe.ValidatedCalls++;
        return Success();
    }
}

[Event]
public partial class GeneratedMixedValidatedEvent
{
    [ValidateNotEmpty]
    public string Message { get; set; }

    [Validate]
    private static void Validate(InlineValidator<GeneratedMixedValidatedEvent> validator)
    {
        validator.RuleFor(x => x.Message).MinimumLength(3);
    }

    [Handle]
    private Result Handle()
    {
        return Success();
    }
}

public sealed class GeneratedNotifierRetryService
{
    public ConcurrentDictionary<string, int> Attempts { get; } = new(StringComparer.Ordinal);

    public Task ExecuteAsync(string name, CancellationToken cancellationToken)
    {
        var attempts = this.Attempts.AddOrUpdate(name, 1, static (_, current) => current + 1);
        if (attempts == 1)
        {
            throw new InvalidOperationException("retry me");
        }

        return Task.CompletedTask;
    }
}

[Event]
[HandlerRetry(1, 1)]
public partial class GeneratedRetryEvent
{
    [Handle]
    private async Task<Result> FirstAsync(
        GeneratedNotifierRetryService retryService,
        CancellationToken cancellationToken)
    {
        await retryService.ExecuteAsync("first", cancellationToken);
        return Success();
    }

    [Handle]
    private async Task<Result> SecondAsync(
        GeneratedNotifierRetryService retryService,
        CancellationToken cancellationToken)
    {
        await retryService.ExecuteAsync("second", cancellationToken);
        return Success();
    }
}
