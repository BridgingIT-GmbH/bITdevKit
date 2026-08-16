// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents message request.
/// </summary>
public class MessageRequest
{
    /// <summary>
    /// Initializes a new instance of the <c>MessageRequest</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public MessageRequest(IMessage message, CancellationToken cancellationToken)
        : this(message, success => { }, cancellationToken) { }

    /// <summary>
    /// Initializes a new instance of the <c>MessageRequest</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="onSendComplete">The on send complete used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public MessageRequest(IMessage message, Action<bool> onSendComplete, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(message, nameof(message));

        this.Message = message;
        this.OnPublishComplete = onSendComplete;
        this.CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the message.
    /// </summary>
    public IMessage Message { get; }

    /// <summary>
    /// Gets the on publish complete.
    /// </summary>
    public Action<bool> OnPublishComplete { get; }

    /// <summary>
    /// Gets the cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
}
