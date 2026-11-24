// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Domain.Model;

/// <summary>Captures feature state inside the same native transaction as an entity mutation.</summary>
public interface ILiteDbEntityMutationObserver
{
    /// <summary>Records feature state before the owning session commits.</summary>
    /// <param name="session">The session owning the transaction.</param>
    /// <param name="database">The native database on its owning thread.</param>
    /// <param name="mutation">The entity mutation and its detached stored snapshots.</param>
    void Capture(LiteDbDatabaseSession session, LiteDatabase database, LiteDbEntityMutation mutation);
}

/// <summary>Describes a committed-candidate mutation for atomic feature capture.</summary>
/// <param name="EntityType">The declared entity type.</param>
/// <param name="Entity">The caller entity, including pending domain events.</param>
/// <param name="Before">The stored state before mutation, or null on insert.</param>
/// <param name="After">The stored state after mutation, or null on delete.</param>
/// <param name="CollectionName">The selected entity collection.</param>
/// <param name="Operation">Insert, Update, or Delete.</param>
/// <param name="Source">The originating persistence operation.</param>
/// <param name="AffectedEntityCount">The selected size of a set operation or bulk insertion.</param>
/// <param name="Reason">The reason attached to an explicit restore.</param>
/// <param name="RestoredChangeSetId">The original change set selected for restore.</param>
/// <param name="OperationId">The shared identity of a multi-row operation.</param>
public sealed record LiteDbEntityMutation(Type EntityType, IEntity Entity, BsonDocument Before, BsonDocument After,
    string CollectionName, string Operation, string Source, Guid OperationId, int? AffectedEntityCount = null, string Reason = null, Guid? RestoredChangeSetId = null);

/// <summary>Attaches restore provenance to mutations in an existing scoped transaction.</summary>
/// <param name="Source">The capture source.</param>
/// <param name="Reason">The caller-supplied restore reason.</param>
/// <param name="RestoredChangeSetId">The original change set being restored.</param>
/// <param name="ChangeSetId">The identity to assign to the new restore audit change set.</param>
public sealed record LiteDbMutationCaptureContext(string Source, string Reason, Guid? RestoredChangeSetId, Guid? ChangeSetId = null);
