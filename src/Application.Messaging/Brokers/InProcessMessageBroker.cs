// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using System.Threading.Tasks.Dataflow;

/// <summary>
///     An in-process message broker that uses TPL Dataflow to provide asynchronous messaging capabilities.
/// </summary>
public class InProcessMessageBroker : MessageBrokerBase
{
    private readonly InProcessMessageBrokerOptions options;
    private readonly ActionBlock<MessageRequest> messageProcessor;

    /// <summary>
    /// Initializes a new instance of the <c>InProcessMessageBroker</c> class.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
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
                        messageRequest.Message.Timestamp.AddMilliseconds(options.MessageExpiration.Value.TotalMilliseconds) >= DateTime.UtcNow)
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
                CancellationToken = CancellationToken.None,
                MaxDegreeOfParallelism = 1,
                EnsureOrdered = true
            });

        this.Logger.LogInformation("[{LogKey}] broker initialized (name={MessageBroker})", Constants.LogKey, this.GetType().Name);
    }

    /// <summary>
    /// Initializes a new instance of the <c>InProcessMessageBroker</c> class.
    /// </summary>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    public InProcessMessageBroker(
        Builder<InProcessMessageBrokerOptionsBuilder, InProcessMessageBrokerOptions> optionsBuilder)
        : this(optionsBuilder(new InProcessMessageBrokerOptionsBuilder()).Build()) { }

    /// <inheritdoc/>
    protected override Task OnPublish(IMessage message, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        this.messageProcessor.Post(
            new MessageRequest(message, tcs.SetResult, cancellationToken)); // TODO: message.Clone(), has issues with inheritance (EchoMessage = Message after clone)

        return tcs.Task;
    }

    /// <inheritdoc/>
    protected override async Task OnProcess(IMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(this.options.ProcessDelay, cancellationToken);
    }
}
