// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents the runtime options used by the Entity Framework jobs persistence provider.
/// </summary>
public class EntityFrameworkJobsOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets the serializer used for durable jobs payloads and properties.
    /// </summary>
    public ISerializer Serializer { get; set; }
}