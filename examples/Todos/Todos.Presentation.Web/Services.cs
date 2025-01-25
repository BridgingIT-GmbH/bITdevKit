namespace BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web;

using System.Diagnostics;
using Microsoft.AspNetCore.Components.Server.Circuits;

public class ServerMonitorService : IDisposable
{
    private readonly Process currentProcess;
    private DateTime processStartTime;
    private PerformanceCounter cpuCounter;

    public ServerMonitorService()
    {
        this.currentProcess = Process.GetCurrentProcess();
        this.processStartTime = this.currentProcess.StartTime;

#pragma warning disable CA1416 // Validate platform compatibility
        this.cpuCounter = new PerformanceCounter(
        "Processor",
        "% Processor Time",
        "_Total");
#pragma warning restore CA1416 // Validate platform compatibility
    }

    public ServerMetrics GetMetrics()
    {
        return new ServerMetrics
        {
#pragma warning disable CA1416 // Validate platform compatibility
            CpuUsage = Math.Round(this.cpuCounter.NextValue(), 2),
#pragma warning restore CA1416 // Validate platform compatibility
            TotalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024,
            UsedMemory = this.currentProcess.WorkingSet64 / 1024 / 1024,
            ThreadCount = this.currentProcess.Threads.Count,
            ProcessUptime = DateTime.Now - this.processStartTime,
            //ActiveConnections = Circuit.Count // If you track active circuits
        };
    }

    public void Dispose()
    {
        this.cpuCounter?.Dispose();
        this.currentProcess?.Dispose();
    }
}

// Models/ServerMetrics.cs
public class ServerMetrics
{
    public double CpuUsage { get; set; }
    public long TotalMemory { get; set; }
    public long UsedMemory { get; set; }
    public int ThreadCount { get; set; }
    public TimeSpan ProcessUptime { get; set; }
    public int ActiveConnections { get; set; }
}

// Services/CircuitMonitorService.cs
public class CircuitMonitorService : CircuitHandler
{
    private readonly ILogger<CircuitMonitorService> logger;
    private CircuitInfo currentCircuit;
    public event Action<CircuitInfo> OnCircuitInfoUpdated;

    public CircuitMonitorService(ILogger<CircuitMonitorService> logger)
    {
        this.logger = logger;
        this.currentCircuit = new CircuitInfo();
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        this.currentCircuit.CircuitId = circuit.Id;
        this.currentCircuit.ConnectedTime = DateTime.UtcNow;
        this.currentCircuit.ConnectionId = circuit.Id;
        this.NotifyStateChanged();
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Circuit closed: {CircuitId}", circuit.Id);
        return base.OnCircuitClosedAsync(circuit, cancellationToken);
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        this.currentCircuit.LastActivity = "Connection Up";
        this.NotifyStateChanged();
        return base.OnConnectionUpAsync(circuit, cancellationToken);
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        this.currentCircuit.LastActivity = "Connection Down";
        this.NotifyStateChanged();
        return base.OnConnectionDownAsync(circuit, cancellationToken);
    }

    public CircuitInfo GetCurrentCircuitInfo()
    {
        this.currentCircuit.Uptime = DateTime.UtcNow - this.currentCircuit.ConnectedTime;
        this.currentCircuit.MemoryUsage = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024; // MB
        return this.currentCircuit;
    }

    private void NotifyStateChanged() => this.OnCircuitInfoUpdated?.Invoke(this.GetCurrentCircuitInfo());
}

// Models/CircuitInfo.cs
public class CircuitInfo
{
    public string CircuitId { get; set; } = string.Empty;
    public DateTime ConnectedTime { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public long MemoryUsage { get; set; }
    public TimeSpan Uptime { get; set; }
    public int TotalRenders { get; set; }
    public string LastActivity { get; set; } = string.Empty;
}

//public interface ILogoutService
//{
//    Task LogoutAsync();
//}

//public class LogoutService : ILogoutService
//{
//    private readonly IHttpContextAccessor httpContextAccessor;
//    private readonly NavigationManager navigationManager;

//    public LogoutService(
//        IHttpContextAccessor httpContextAccessor,
//        NavigationManager navigationManager)
//    {
//        this.httpContextAccessor = httpContextAccessor;
//        this.navigationManager = navigationManager;
//    }

//    public async Task LogoutAsync()
//    {
//        if (this.httpContextAccessor.HttpContext != null)
//        {
//            await this.httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
//            await this.httpContextAccessor.HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
//        }

//        this.navigationManager.NavigateTo("/", true);
//    }
//}