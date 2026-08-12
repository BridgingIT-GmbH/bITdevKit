// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;

/// <summary>
/// Creates immutable, non-sensitive context for one profiling session node.
/// </summary>
/// <example><code>var context = factory.Create(session, node);</code></example>
public sealed class ProfilingRuntimeContextFactory : IProfilingRuntimeContextFactory
{
    private readonly IProfilingRuntimeContextSource source;

    /// <summary>Creates a factory backed by the current process and runtime.</summary>
    /// <example><code>var factory = new ProfilingRuntimeContextFactory();</code></example>
    public ProfilingRuntimeContextFactory()
        : this(new SystemProfilingRuntimeContextSource()) { }

    /// <summary>Creates a factory backed by a caller-supplied runtime context source.</summary>
    /// <param name="source">The source that supplies runtime context values.</param>
    /// <example><code>var factory = new ProfilingRuntimeContextFactory(source);</code></example>
    public ProfilingRuntimeContextFactory(IProfilingRuntimeContextSource source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <inheritdoc />
    public ProfilingRuntimeContext Create(ProfilingSession session, ProfilingNode node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        var values = this.source.Capture();
        return new ProfilingRuntimeContext
        {
            SessionId = session.Identity.Id,
            NodeId = node.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeKey = node.Identity.Key,
            ApplicationName = values.ApplicationName,
            ApplicationVersion = values.ApplicationVersion,
            RuntimeDescription = values.RuntimeDescription,
            RuntimeVersion = values.RuntimeVersion,
            OperatingSystemDescription = values.OperatingSystemDescription,
            OperatingSystemArchitecture = values.OperatingSystemArchitecture,
            ProcessArchitecture = values.ProcessArchitecture,
            ServerGarbageCollection = values.ServerGarbageCollection,
            LogicalProcessorCount = values.LogicalProcessorCount,
            ProcessStartedUtc =
                node.Correlation?.ProcessStartedUtc
                ?? values.ProcessStartedUtc
                ?? DateTimeOffset.MinValue,
            DebuggerAttached = values.DebuggerAttached,
        };
    }
}

/// <summary>
/// Supplies the process and runtime values used to create a profiling runtime context.
/// </summary>
/// <example><code>var values = source.Capture();</code></example>
public interface IProfilingRuntimeContextSource
{
    /// <summary>Captures the currently available non-sensitive runtime context values.</summary>
    /// <returns>The captured runtime context values.</returns>
    /// <example><code>var values = source.Capture();</code></example>
    ProfilingRuntimeContextValues Capture();
}

/// <summary>Contains non-sensitive runtime context values captured for a profiling node.</summary>
/// <param name="ApplicationName">The entry application name.</param>
/// <param name="ApplicationVersion">The entry application version.</param>
/// <param name="RuntimeDescription">The runtime description.</param>
/// <param name="RuntimeVersion">The runtime version.</param>
/// <param name="OperatingSystemDescription">The operating system description.</param>
/// <param name="OperatingSystemArchitecture">The operating system architecture.</param>
/// <param name="ProcessArchitecture">The process architecture.</param>
/// <param name="ServerGarbageCollection">Whether server garbage collection is enabled.</param>
/// <param name="LogicalProcessorCount">The logical processor count.</param>
/// <param name="ProcessStartedUtc">The process start timestamp in UTC.</param>
/// <param name="DebuggerAttached">Whether a debugger is attached.</param>
/// <example><code>var values = source.Capture();</code></example>
public sealed record ProfilingRuntimeContextValues(
    string ApplicationName,
    string ApplicationVersion,
    string RuntimeDescription,
    string RuntimeVersion,
    string OperatingSystemDescription,
    string OperatingSystemArchitecture,
    string ProcessArchitecture,
    bool? ServerGarbageCollection,
    int? LogicalProcessorCount,
    DateTimeOffset? ProcessStartedUtc,
    bool DebuggerAttached
);

/// <summary>Captures profiling runtime context values from the current process.</summary>
/// <example><code>var values = new SystemProfilingRuntimeContextSource().Capture();</code></example>
public sealed class SystemProfilingRuntimeContextSource : IProfilingRuntimeContextSource
{
    /// <inheritdoc />
    public ProfilingRuntimeContextValues Capture()
    {
        var assembly = TryGet(Assembly.GetEntryAssembly);
        return new ProfilingRuntimeContextValues(
            assembly?.GetName().Name,
            TryGet(() =>
                assembly
                    ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion
            ),
            TryGet(() => RuntimeInformation.FrameworkDescription),
            TryGet(() => Environment.Version.ToString()),
            TryGet(() => RuntimeInformation.OSDescription),
            TryGet(() => RuntimeInformation.OSArchitecture.ToString()),
            TryGet(() => RuntimeInformation.ProcessArchitecture.ToString()),
            TryGet(() => (bool?)GCSettings.IsServerGC),
            TryGet(() => (int?)Environment.ProcessorCount),
            TryGet(GetProcessStartedUtc),
            TryGet(() => Debugger.IsAttached)
        );
    }

    private static DateTimeOffset? GetProcessStartedUtc()
    {
        using var process = Process.GetCurrentProcess();
        return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
    }

    private static T TryGet<T>(Func<T> accessor)
    {
        try
        {
            return accessor();
        }
        catch (Exception exception) when (IsUnsupported(exception))
        {
            return default;
        }
    }

    private static bool IsUnsupported(Exception exception) =>
        exception
            is InvalidOperationException
                or NotSupportedException
                or PlatformNotSupportedException
                or System.ComponentModel.Win32Exception;
}
