// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Represents entity id.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
[DebuggerDisplay("{Value}")]
public abstract class EntityId<TId> : ValueObject
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public abstract TId Value { get; protected set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Value?.ToString();
    }
}
