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
        => Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"; // https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_running_in_container-and-dotnet_running_in_containers

    public static bool IsKubernetes()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));

    public static bool IsAzure()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_WEBSITES_ENV));

    public static bool IsAzureFunctions()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_FUNCTIONS_ENV));

    public static bool IsCloud()
        => IsAzure();

    public static bool IsContainerized()
        => IsDocker() || IsKubernetes();

    public static bool IsDocker(this IHostEnvironment env)
        => IsDocker();

    public static bool IsKubernetes(this IHostEnvironment env)
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));

    public static bool IsAzure(this IHostEnvironment env)
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_WEBSITES_ENV));

    public static bool IsAzureFunctions(this IHostEnvironment env)
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_FUNCTIONS_ENV));

    public static bool IsLocalDevelopment(this IHostEnvironment env)
        => env.IsDevelopment() && !env.IsCloud() && !env.IsContainerized();

    public static bool IsCloud(this IHostEnvironment env)
        => IsCloud();

    public static bool IsContainerized(this IHostEnvironment env)
        => env.IsDocker() || env.IsKubernetes();
}