// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System;
using System.Reflection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods and helpers to detect the current hosting environment (local, cloud, containerized, etc.).
/// Useful for conditional startup logic, diagnostics and environment-specific configuration.
/// </summary>
public static class EnvironmentExtensions
{
    private const string AZURE_FUNCTIONS_ENV = "AZURE_FUNCTIONS_ENVIRONMENT";
    private const string AZURE_WEBSITES_ENV = "WEBSITE_SITE_NAME";

    /// <summary>
    /// Determines if the app is currently running in a Docker container (via <c>DOTNET_RUNNING_IN_CONTAINER</c> env var).
    /// </summary>
    public static bool IsDocker() =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

    /// <summary>
    /// Determines if the app is currently running in Kubernetes (via <c>KUBERNETES_SERVICE_HOST</c> env var).
    /// </summary>
    public static bool IsKubernetes() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));

    /// <summary>
    /// Determines if the app is currently running in Azure App Service (via <c>WEBSITE_SITE_NAME</c> env var).
    /// </summary>
    public static bool IsAzure() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_WEBSITES_ENV));

    /// <summary>
    /// Determines if the app is currently running in Azure Functions (via <c>AZURE_FUNCTIONS_ENVIRONMENT</c> env var).
    /// </summary>
    public static bool IsAzureFunctions() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_FUNCTIONS_ENV));

    /// <summary>
    /// Determines if the app runs in any cloud environment. Currently only checks for Azure.
    /// </summary>
    public static bool IsCloud() => IsAzure();

    /// <summary>
    /// Determines if the app is containerized (Docker or Kubernetes).
    /// </summary>
    public static bool IsContainerized() =>
        IsDocker() || IsKubernetes();

    // ----------- IHostEnvironment extension overloads ---------------- //

    /// <inheritdoc cref="IsDocker"/>
    public static bool IsDocker(this IHostEnvironment env) => IsDocker();

    /// <inheritdoc cref="IsKubernetes"/>
    public static bool IsKubernetes(this IHostEnvironment env) =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));

    /// <inheritdoc cref="IsAzure"/>
    public static bool IsAzure(this IHostEnvironment env) =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_WEBSITES_ENV));

    /// <inheritdoc cref="IsAzureFunctions"/>
    public static bool IsAzureFunctions(this IHostEnvironment env) =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_FUNCTIONS_ENV));

    /// <summary>
    /// Determines if the host environment is local development.
    /// True if <see cref="IHostEnvironment.IsDevelopment"/>, not cloud, not containerized,
    /// or if the app is running under OpenAPI build-time doc generation.
    /// </summary>
    public static bool IsLocalDevelopment(this IHostEnvironment env) =>
        (env?.IsDevelopment() == true && env?.IsCloud() == false && env?.IsContainerized() == false)
        || IsBuildTimeOpenApiGeneration();

    /// <summary>
    /// Determines if the environment represents a testing context.
    /// Matches "Testing", "Test" or <see cref="IHostEnvironment.IsStaging"/>.
    /// </summary>
    public static bool IsTesting(this IHostEnvironment env) =>
        env?.IsEnvironment("Testing") == true
        || env?.IsEnvironment("Test") == true
        || env?.IsStaging() == true;

    /// <inheritdoc cref="IsCloud"/>
    public static bool IsCloud(this IHostEnvironment env) => IsCloud();

    /// <inheritdoc cref="IsContainerized"/>
    public static bool IsContainerized(this IHostEnvironment env) =>
        env?.IsDocker() == true || env?.IsKubernetes() == true;

    /// <summary>
    /// Detects when the app is running under **OpenAPI build-time document generation**
    /// (special case for Swagger in .NET 9, see official docs).
    /// </summary>
    /// <remarks>
    /// This checks whether the entry assembly name is <c>GetDocument.Insider</c>,
    /// which is used by the tooling when generating OpenAPI docs at build time.
    /// </remarks>
    public static bool IsBuildTimeOpenApiGeneration() =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}