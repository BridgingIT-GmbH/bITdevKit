// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
///     Writes separate summaries of application and system API endpoint sets during host startup.
/// </summary>
/// <param name="endpoints">The endpoint sets registered for the application host.</param>
/// <param name="loggerFactory">The factory used to create the endpoint registration logger.</param>
/// <example>
/// <code>
/// services.AddHostedService&lt;EndpointStartupDiagnosticsService&gt;();
/// </code>
/// </example>
public sealed class EndpointStartupDiagnosticsService(
    IEnumerable<IEndpoints> endpoints,
    ILoggerFactory loggerFactory) : IHostedService
{
    private const string DevKitNamespace = "BridgingIT.DevKit.Presentation.Web";
    private const string LogKey = "REQ";
    private readonly ILogger logger = loggerFactory.CreateLogger(LogKey);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var endpointTypes = endpoints
            .Select(endpoint => endpoint.GetType())
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        var devKitEndpointTypes = endpointTypes.Where(IsDevKitEndpoint).ToArray();
        var applicationEndpointTypes = endpointTypes.Where(type => !IsDevKitEndpoint(type)).ToArray();

        LogEndpointSummary(this.logger, GetApplicationEndpointNames(applicationEndpointTypes));
        LogSystemEndpointSummary(this.logger, devKitEndpointTypes.Select(GetDevKitEndpointName).ToArray());

        return Task.CompletedTask;
    }

    private static string[] GetApplicationEndpointNames(IEnumerable<Type> endpointTypes)
    {
        var types = endpointTypes.ToArray();
        var duplicateTypeNames = types
            .GroupBy(type => type.Name, StringComparer.Ordinal)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        return types
            .Select(type => GetDisplayName(type, duplicateTypeNames))
            .ToArray();
    }

    private static string GetDevKitEndpointName(Type type)
    {
        var namespaceName = type.Namespace ?? string.Empty;
        var relativeNamespace = namespaceName.Length > DevKitNamespace.Length
            ? namespaceName[(DevKitNamespace.Length + 1)..]
            : string.Empty;
        var typeName = GetTypeName(type);

        return string.IsNullOrWhiteSpace(relativeNamespace) ? typeName : $"{relativeNamespace}.{typeName}";
    }

    private static bool IsDevKitEndpoint(Type type)
    {
        return string.Equals(type.Namespace, DevKitNamespace, StringComparison.Ordinal) ||
            type.Namespace?.StartsWith($"{DevKitNamespace}.", StringComparison.Ordinal) == true;
    }

    private static void LogEndpointSummary(ILogger logger, IReadOnlyCollection<string> endpointNames)
    {
        if (endpointNames.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "[{LogKey}] api endpoints added (count={EndpointCount}, endpoints={Endpoints})",
            LogKey,
            endpointNames.Count,
            string.Join(",", endpointNames));
    }

    private static void LogSystemEndpointSummary(ILogger logger, IReadOnlyCollection<string> endpointNames)
    {
        if (endpointNames.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "[{LogKey}] system api endpoints added (count={EndpointCount}, endpoints={Endpoints})",
            LogKey,
            endpointNames.Count,
            string.Join(",", endpointNames));
    }

    private static string GetDisplayName(Type type, IReadOnlySet<string> duplicateTypeNames)
    {
        var typeName = GetTypeName(type);
        if (!duplicateTypeNames.Contains(type.Name))
        {
            return typeName;
        }

        var qualifier = type.DeclaringType?.Name ?? type.Namespace;
        return string.IsNullOrWhiteSpace(qualifier) ? typeName : $"{qualifier}.{typeName}";
    }

    private static string GetTypeName(Type type)
    {
        var genericArityIndex = type.Name.IndexOf('`', StringComparison.Ordinal);
        var typeName = genericArityIndex < 0 ? type.Name : type.Name[..genericArityIndex];

        return type.IsGenericType
            ? $"{typeName}<{string.Join(",", type.GetGenericArguments().Select(GetTypeName))}>"
            : typeName;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
