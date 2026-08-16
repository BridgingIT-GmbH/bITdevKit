// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Registration;

using System.Reflection;

/// <summary>
/// Defines operations for i registration for event store aggregates and events.
/// </summary>
public interface IRegistrationForEventStoreAggregatesAndEvents
{
    /// <summary>
    /// Executes the register aggregates and events operation.
    /// </summary>
    void RegisterAggregatesAndEvents();
    /// <summary>
    /// Executes the register aggregates and events operation.
    /// </summary>
    /// <param name="assemblies">The assemblies used by the operation.</param>
    void RegisterAggregatesAndEvents(Assembly[] assemblies);
}
