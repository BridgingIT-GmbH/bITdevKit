namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Represents a resolved queue handler together with its owned runtime lifetime.
/// </summary>
public sealed class QueueMessageHandlerFactoryResult : IDisposable, IAsyncDisposable
{
    private readonly Func<ValueTask> disposeAsync;
    private int disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueMessageHandlerFactoryResult"/> class.
    /// </summary>
    /// <param name="handler">The resolved queue handler instance.</param>
    /// <param name="disposeAsync">The delegate that releases owned resources.</param>
    public QueueMessageHandlerFactoryResult(object handler, Func<ValueTask> disposeAsync = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        this.Handler = handler;
        this.disposeAsync = disposeAsync ?? (() => ValueTask.CompletedTask);
    }

    /// <summary>
    /// Gets the resolved queue handler instance.
    /// </summary>
    public object Handler { get; }

    /// <summary>
    /// Creates a result for a handler that does not own disposable resources.
    /// </summary>
    /// <param name="handler">The resolved queue handler instance.</param>
    /// <returns>The owned handler result.</returns>
    public static QueueMessageHandlerFactoryResult Create(object handler)
    {
        return new QueueMessageHandlerFactoryResult(handler);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        await this.disposeAsync();
    }
}