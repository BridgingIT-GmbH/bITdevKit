// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using System.Threading.Tasks.Dataflow;
using Common;
using Microsoft.Extensions.Logging;

/// <summary>
///     An in-process message broker that uses TPL Dataflow to provide asynchronous messaging capabilities.
/// </summary>
public class InProcessMessageBroker : MessageBrokerBase
{
    private readonly InProcessMessageBrokerOptions options;
    private readonly ActionBlock<MessageRequest> messageProcessor;

    public InProcessMessageBroker(InProcessMessageBrokerOptions options)
        : base(options.LoggerFactory,
            options.HandlerFactory,
            options.Serializer,
            options.PublisherBehaviors,
            options.HandlerBehaviors)
    {
        EnsureArg.IsNotNull(options, nameof(options));

        this.options = options;
        this.messageProcessor = new ActionBlock<MessageRequest>(async messageRequest =>
            {
                if (messageRequest != null)
                {
                    if (!options.MessageExpiration.HasValue ||
                        messageRequest.Message.Timestamp.AddMilliseconds(options.MessageExpiration.Value
                            .TotalMilliseconds) >=
                        DateTime.UtcNow)
                    {
                        await this.Process(messageRequest);
                    }
                    else
                    {
                        messageRequest.OnPublishComplete(true);
                    }
                }
            },
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = CancellationToken.None, MaxDegreeOfParallelism = 1, EnsureOrdered = true
            });

        this.Logger.LogInformation("{LogKey} broker initialized (name={MessageBroker})",
            Constants.LogKey,
            this.GetType().Name);
    }

    public InProcessMessageBroker(
        Builder<InProcessMessageBrokerOptionsBuilder, InProcessMessageBrokerOptions> optionsBuilder)
        : this(optionsBuilder(new InProcessMessageBrokerOptionsBuilder()).Build()) { }

    protected override Task OnPublish(IMessage message, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        this.messageProcessor.Post(new MessageRequest(message,
            result => tcs.SetResult(result),
            cancellationToken)); // TODO: message.Clone(), has issues with inheritance (EchoMessage = Message after clone)

        return tcs.Task;
    }

    protected override async Task OnProcess(IMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(this.options.ProcessDelay, cancellationToken);
    }
}