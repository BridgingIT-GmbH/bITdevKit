// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Orchestrations;

/// <summary>
/// Builds <see cref="EntityFrameworkOrchestrationOptions"/> instances for the Entity Framework orchestration provider.
/// </summary>
public class EntityFrameworkOrchestrationOptionsBuilder
    : OptionsBuilderBase<EntityFrameworkOrchestrationOptions, EntityFrameworkOrchestrationOptionsBuilder>
{
    /// <summary>
    /// Sets the serializer used for durable orchestration payloads.
    /// </summary>
    /// <param name="serializer">The serializer to use.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkOrchestrationOptionsBuilder UseSerializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;

        return this;
    }
}