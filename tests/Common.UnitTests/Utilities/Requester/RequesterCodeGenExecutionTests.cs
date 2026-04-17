// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Common")]
public class RequesterCodeGenExecutionTests
{
    [Fact]
    public async Task GeneratedQuery_SendAsync_UsesGeneratedHandlerAndConvenienceHelpers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GeneratedUserStore>();
        services.AddSingleton<IGeneratedUserStore>(sp => sp.GetRequiredService<GeneratedUserStore>());
        services.AddRequester()
            .AddHandlers();

        var provider = services.BuildServiceProvider();
        var requester = provider.GetRequiredService<IRequester>();
        var store = provider.GetRequiredService<GeneratedUserStore>();
        var userId = Guid.NewGuid();
        store.Users[userId] = new GeneratedUser { UserId = userId, Username = "jane" };

        var success = await requester.SendAsync(new GeneratedGetUserQuery { UserId = userId });
        var failure = await requester.SendAsync(new GeneratedGetUserQuery { UserId = Guid.NewGuid() });

        success.IsSuccess.ShouldBeTrue();
        success.Value.Username.ShouldBe("jane");
        failure.IsFailure.ShouldBeTrue();
        failure.Messages.ShouldContain(message => message.Contains("not found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GeneratedCommand_WithValidationBehavior_UsesGeneratedValidator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GeneratedRequesterProbe>();
        services.AddRequester()
            .AddHandlers()
            .WithBehavior(typeof(ValidationPipelineBehavior<,>));

        var provider = services.BuildServiceProvider();
        var requester = provider.GetRequiredService<IRequester>();
        var probe = provider.GetRequiredService<GeneratedRequesterProbe>();

        var invalidResult = await requester.SendAsync(new GeneratedValidatedCommand { Message = string.Empty });
        var validResult = await requester.SendAsync(new GeneratedValidatedCommand { Message = "hello" });

        invalidResult.IsFailure.ShouldBeTrue();
        invalidResult.Errors.ShouldNotBeEmpty();
        probe.ValidatedCommandCalls.ShouldBe(1);
        validResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task GeneratedCommand_WithPropertyValidationAttributes_UsesGeneratedValidator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GeneratedRequesterProbe>();
        services.AddRequester()
            .AddHandlers()
            .WithBehavior(typeof(ValidationPipelineBehavior<,>));

        var provider = services.BuildServiceProvider();
        var requester = provider.GetRequiredService<IRequester>();
        var probe = provider.GetRequiredService<GeneratedRequesterProbe>();

        var invalidResult = await requester.SendAsync(new GeneratedAttributedValidatedCommand { Message = string.Empty });
        var validResult = await requester.SendAsync(new GeneratedAttributedValidatedCommand { Message = "hello" });

        invalidResult.IsFailure.ShouldBeTrue();
        invalidResult.Errors.ShouldNotBeEmpty();
        probe.ValidatedCommandCalls.ShouldBe(1);
        validResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task GeneratedCommand_WithPropertyValidationAttributesAndValidateMethod_UsesBothRuleSources()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GeneratedRequesterProbe>();
        services.AddRequester()
            .AddHandlers()
            .WithBehavior(typeof(ValidationPipelineBehavior<,>));

        var provider = services.BuildServiceProvider();
        var requester = provider.GetRequiredService<IRequester>();

        var shortNameResult = await requester.SendAsync(new GeneratedMixedValidatedCommand { Message = "hi" });
        var emptyNameResult = await requester.SendAsync(new GeneratedMixedValidatedCommand { Message = string.Empty });
        var validResult = await requester.SendAsync(new GeneratedMixedValidatedCommand { Message = "hello" });

        shortNameResult.IsFailure.ShouldBeTrue();
        emptyNameResult.IsFailure.ShouldBeTrue();
        validResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task GeneratedCommand_WithGuidValidationAttributes_UsesGeneratedValidator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .AddHandlers()
            .WithBehavior(typeof(ValidationPipelineBehavior<,>));

        var provider = services.BuildServiceProvider();
        var requester = provider.GetRequiredService<IRequester>();

        var invalidResult = await requester.SendAsync(new GeneratedGuidValidatedCommand { Id = "not-a-guid", ParentId = Guid.NewGuid().ToString() });
        var validResult = await requester.SendAsync(new GeneratedGuidValidatedCommand { Id = Guid.NewGuid().ToString(), ParentId = string.Empty });

        invalidResult.IsFailure.ShouldBeTrue();
        invalidResult.Errors.ShouldNotBeEmpty();
        validResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task GeneratedCommand_WithAllGuidValidationAttributes_UsesGeneratedValidator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester()
            .AddHandlers()
            .WithBehavior(typeof(ValidationPipelineBehavior<,>));

        var provider = services.BuildServiceProvider();
        var requester = provider.GetRequiredService<IRequester>();

        var invalidResult = await requester.SendAsync(new GeneratedAllGuidValidatedCommand
        {
            RequiredGuid = Guid.Empty.ToString(),
            RequiredNonDefaultGuid = Guid.Empty.ToString(),
            ValidGuid = "not-a-guid",
            EmptyGuid = Guid.NewGuid().ToString(),
            DefaultOrEmptyGuid = Guid.NewGuid().ToString(),
            GuidFormatOnly = "bad"
        });

        var validResult = await requester.SendAsync(new GeneratedAllGuidValidatedCommand
        {
            RequiredGuid = Guid.NewGuid().ToString(),
            RequiredNonDefaultGuid = Guid.NewGuid().ToString(),
            ValidGuid = Guid.NewGuid().ToString(),
            EmptyGuid = string.Empty,
            DefaultOrEmptyGuid = string.Empty,
            GuidFormatOnly = Guid.NewGuid().ToString()
        });

        invalidResult.IsFailure.ShouldBeTrue();
        invalidResult.Errors.ShouldNotBeEmpty();
        validResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task GeneratedCommand_WithRetryBehavior_UsesCopiedHandlerAttributes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GeneratedRetryService>();
        services.AddRequester()
            .AddHandlers()
            .WithBehavior(typeof(RetryPipelineBehavior<,>));

        var provider = services.BuildServiceProvider();
        var requester = provider.GetRequiredService<IRequester>();
        var retryService = provider.GetRequiredService<GeneratedRetryService>();

        var result = await requester.SendAsync(new GeneratedRetryCommand());

        result.IsSuccess.ShouldBeTrue();
        retryService.Attempts.ShouldBe(2);
    }
}

public sealed class GeneratedRequesterProbe
{
    public int ValidatedCommandCalls { get; set; }
}

public sealed class GeneratedUser
{
    public Guid UserId { get; set; }

    public string Username { get; set; }
}

public interface IGeneratedUserStore
{
    Task<GeneratedUser> FindAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class GeneratedUserStore : IGeneratedUserStore
{
    public Dictionary<Guid, GeneratedUser> Users { get; } = [];

    public Task<GeneratedUser> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        this.Users.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }
}

public sealed class GeneratedRetryService
{
    public int Attempts { get; private set; }

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.Attempts++;
        if (this.Attempts == 1)
        {
            throw new InvalidOperationException("retry me");
        }

        return Task.CompletedTask;
    }
}

[Query]
public partial class GeneratedGetUserQuery
{
    public Guid UserId { get; set; }

    [Handle]
    private async Task<Result<GeneratedUser>> HandleAsync(
        IGeneratedUserStore userStore,
        CancellationToken cancellationToken)
    {
        var user = await userStore.FindAsync(this.UserId, cancellationToken);
        return user != null
            ? Success(user)
            : Failure($"User with ID {this.UserId} not found.");
    }
}

[Command]
public partial class GeneratedValidatedCommand
{
    public string Message { get; set; }

    [Validate]
    private static void Validate(InlineValidator<GeneratedValidatedCommand> validator)
    {
        validator.RuleFor(x => x.Message).NotEmpty().WithMessage("Message is required.");
    }

    [Handle]
    private Result<Unit> Handle(GeneratedRequesterProbe probe)
    {
        probe.ValidatedCommandCalls++;
        return Success();
    }
}

[Command]
public partial class GeneratedAttributedValidatedCommand
{
    [ValidateNotEmpty("Message is required.")]
    public string Message { get; set; }

    [Handle]
    private Result<Unit> Handle(GeneratedRequesterProbe probe)
    {
        probe.ValidatedCommandCalls++;
        return Success();
    }
}

[Command]
public partial class GeneratedMixedValidatedCommand
{
    [ValidateNotEmpty]
    public string Message { get; set; }

    [Validate]
    private static void Validate(InlineValidator<GeneratedMixedValidatedCommand> validator)
    {
        validator.RuleFor(x => x.Message).MinimumLength(3);
    }

    [Handle]
    private Result<Unit> Handle()
    {
        return Success();
    }
}

[Command]
public partial class GeneratedGuidValidatedCommand
{
    [ValidateValidGuid("Invalid guid.")]
    public string Id { get; set; }

    [ValidateDefaultOrEmptyGuid]
    public string ParentId { get; set; }

    [Handle]
    private Result<Unit> Handle()
    {
        return Success();
    }
}

[Command]
public partial class GeneratedAllGuidValidatedCommand
{
    [ValidateNotEmptyGuid]
    public string RequiredGuid { get; set; }

    [ValidateNotDefaultOrEmptyGuid]
    public string RequiredNonDefaultGuid { get; set; }

    [ValidateValidGuid]
    public string ValidGuid { get; set; }

    [ValidateEmptyGuid]
    public string EmptyGuid { get; set; }

    [ValidateDefaultOrEmptyGuid]
    public string DefaultOrEmptyGuid { get; set; }

    [ValidateGuidFormat]
    public string GuidFormatOnly { get; set; }

    [Handle]
    private Result<Unit> Handle()
    {
        return Success();
    }
}

[Command]
[HandlerRetry(1, 1)]
public partial class GeneratedRetryCommand
{
    [Handle]
    private async Task<Result<Unit>> HandleAsync(
        GeneratedRetryService retryService,
        CancellationToken cancellationToken)
    {
        await retryService.ExecuteAsync(cancellationToken);
        return Success();
    }
}
