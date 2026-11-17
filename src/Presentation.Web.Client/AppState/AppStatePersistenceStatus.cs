// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>Reports state persistence readiness without implying business-operation success.</summary>
/// <example>
/// <code><![CDATA[
/// await state.Load();
/// if (state.PersistenceStatus == AppStatePersistenceStatus.Ready)
/// {
///     await state.Save();
/// }
/// ]]></code>
/// </example>
public enum AppStatePersistenceStatus
{
    /// <summary>No persistence is configured.</summary>
    NotConfigured,

    /// <summary>The owning state has not established its storage partition.</summary>
    NotLoaded,

    /// <summary>A shared initial read is in progress.</summary>
    Loading,

    /// <summary>The partition was read or saved successfully.</summary>
    Ready,

    /// <summary>A captured value is being written.</summary>
    Saving,

    /// <summary>Browser access or a context reload is required.</summary>
    Deferred,

    /// <summary>The most recent storage operation failed.</summary>
    Failed,

    /// <summary>The live owner has been disposed.</summary>
    Disposed
}
