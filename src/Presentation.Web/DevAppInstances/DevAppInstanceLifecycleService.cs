namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Holds the process-scoped runtime state of an active development instance.
/// </summary>
internal sealed class DevAppInstanceRuntimeState : IDisposable
{
    public DevAppInstanceLease Lease { get; set; }

    public List<DevAppInstanceLogStep> Steps { get; } = [];

    public void Dispose()
    {
        this.Lease?.Dispose();
        this.Lease = null;
    }
}

/// <summary>
/// Writes why worktree isolation is inactive, plus any configuration warnings, through the host logger.
/// Registered only for inactive contexts in the Development environment.
/// </summary>
internal sealed class DevAppInstanceStatusLogService(
    DevAppInstanceContext context,
    IHostApplicationLifetime lifetime,
    IServiceProvider services,
    ILogger<DevAppInstanceStatusLogService> logger) : IHostedService
{
    private CancellationTokenRegistration startedRegistration;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.startedRegistration = lifetime.ApplicationStarted.Register(this.OnStarted);
        try
        {
            logger.LogDebug(
                "[{LogKey}] worktree isolation inactive (reason={InactiveReason}, app={AppId})",
                DevAppInstanceLifecycleService.LogKey,
                context.InactiveReason,
                context.AppId);
            foreach (var step in context.LogSteps)
            {
                DevAppInstanceLifecycleService.WriteStep(logger, step);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[{LogKey}] worktree status log failed", DevAppInstanceLifecycleService.LogKey);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.startedRegistration.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the URLs a host listens on, replacing wildcard hosts with localhost.
    /// </summary>
    /// <param name="addresses">The bound listener addresses.</param>
    /// <returns>The browsable URLs, HTTPS first.</returns>
    internal static IReadOnlyList<string> GetListenUrls(IEnumerable<string> addresses) =>
        [.. (addresses ?? [])
            .Select(a => a.Replace("://*", "://localhost", StringComparison.Ordinal).Replace("://+", "://localhost", StringComparison.Ordinal).Replace("://[::]", "://localhost", StringComparison.Ordinal).Replace("://0.0.0.0", "://localhost", StringComparison.Ordinal).TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(a => a.StartsWith("https://", StringComparison.OrdinalIgnoreCase))];

    private void OnStarted()
    {
        try
        {
            var urls = GetListenUrls(services.GetService<IServer>()?.Features.Get<IServerAddressesFeature>()?.Addresses);
            if (urls.Count == 0)
            {
                return;
            }

            var template = "[{LogKey}] worktree isolation inactive, host listening (app={AppId})" +
                "\n      Open:       {OpenUrl}";
            var arguments = new List<object> { DevAppInstanceLifecycleService.LogKey, context.AppId, urls[0] };
            foreach (var url in urls.Skip(1))
            {
                template += "\n      Also:       {AlsoUrl" + arguments.Count + "}";
                arguments.Add(url);
            }

            if (DevAppInstanceLifecycleService.HasDashboard(services))
            {
                template += "\n      Dashboard:  {DashboardUrl}";
                arguments.Add($"{urls[0]}/_bdk/dashboard");
            }

#pragma warning disable CA2254 // The template is composed from constant fragments.
            logger.LogInformation(template, [.. arguments]);
#pragma warning restore CA2254
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[{LogKey}] worktree status urls unavailable", DevAppInstanceLifecycleService.LogKey);
        }
    }
}

/// <summary>
/// Writes the resolution steps through the host logger, verifies the listener addresses and reports readiness.
/// </summary>
internal sealed class DevAppInstanceLifecycleService(
    DevAppInstanceContext context,
    DevAppInstanceRuntimeState state,
    IHostApplicationLifetime lifetime,
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<DevAppInstanceLifecycleService> logger) : IHostedService
{
    internal const string LogKey = "BDK";

    private CancellationTokenRegistration startedRegistration;

    private Timer stopRequestTimer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var step in context.LogSteps.Concat(state.Steps))
        {
            WriteStep(logger, step);
        }

        this.startedRegistration = lifetime.ApplicationStarted.Register(this.OnStarted);
        if (!string.IsNullOrEmpty(context.LeasePath))
        {
            TryDelete(DevAppInstanceLease.GetStopRequestFile(context.LeasePath));
            this.stopRequestTimer = new Timer(_ => this.CheckStopRequest(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.stopRequestTimer?.Dispose();
        this.startedRegistration.Dispose();
        state.Dispose();
        if (!string.IsNullOrEmpty(context.LeasePath))
        {
            TryDelete(DevAppInstanceLease.GetStopRequestFile(context.LeasePath));
        }

        return Task.CompletedTask;
    }

    internal static void WriteStep(ILogger logger, DevAppInstanceLogStep step)
    {
        const string debugPrefix = "[debug] ";
        const string warningPrefix = "[warning] ";
        var (level, text) = step.Template switch
        {
            var t when t.StartsWith(debugPrefix, StringComparison.Ordinal) => (LogLevel.Debug, t[debugPrefix.Length..]),
            var t when t.StartsWith(warningPrefix, StringComparison.Ordinal) => (LogLevel.Warning, t[warningPrefix.Length..]),
            var t => (LogLevel.Information, t),
        };
#pragma warning disable CA2254 // Templates come from the resolver and are constant per step.
        logger.Log(level, "[{LogKey}] worktree " + text, [LogKey, .. step.Arguments ?? []]);
#pragma warning restore CA2254
    }

    internal static void LogFailure(ILogger logger, DevAppInstanceException exception, string recoveryAction)
    {
        logger.LogError(
            "[{LogKey}] worktree failure: {ErrorMessage} (code={ErrorCode}, app={AppId}, slot={Slot}) {RecoveryAction}",
            LogKey,
            exception.Message,
            exception.Code,
            exception.AppId,
            exception.Slot,
            recoveryAction);
    }

    internal static bool AddressesMatch(IEnumerable<string> addresses, DevAppInstanceContext context)
    {
        var bound = (addresses ?? [])
            .Select(a => Uri.TryCreate(a.Replace("://*", "://localhost", StringComparison.Ordinal).Replace("://+", "://localhost", StringComparison.Ordinal), UriKind.Absolute, out var uri) ? uri : null)
            .Where(u => u is not null)
            .ToList();

        return bound.Count > 0 &&
            bound.All(u => (u.Scheme == Uri.UriSchemeHttps && u.Port == context.HttpsPort) || (u.Scheme == Uri.UriSchemeHttp && u.Port == context.HttpPort)) &&
            bound.Any(u => u.Scheme == Uri.UriSchemeHttps && u.Port == context.HttpsPort);
    }

    private void CheckStopRequest()
    {
        var path = DevAppInstanceLease.GetStopRequestFile(context.LeasePath);
        if (!File.Exists(path))
        {
            return;
        }

        this.stopRequestTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        TryDelete(path);
        logger.LogInformation("[{LogKey}] worktree stop requested by bdk dev stop (app={AppId}, slot={Slot})", LogKey, context.AppId, context.Slot);
        lifetime.StopApplication();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover request file is removed again at the next start.
        }
    }

    private void OnStarted()
    {
        List<string> addresses;
        try
        {
            addresses = services.GetService<IServer>()?.Features.Get<IServerAddressesFeature>()?.Addresses?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            // Diagnostics only: the managed URLs were already applied, so an unreadable address list must not stop the host.
            logger.LogWarning(ex, "[{LogKey}] worktree listener check skipped, addresses unavailable (app={AppId}, slot={Slot})", LogKey, context.AppId, context.Slot);

            return;
        }

        if (!AddressesMatch(addresses, context))
        {
            logger.LogWarning(
                "[{LogKey}] worktree isolation incomplete, the host listens on {Addresses} instead of {HttpsUrl};{HttpUrl} (app={AppId}, slot={Slot}) Remove explicit Kestrel endpoint or Listen configuration so the managed URLs apply.",
                LogKey,
                string.Join(";", addresses),
                context.HttpsUrl,
                context.HttpUrl,
                context.AppId,
                context.Slot);

            return;
        }

        logger.LogInformation("[{LogKey}] worktree listener verified (addresses={Addresses})", LogKey, string.Join(";", addresses));
        try
        {
            this.WriteSummary();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{LogKey}] worktree startup summary unavailable (app={AppId}, slot={Slot})", LogKey, context.AppId, context.Slot);
        }
    }

    private void WriteSummary()
    {
        var databases = string.Join(", ", context.Resources.Where(r => !r.Shared && DevAppInstanceResolver.IsDatabaseKind(r.Kind)).Select(r => r.Target).Distinct(StringComparer.OrdinalIgnoreCase));
        var dashboard = HasDashboard(services) ? $"{context.HttpsUrl}/_bdk/dashboard" : null;
        var issuer = context.ManagedValues.TryGetValue("Authentication:Authority", out var authority) ? authority : configuration["Authentication:Authority"];
        var replaced = context.LogSteps.FirstOrDefault(s => s.Template.StartsWith("baseline urls replaced", StringComparison.Ordinal))?.Arguments?.FirstOrDefault()?.ToString();

        var template = "[{LogKey}] worktree instance ready (app={AppId}, slot={Slot}, worktree={WorktreeId})" +
            "\n      Open:       {OpenUrl}" +
            "\n      HTTP:       {HttpUrl} (redirects to HTTPS)";
        var arguments = new List<object> { LogKey, context.AppId, context.Slot, context.WorktreeId, context.HttpsUrl, context.HttpUrl };
        if (dashboard is not null)
        {
            template += "\n      Dashboard:  {DashboardUrl}";
            arguments.Add(dashboard);
        }

        if (!string.IsNullOrEmpty(issuer))
        {
            template += "\n      Issuer:     {Issuer}";
            arguments.Add(issuer);
        }

        if (!string.IsNullOrEmpty(databases))
        {
            template += "\n      Databases:  {Databases}";
            arguments.Add(databases);
        }

        template += "\n      Worktree:   {WorktreeRoot}";
        arguments.Add(context.WorktreeRoot);
        if (!string.IsNullOrEmpty(replaced))
        {
            template += "\n      Note:       launch profile URL {ReplacedUrls} was replaced; an IDE browser may still open it";
            arguments.Add(replaced);
        }

#pragma warning disable CA2254 // The template is composed from constant fragments.
        logger.LogInformation(template, [.. arguments]);
#pragma warning restore CA2254
    }

    internal static bool HasDashboard(IServiceProvider services)
    {
        try
        {
            return services.GetService<EndpointDataSource>()?.Endpoints
                .OfType<RouteEndpoint>()
                .Any(e => e.RoutePattern.RawText?.TrimStart('/').StartsWith("_bdk/dashboard", StringComparison.OrdinalIgnoreCase) == true) == true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
