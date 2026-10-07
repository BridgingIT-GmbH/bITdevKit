// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Configures the overarching Profiling feature and its independent capabilities.</summary>
/// <example><code>services.AddProfiling(o => o.Enabled()).WithRuntimeProfiling();</code></example>
public sealed class ProfilingOptions
{
    /// <summary>Gets or sets whether the master feature is enabled.</summary>
    /// <example><code>var value = options.Enabled;</code></example>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the bounded local process display name.</summary>
    /// <example><code>var value = options.NodeDisplayName;</code></example>
    public string NodeDisplayName { get; set; }

    /// <summary>Gets or sets the bounded application version override.</summary>
    /// <example><code>var value = options.ApplicationVersion;</code></example>
    public string ApplicationVersion { get; set; }

    /// <summary>Gets or sets independent runtime capture settings.</summary>
    /// <example><code>var value = options.Runtime;</code></example>
    public RuntimeProfilingOptions Runtime { get; set; } = new();

    /// <summary>Gets or sets independent operation recording settings.</summary>
    /// <example><code>var value = options.Operations;</code></example>
    public OperationProfilingOptions Operations { get; set; } = new();

    /// <summary>Gets or sets http adapter enablement, configured by the web package.</summary>
    /// <example><code>var value = options.Requests;</code></example>
    public ProfilingCapabilityOptions Requests { get; set; } = new();

    /// <summary>Gets or sets bounded retained-history query settings.</summary>
    /// <example><code>var value = options.Queries;</code></example>
    public ProfilingQueryOptions Queries { get; set; } = new();

    /// <summary>Gets the effective Runtime capture state.</summary>
    /// <example><code>if (options.RuntimeEnabled) StartRuntime();</code></example>
    public bool RuntimeEnabled => this.Enabled && this.Runtime.Enabled;

    /// <summary>Gets the effective operation capture state.</summary>
    /// <example><code>if (options.OperationEnabled) StartRecorder();</code></example>
    public bool OperationEnabled => this.Enabled && this.Operations.Enabled;

    /// <summary>Validates the final composed configuration.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (this.Runtime is null || this.Operations is null || this.Requests is null || this.Queries is null)
        {
            throw new InvalidOperationException("Profiling capability options cannot be null.");
        }

        if (!this.Enabled)
        {
            return;
        }

        if (this.Requests.Enabled && !this.Operations.Enabled)
        {
            throw new InvalidOperationException("Request Profiling requires explicitly enabled Operation Profiling.");
        }

        if (this.NodeDisplayName?.Length > 128 || this.ApplicationVersion?.Length > 128)
        {
            throw new InvalidOperationException("Profiling node metadata cannot exceed 128 characters.");
        }

        this.Runtime.Validate();
        this.Operations.Validate();
        this.Queries.Validate();
    }
}

/// <summary>Configures master enablement and shared process metadata.</summary>
/// <example><code>options.Enabled().Node("worker-1");</code></example>
public sealed class ProfilingOptionsBuilder(ProfilingOptions target)
{
    /// <summary>Sets the master capture flag.</summary>
    /// <example><code>options.Enabled(environment.IsDevelopment());</code></example>
    public ProfilingOptionsBuilder Enabled(bool value = true)
    {
        target.Enabled = value;
        return this;
    }

    /// <summary>Sets bounded cached local process metadata.</summary>
    /// <example><code>options.Node("worker-1", "2.0.0");</code></example>
    public ProfilingOptionsBuilder Node(string displayName, string applicationVersion = null)
    {
        target.NodeDisplayName = displayName;
        target.ApplicationVersion = applicationVersion;
        return this;
    }
}

/// <summary>Records whether an adapter capability is configured and enabled.</summary>
/// <example><code>var enabled = options.Requests.Enabled;</code></example>
public class ProfilingCapabilityOptions
{
    /// <summary>Gets or sets explicit capability enablement.</summary>
    /// <example><code>options.Enabled = true;</code></example>
    public bool Enabled { get; set; }
}
