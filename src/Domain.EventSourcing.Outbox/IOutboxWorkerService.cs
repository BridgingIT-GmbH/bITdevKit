// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Outbox;

/// <summary>
/// Defines operations for i outbox worker service.
/// </summary>
public interface IOutboxWorkerService
{
    /// <summary>
    /// Executes the do work operation.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DoWorkAsync();
}
