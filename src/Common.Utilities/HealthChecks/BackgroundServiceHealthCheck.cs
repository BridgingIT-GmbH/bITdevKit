// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

/// <summary>
/// Reports health for a hosted service by inspecting its registered instance and, for <see cref="BackgroundService" />, its execution task.
/// </summary>
/// <typeparam name="TService">The hosted service type.</typeparam>
/// <example>
/// <code>
/// services.AddHealthChecks()
///     .AddCheck&lt;BackgroundServiceHealthCheck&lt;WorkerService&gt;&gt;("WorkerService");
/// </code>
/// </example>
public sealed class BackgroundServiceHealthCheck<TService>(
    IEnumerable<IHostedService> hostedServices,
    IHostApplicationLifetime applicationLifetime = null) : IHealthCheck
    where TService : class, IHostedService
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var serviceName = typeof(TService).Name;
        var service = hostedServices.OfType<TService>().FirstOrDefault();
        if (service is null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"{serviceName} is not registered as a hosted service."));
        }

        var backgroundService = service as BackgroundService;
        var executeTask = backgroundService?.ExecuteTask;
        var trackedTask = GetMonitoredTask(service);
        var monitoredTask = trackedTask.Task ?? executeTask;
        if (backgroundService is null && monitoredTask is null)
        {
            return Task.FromResult(HealthCheckResult.Healthy($"{serviceName} is registered."));
        }

        var data = new Dictionary<string, object>
        {
            ["service"] = typeof(TService).FullName,
            ["state"] = ToHealthState(monitoredTask),
            ["source"] = trackedTask.Source ?? nameof(BackgroundService.ExecuteTask),
        };

        if (executeTask is not null)
        {
            data["executeTaskState"] = ToHealthState(executeTask);
            data["executeTaskStatus"] = executeTask.Status.ToString();
        }

        if (trackedTask.Task is not null)
        {
            data[$"{trackedTask.Source}State"] = ToHealthState(trackedTask.Task);
            data[$"{trackedTask.Source}Status"] = trackedTask.Task.Status.ToString();
        }

        if (monitoredTask is null)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"{serviceName} has not started yet.", data: data));
        }

        if (monitoredTask.IsFaulted)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"{serviceName} faulted.", monitoredTask.Exception?.GetBaseException(), data));
        }

        if (monitoredTask.IsCanceled)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"{serviceName} was canceled.", data: data));
        }

        if (trackedTask.Task is null && executeTask?.IsCompleted == true && applicationLifetime?.ApplicationStarted.IsCancellationRequested == true)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"{serviceName} completed without a running startup task.", data: data));
        }

        var description = monitoredTask.IsCompleted
            ? $"{serviceName} completed."
            : $"{serviceName} is running.";

        return Task.FromResult(HealthCheckResult.Healthy(description, data));
    }

    private static (Task Task, string Source) GetMonitoredTask(TService service)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var serviceType = service.GetType();
        var startupTask = serviceType.GetField("startupTask", Flags)?.GetValue(service) as Task;
        if (startupTask is not null)
        {
            return (startupTask, "startupTask");
        }

        var processingTask = serviceType.GetField("processingTask", Flags)?.GetValue(service) as Task;
        return processingTask is null ? (null, null) : (processingTask, "processingTask");
    }

    private static string ToHealthState(Task task)
    {
        if (task is null)
        {
            return "NotStarted";
        }

        if (task.IsFaulted)
        {
            return "Faulted";
        }

        if (task.IsCanceled)
        {
            return "Canceled";
        }

        if (task.IsCompleted)
        {
            return "Completed";
        }

        return "Running";
    }
}

/// <summary>
/// Registers background-service health checks without duplicating entries when feature builders are called more than once.
/// </summary>
/// <example>
/// <code>
/// services.TryAddBackgroundServiceHealthCheck&lt;WorkerService&gt;("WorkerService");
/// </code>
/// </example>
public static class BackgroundServiceHealthCheckServiceCollectionExtensions
{
    /// <summary>
    /// Adds a health check for a hosted service when a check with the same name has not already been registered through this helper.
    /// </summary>
    /// <typeparam name="TService">The hosted service type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The health check name.</param>
    /// <param name="failureStatus">The status reported when the check fails.</param>
    /// <param name="tags">Health check tags.</param>
    /// <returns>The same <paramref name="services" /> instance for chaining.</returns>
    public static IServiceCollection TryAddBackgroundServiceHealthCheck<TService>(
        this IServiceCollection services,
        string name,
        HealthStatus? failureStatus = null,
        IEnumerable<string> tags = null)
        where TService : class, IHostedService
    {
        if (services.Any(d =>
            d.ServiceType == typeof(BackgroundServiceHealthCheckRegistrationMarker) &&
            d.ImplementationInstance is BackgroundServiceHealthCheckRegistrationMarker marker &&
            StringComparer.Ordinal.Equals(marker.Name, name)))
        {
            return services;
        }

        services.AddSingleton(new BackgroundServiceHealthCheckRegistrationMarker(name));
        services.AddHealthChecks()
            .AddCheck<BackgroundServiceHealthCheck<TService>>(name, failureStatus, tags);

        return services;
    }

    private sealed record BackgroundServiceHealthCheckRegistrationMarker(string Name);
}
