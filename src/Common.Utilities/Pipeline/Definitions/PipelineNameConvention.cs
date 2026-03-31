// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.RegularExpressions;

/// <summary>
/// Provides the default naming convention for packaged pipeline definitions.
/// </summary>
public static class PipelineNameConvention
{
    /// <summary>
    /// Creates the default pipeline name from a pipeline type.
    /// </summary>
    /// <param name="pipelineType">The packaged pipeline definition type.</param>
    /// <returns>The kebab-case pipeline name with a trailing <c>Pipeline</c> suffix removed.</returns>
    /// <example>
    /// <code>
    /// var name = PipelineNameConvention.FromType(typeof(OrderImportPipeline));
    /// // name == "order-import"
    /// </code>
    /// </example>
    public static string FromType(Type pipelineType)
    {
        if (pipelineType is null)
        {
            throw new ArgumentNullException(nameof(pipelineType));
        }

        var name = pipelineType.Name.EndsWith("Pipeline", StringComparison.Ordinal)
            ? pipelineType.Name[..^"Pipeline".Length]
            : pipelineType.Name;

        return Regex.Replace(name, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
    }
}
