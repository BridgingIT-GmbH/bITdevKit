// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

public sealed class MessageHandlerFactoryResult : IDisposable, IAsyncDisposable
{
    private readonly Func<ValueTask> disposeAsync;
    private int disposed;

    public MessageHandlerFactoryResult(object handler, Func<ValueTask> disposeAsync = null)
    {
        EnsureArg.IsNotNull(handler, nameof(handler));

        this.Handler = handler;
        this.disposeAsync = disposeAsync ?? (() => ValueTask.CompletedTask);
    }

    public object Handler { get; }

    public static MessageHandlerFactoryResult Create(object handler)
    {
        return new MessageHandlerFactoryResult(handler);
    }

    public void Dispose()
    {
        this.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        await this.disposeAsync();
    }
}