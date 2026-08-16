// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Represents entity cannot be deleted again rule.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="entity">The entity involved in the operation.</param>
public class EntityCannotBeDeletedAgainRule<TEntity>(TEntity entity) : RuleBase
    where TEntity : class, IEntity, IAuditable
{
    private readonly bool? deleted = entity?.AuditState?.IsDeleted();

    /// <inheritdoc/>
    public override string Message => "An already deleted entity cannot be deleted again.";

    /// <inheritdoc/>
    public override Result Execute()
    {
        return Result.SuccessIf(this.deleted is null or false);
    }
}
