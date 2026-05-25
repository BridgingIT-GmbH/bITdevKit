// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public interface ITodoItemOrchestrationCoordinator
{
    Task EnsureStartedAsync(TodoItem todoItem, CancellationToken cancellationToken = default);

    Task SynchronizeAsync(TodoItem todoItem, CancellationToken cancellationToken = default);

    Task HandleDeletedAsync(TodoItem todoItem, CancellationToken cancellationToken = default);
}