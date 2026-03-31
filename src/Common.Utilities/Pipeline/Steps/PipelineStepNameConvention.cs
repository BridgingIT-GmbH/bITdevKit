// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.RegularExpressions;

/// <summary>
/// Provides the default naming convention for pipeline steps.
/// </summary>
public static class PipelineStepNameConvention
{
    /// <summary>
    /// Creates the default step name from a step type.
    /// </summary>
    /// <param name="stepType">The step implementation type.</param>
    /// <returns>The kebab-case step name with a trailing <c>Step</c> suffix removed.</returns>
    /// <example>
    /// <code>
    /// var name = PipelineStepNameConvention.FromType(typeof(PersistOrdersStep));
    /// // name == "persist-orders"
    /// </code>
    /// </example>
    public static string FromType(Type stepType)
    {
        if (stepType is null)
        {
            throw new ArgumentNullException(nameof(stepType));
        }

        var name = stepType.Name.EndsWith("Step", StringComparison.Ordinal)
            ? stepType.Name[..^"Step".Length]
            : stepType.Name;

        return Regex.Replace(name, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
    }
}
