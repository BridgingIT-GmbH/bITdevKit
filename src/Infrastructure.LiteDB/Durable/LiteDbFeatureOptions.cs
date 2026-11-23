// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;

/// <summary>Configures bounded native record storage for a durable DevKit feature.</summary>
public class LiteDbFeatureOptions
{
    /// <summary>Gets or sets the named database registration.</summary>
    public string DatabaseName { get; set; } = "default";

    /// <summary>Gets or sets the feature's collection prefix.</summary>
    public string CollectionPrefix { get; set; }

    /// <summary>Gets or sets the maximum records materialized by a command.</summary>
    public int MaxMaterializedRows { get; set; } = 10000;

    /// <summary>Gets or sets the serializer used for feature payload boundaries.</summary>
    public ISerializer Serializer { get; set; }
}
