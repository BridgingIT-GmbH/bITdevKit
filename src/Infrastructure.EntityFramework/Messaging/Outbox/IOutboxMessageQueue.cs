// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Defines operations for i outbox message queue.
/// </summary>
public interface IOutboxMessageQueue
{
    /// <summary>
    /// Executes the enqueue operation.
    /// </summary>
    /// <param name="messageId">The message id used by the operation.</param>
    void Enqueue(string messageId);
}
