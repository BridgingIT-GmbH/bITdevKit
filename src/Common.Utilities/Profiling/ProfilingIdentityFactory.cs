// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Creates profiling identities without introducing implementation dependencies into records.</summary>
/// <example><code>var identity = ProfilingIdentityFactory.CreateNode();</code></example>
public static class ProfilingIdentityFactory
{
    /// <summary>Creates a Runtime session identity.</summary>
    /// <example><code>var identity = ProfilingIdentityFactory.CreateRuntimeSession();</code></example>
    public static RuntimeProfilingSessionIdentity CreateRuntimeSession() => new(Guid.NewGuid(), KeyGenerator.CreateLowercase(8));

    /// <summary>Creates a process identity.</summary>
    /// <example><code>var identity = ProfilingIdentityFactory.CreateNode();</code></example>
    public static ProfilingNodeIdentity CreateNode() => new(Guid.NewGuid(), KeyGenerator.CreateLowercase(8));

    /// <summary>Creates a Runtime snapshot identity.</summary>
    /// <example><code>var identity = ProfilingIdentityFactory.CreateRuntimeSnapshot();</code></example>
    public static RuntimeProfilingSnapshotIdentity CreateRuntimeSnapshot() => new(Guid.NewGuid(), KeyGenerator.CreateLowercase(8));
}
