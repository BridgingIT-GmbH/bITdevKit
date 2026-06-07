// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents the minimal job-definition registration contract shared with feature integrations.
/// </summary>
public interface IJobRegistrationStore
{
    /// <summary>
    /// Adds a code-registered job definition.
    /// </summary>
    void Add(JobDefinition definition);

    /// <summary>
    /// Removes a code-registered job definition by name.
    /// </summary>
    /// <param name="jobName">The stable job name.</param>
    void Remove(string jobName);

    /// <summary>
    /// Adds a global behavior type.
    /// </summary>
    void AddGlobalBehavior(Type behaviorType);
}
