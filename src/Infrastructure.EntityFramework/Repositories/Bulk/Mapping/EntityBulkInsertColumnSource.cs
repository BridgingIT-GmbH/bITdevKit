// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

/// <summary>Identifies how a native bulk insert column obtains its value.</summary>
/// <example><code>var source = EntityBulkInsertColumnSource.ShadowProvider;</code></example>
public enum EntityBulkInsertColumnSource
{
    /// <summary>The value comes from a CLR property on the root entity.</summary>
    ClrProperty = 0,

    /// <summary>The value comes from a CLR property on a same-table owned reference.</summary>
    OwnedProperty = 1,

    /// <summary>The value is a constant supplied by EF metadata, such as a TPH discriminator.</summary>
    MetadataConstant = 2,

    /// <summary>The value comes from an explicit deterministic shadow-property provider.</summary>
    ShadowProvider = 3,
}
