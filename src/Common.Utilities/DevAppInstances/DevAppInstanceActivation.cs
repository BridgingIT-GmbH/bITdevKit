// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Holds the pending default activation of a DevKit host's development instance. The first configuration consumer
/// runs it, for example logging, module registration, a DbContext registration or the build.
/// </summary>
/// <example>
/// <code>
/// var activation = services.LastOrDefault(d => d.ServiceType == typeof(DevAppInstanceActivation))?.ImplementationInstance as DevAppInstanceActivation;
/// activation?.Run();
/// </code>
/// </example>
public sealed class DevAppInstanceActivation
{
    private Action activate;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevAppInstanceActivation"/> class.
    /// </summary>
    /// <param name="activate">The activation to run once.</param>
    public DevAppInstanceActivation(Action activate)
    {
        ArgumentNullException.ThrowIfNull(activate);

        this.activate = activate;
    }

    /// <summary>
    /// Gets a value indicating whether the activation ran or was cancelled.
    /// </summary>
    public bool IsCompleted => this.activate is null;

    /// <summary>
    /// Runs the activation once. Later calls do nothing.
    /// </summary>
    /// <example>
    /// <code>
    /// activation.Run();
    /// </code>
    /// </example>
    public void Run() => Interlocked.Exchange(ref this.activate, null)?.Invoke();

    /// <summary>
    /// Drops the activation without running it, because an explicit registration replaces it.
    /// </summary>
    /// <example>
    /// <code>
    /// activation.Cancel();
    /// </code>
    /// </example>
    public void Cancel() => Interlocked.Exchange(ref this.activate, null);
}
