// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

public abstract partial class AppState<TState>
{
    private bool hasHandoff;
    private long lastEditRevision;

    /// <summary>Gets the stable logical identity reserved by a lifecycle owner, independently of storage enablement.</summary>
    /// <remarks>Hosts assign distinct instance keys to independent views. This identity excludes runtime IDs and user context.</remarks>
    /// <example>
    /// <code><![CDATA[
    /// var identity = state.HandoffIdentity;
    /// Debug.Assert(!string.IsNullOrEmpty(identity));
    /// ]]></code>
    /// </example>
    public string HandoffIdentity => this.BuildHandoffKey(string.Empty, string.Empty);

    /// <summary>Gets the current context-qualified transfer key without copying the model.</summary>
    /// <remarks>The host establishes context before reading this key. Capture the destination version before deserialization.</remarks>
    /// <example>
    /// <code><![CDATA[
    /// var key = state.HandoffKey;
    /// Debug.Assert(key == state.CaptureHandoff().Key);
    /// ]]></code>
    /// </example>
    public string HandoffKey
    {
        get
        {
            lock (this.sync)
            {
                ObjectDisposedException.ThrowIf(this.disposed, this);
                if (!this.IsOwnerContextCurrent())
                {
                    throw new InvalidOperationException("The AppState owner context is not current.");
                }

                return this.GetHandoffKey();
            }
        }
    }

    /// <summary>Captures approved model values under the current logical instance and host context.</summary>
    /// <returns>An independent model and its context-qualified key.</returns>
    /// <remarks>Call only for data the host permits to cross its selected render-mode boundary.</remarks>
    /// <example>
    /// <code><![CDATA[
    /// var handoff = state.CaptureHandoff();
    /// Console.WriteLine(handoff.Key);
    /// ]]></code>
    /// </example>
    public AppStateHandoff<TState> CaptureHandoff()
    {
        lock (this.sync)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            var version = this.Version;
            var key = this.GetHandoffKey();
            var model = this.CloneState(this.GetCurrentState());
            if (!this.IsCurrent(version))
            {
                throw new InvalidOperationException("The AppState context changed during handoff capture.");
            }

            return new(key, model);
        }
    }

    /// <summary>Initializes an unedited state from a matching lifecycle snapshot without history or automatic saving.</summary>
    /// <param name="handoff">The approved model and logical origin key.</param>
    /// <param name="expectedVersion">The destination version captured before deserialization or asynchronous work.</param>
    /// <returns>True when accepted; false for an obsolete version, mismatched key, prior edit or prior handoff.</returns>
    /// <remarks>Later preference loads establish storage readiness without replacing these values. Reset releases that precedence.</remarks>
    /// <example>
    /// <code><![CDATA[
    /// var version = destination.Version;
    /// var handoff = source.CaptureHandoff();
    /// var restored = destination.TryRestoreHandoff(handoff, version);
    /// Debug.Assert(restored && !destination.CanUndo);
    /// ]]></code>
    /// </example>
    public bool TryRestoreHandoff(AppStateHandoff<TState> handoff, AppStateVersion expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        ArgumentNullException.ThrowIfNull(handoff.Model);
        if (!this.IsCurrent(expectedVersion)) { return false; }

        return this.Commit(handoff.Model, "Handoff", expected: expectedVersion, handoffKey: handoff.Key);
    }

    private string GetHandoffKey() => this.BuildHandoffKey(
        this.context.PrincipalKey == null ? "anonymous" : "user:" + this.context.PrincipalKey,
        this.context.WorkspaceKey == null ? "none" : "workspace:" + this.context.WorkspaceKey);

    private string BuildHandoffKey(string principal, string workspace) => "bdk:appstate:handoff:1:" +
        AppStateStorageKey.Create(this.options.ApplicationId, this.options.ModuleId,
            this.options.StateName ?? this.StateId, this.options.InstanceKey, principal, workspace,
            this.options.SchemaVersion)[AppStateStorageKey.Prefix.Length..];
}
