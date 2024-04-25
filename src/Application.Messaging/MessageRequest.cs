// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

public class MessageRequest
{
    public MessageRequest(IMessage message, CancellationToken cancellationToken)
        : this(message, success => { }, cancellationToken)
    {
    }

    public MessageRequest(IMessage message, Action<bool> onSendComplete, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(message, nameof(message));

        this.Message = message;
        this.OnPublishComplete = onSendComplete;
        this.CancellationToken = cancellationToken;
    }

    public IMessage Message { get; }

    public Action<bool> OnPublishComplete { get; }

    public CancellationToken CancellationToken { get; }

    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
}
