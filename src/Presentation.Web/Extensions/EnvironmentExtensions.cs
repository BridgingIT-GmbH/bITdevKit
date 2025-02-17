// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System;
using Microsoft.Extensions.Hosting;

public static class EnvironmentExtensions
{
    private const string AZURE_FUNCTIONS_ENV = "AZURE_FUNCTIONS_ENVIRONMENT";
    private const string AZURE_WEBSITES_ENV = "WEBSITE_SITE_NAME";

    public static bool IsDocker()
        => Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

    public static bool IsKubernetes()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));

    public static bool IsAzure()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_WEBSITES_ENV));

    public static bool IsAzureFunctions()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_FUNCTIONS_ENV));

    public static bool IsLocalDevelopment()
        => IsDevelopment() && !IsCloud() && !IsContainerized();

    public static bool IsCloud()
        => IsAzure();

    public static bool IsContainerized()
        => IsDocker() || IsKubernetes();

    public static bool IsDevelopment()
        => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Development;

    public static bool IsStaging()
        => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Staging;

    public static bool IsProduction()
        => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Production;

    public static bool IsDocker(this IHostEnvironment env)
        => IsDocker();

    public static bool IsKubernetes(this IHostEnvironment env)
        => IsKubernetes();

    public static bool IsAzure(this IHostEnvironment env)
        => IsAzure();

    public static bool IsAzureFunctions(this IHostEnvironment env)
        => IsAzureFunctions();

    public static bool IsLocalDevelopment(this IHostEnvironment env)
        => IsLocalDevelopment();

    public static bool IsCloud(this IHostEnvironment env)
        => IsCloud();

    public static bool IsContainerized(this IHostEnvironment env)
        => IsContainerized();

    public static bool IsDocker(this IHostBuilder builder)
        => IsDocker();

    public static bool IsKubernetes(this IHostBuilder builder)
        => IsKubernetes();

    public static bool IsAzure(this IHostBuilder builder)
        => IsAzure();

    public static bool IsAzureFunctions(this IHostBuilder builder)
        => IsAzureFunctions();

    public static bool IsLocalDevelopment(this IHostBuilder builder)
        => IsLocalDevelopment();

    public static bool IsDevelopment(this IHostBuilder builder)
        => IsDevelopment();

    public static bool IsStaging(this IHostBuilder builder)
        => IsStaging();

    public static bool IsProduction(this IHostBuilder builder)
        => IsProduction();

    public static bool IsCloud(this IHostBuilder builder)
        => IsCloud();

    public static bool IsContainerized(this IHostBuilder builder)
        => IsContainerized();
}