// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System;
using System.Reflection;
using Microsoft.Extensions.Hosting;

public static class EnvironmentExtensions
{
    private const string AZURE_FUNCTIONS_ENV = "AZURE_FUNCTIONS_ENVIRONMENT";
    private const string AZURE_WEBSITES_ENV = "WEBSITE_SITE_NAME";

    public static bool IsDocker()
    {
        return Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"; // https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_running_in_container-and-dotnet_running_in_containers
    }

    public static bool IsKubernetes()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
    }

    public static bool IsAzure()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_WEBSITES_ENV));
    }

    public static bool IsAzureFunctions()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_FUNCTIONS_ENV));
    }

    public static bool IsCloud()
    {
        return IsAzure();
    }

    public static bool IsContainerized()
    {
        return IsDocker() || IsKubernetes();
    }

    public static bool IsDocker(this IHostEnvironment env)
    {
        return IsDocker();
    }

    public static bool IsKubernetes(this IHostEnvironment env)
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
    }

    public static bool IsAzure(this IHostEnvironment env)
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_WEBSITES_ENV));
    }

    public static bool IsAzureFunctions(this IHostEnvironment env)
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AZURE_FUNCTIONS_ENV));
    }

    public static bool IsLocalDevelopment(this IHostEnvironment env)
    {
        return env?.IsDevelopment() == true && env?.IsCloud() == false && env?.IsContainerized() == false;
    }

    public static bool IsTesting(this IHostEnvironment env)
    {
        return env?.IsEnvironment("Testing") == true || env?.IsEnvironment("Test") == true || env?.IsStaging() == true;
    }

    public static bool IsCloud(this IHostEnvironment env)
    {
        return IsCloud();
    }

    public static bool IsContainerized(this IHostEnvironment env)
    {
        return env?.IsDocker() == true || env?.IsKubernetes() == true;
    }

    public static bool IsBuildTimeOpenApiGeneration() // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-9.0&tabs=visual-studio%2Cvisual-studio-code#customizing-run-time-behavior-during-build-time-document-generation
    {
        return Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
    }
}