// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Builds <see cref="EntityFrameworkJobsOptions"/> instances for the Entity Framework jobs provider.
/// </summary>
public class EntityFrameworkJobsOptionsBuilder
    : OptionsBuilderBase<EntityFrameworkJobsOptions, EntityFrameworkJobsOptionsBuilder>
{
    /// <summary>
    /// Sets the serializer used for durable jobs payloads and properties.
    /// </summary>
    /// <param name="serializer">The serializer to use.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkJobsOptionsBuilder UseSerializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;
        return this;
    }
}