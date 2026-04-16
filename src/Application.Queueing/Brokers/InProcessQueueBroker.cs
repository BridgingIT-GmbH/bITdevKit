namespace BridgingIT.DevKit.Application.Queueing;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides an in-memory queue broker using <see cref="Channel{T}"/>.
/// </summary>
/// <remarks>
/// The broker is process-bound and best suited for tests, local work distribution, and lightweight workloads.
/// </remarks>
public class InProcessQueueBroker : QueueBrokerBase, IDisposable
{
    private readonly InProcessQueueBrokerOptions options;
    private readonly QueueBrokerControlState controlState;
    private readonly InProcessQueueBrokerRuntime runtime;
    private readonly CancellationTokenSource disposeCts = new();
    private readonly List<Task> workers;
    private int disposed;

    /// <summary>
    /// Initializes a new in-process queue broker instance.
    /// </summary>
    /// <param name="options">The broker runtime options.</param>
    public InProcessQueueBroker(
        InProcessQueueBrokerOptions options,
        QueueBrokerControlState controlState = null,
        InProcessQueueBrokerRuntime runtime = null)
        : base(
            options?.LoggerFactory,
            options?.HandlerFactory ?? throw new ArgumentNullException(nameof(options)),
            options.Serializer,
            options.EnqueuerBehaviors,
            options.HandlerBehaviors)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.controlState = controlState ?? new QueueBrokerControlState();
        this.runtime = runtime ?? new InProcessQueueBrokerRuntime(options);
        this.workers = Enumerable.Range(0, Math.Max(1, options.MaxDegreeOfParallelism))
            .Select(_ => Task.Run(() => this.ProcessLoopAsync(this.disposeCts.Token), this.disposeCts.Token))
            .ToList();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 1)
        {
            return;
        }

        try
        {
            this.disposeCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        this.runtime.Complete();

        try
        {
            Task.WaitAll(this.workers.ToArray(), TimeSpan.FromSeconds(5));
        }
        catch
        {
        }

        this.disposeCts.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnEnqueue(IQueueMessage message, CancellationToken cancellationToken)
    {
        var item = new InProcessQueueTrackedItem
        {
            Id = Guid.NewGuid(),
            Message = message,
            QueueName = this.GetQueueName(message.GetType()),
            Type = message.GetType().PrettyName(false),
            CreatedDate = DateTimeOffset.UtcNow,
            ExpiresOn = this.options.MessageExpiration.HasValue ? message.Timestamp.Add(this.options.MessageExpiration.Value) : null,
            ContentHash = HashHelper.Compute(message),
            Status = QueueMessageStatus.Pending
        };

        await this.runtime.EnqueueAsync(item, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task OnSubscribe<TMessage, THandler>()
    {
        return this.runtime.RequeueWaitingItemsAsync(typeof(TMessage).PrettyName(false), CancellationToken.None);
    }

    /// <inheritdoc />
    protected override Task OnSubscribe(Type messageType, Type handlerType)
    {
        return this.runtime.RequeueWaitingItemsAsync(messageType.PrettyName(false), CancellationToken.None);
    }

    private async Task ProcessLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var item in this.runtime.ReadAllAsync(cancellationToken))
        {
            await this.ProcessItemAsync(item, cancellationToken);
        }
    }

    private async Task ProcessItemAsync(InProcessQueueTrackedItem item, CancellationToken cancellationToken)
    {
        if (this.IsExpired(item))
        {
            this.runtime.MarkExpired(item, "message expired before processing");
            return;
        }

        if (this.IsPaused(item))
        {
            this.runtime.MarkPaused(item);
            return;
        }

        this.runtime.MarkProcessing(item);

        var result = QueueProcessingResult.Failed;
        await this.Process(new QueueMessageRequest(item.Message, value => result = value, cancellationToken));

        switch (result)
        {
            case QueueProcessingResult.Succeeded:
                this.runtime.MarkSucceeded(item, this.GetSubscription(item.Type)?.HandlerType?.FullName);
                break;

            case QueueProcessingResult.WaitingForHandler:
                this.runtime.MarkWaitingForHandler(item);
                break;

            case QueueProcessingResult.Expired:
                this.runtime.MarkExpired(item, "message expired before processing");
                break;

            default:
                this.runtime.MarkFailed(item, item.LastError ?? "queue processing failed", this.GetSubscription(item.Type)?.HandlerType?.FullName);
                break;
        }
    }

    private bool IsPaused(InProcessQueueTrackedItem item)
    {
        return this.controlState.IsQueuePaused(item.QueueName) || this.controlState.IsMessageTypePaused(item.Type);
    }

    private bool IsExpired(InProcessQueueTrackedItem item)
    {
        return this.options.MessageExpiration.HasValue && item.Message.Timestamp.Add(this.options.MessageExpiration.Value) < DateTimeOffset.UtcNow;
    }

    private string GetQueueName(Type messageType)
    {
        var typeName = messageType.PrettyName(false);
        return string.Concat(this.options.QueueNamePrefix, typeName, this.options.QueueNameSuffix);
    }
}