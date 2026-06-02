// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;

/// <summary>
/// Provides process/environment helpers used by startup and registration code.
/// </summary>
/// <example>
/// <code>
/// if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration())
/// {
///     services.AddHostedService&lt;Worker&gt;();
/// }
/// </code>
/// </example>
public static class EnvironmentExtensions
{
    /// <summary>
    /// Detects when the app is running under OpenAPI build-time document generation.
    /// </summary>
    /// <returns><c>true</c> when the entry assembly is the OpenAPI document generator.</returns>
    /// <example>
    /// <code>
    /// var skipHostedServices = EnvironmentExtensions.IsBuildTimeOpenApiGeneration();
    /// </code>
    /// </example>
    public static bool IsBuildTimeOpenApiGeneration() =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
