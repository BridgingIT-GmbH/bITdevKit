// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

/// <summary>
/// Defines operations for i outbox message worker repository.
/// </summary>
/// <typeparam name="OutboxMessage">The outbox message type.</typeparam>
public interface IOutboxMessageWorkerRepository : IGenericRepository<OutboxMessage>;
