// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents message handler factory result.
/// </summary>
public sealed class MessageHandlerFactoryResult : IDisposable, IAsyncDisposable
{
    private readonly Func<ValueTask> disposeAsync;
    private int disposed;

    /// <summary>
    /// Initializes a new instance of the <c>MessageHandlerFactoryResult</c> class.
    /// </summary>
    /// <param name="handler">The handler used by the operation.</param>
    /// <param name="disposeAsync">The dispose used by the operation.</param>
    public MessageHandlerFactoryResult(object handler, Func<ValueTask> disposeAsync = null)
    {
        EnsureArg.IsNotNull(handler, nameof(handler));

        this.Handler = handler;
        this.disposeAsync = disposeAsync ?? (() => ValueTask.CompletedTask);
    }

    /// <summary>
    /// Gets the handler.
    /// </summary>
    public object Handler { get; }

    /// <summary>
    /// Creates .
    /// </summary>
    /// <param name="handler">The handler used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static MessageHandlerFactoryResult Create(object handler)
    {
        return new MessageHandlerFactoryResult(handler);
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        this.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        await this.disposeAsync();
    }
}
