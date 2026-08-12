// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Carries the ambient measured segment through asynchronous application execution.
/// </summary>
/// <remarks>
/// Application code normally receives this behavior through
/// <see cref="IProfilingMeasurementService"/> and does not manipulate the context directly.
/// </remarks>
/// <example><code>var segmentKey = context.Current?.SessionKey;</code></example>
public sealed class ProfilingSegmentContext
{
    private readonly AsyncLocal<Frame> current = new();

    /// <summary>Gets the active ambient segment, when one exists.</summary>
    /// <example><code>var segment = context.Current;</code></example>
    public ProfilingAmbientSegment Current
    {
        get
        {
            var frame = this.current.Value;
            while (frame is not null)
            {
                if (frame.IsActive && frame.Segment is not null)
                {
                    return frame.Segment;
                }

                frame = frame.Parent;
            }

            return null;
        }
    }

    internal Frame PushPending()
    {
        var frame = new Frame(this.current.Value);
        this.current.Value = frame;
        return frame;
    }

    internal sealed class Frame
    {
        private int active = 1;
        private ProfilingAmbientSegment segment;

        public Frame(Frame parent)
        {
            this.Parent = parent;
        }

        public Frame Parent { get; }

        public bool IsActive => Volatile.Read(ref this.active) != 0;

        public ProfilingAmbientSegment Segment => Volatile.Read(ref this.segment);

        public void Activate(ProfilingAmbientSegment value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Volatile.Write(ref this.segment, value);
        }

        public void Deactivate()
        {
            Interlocked.Exchange(ref this.active, 0);
        }
    }
}

/// <summary>Identifies the measured segment currently associated with application execution.</summary>
/// <example><code>var sessionKey = segment.SessionKey;</code></example>
public sealed class ProfilingAmbientSegment
{
    internal ProfilingAmbientSegment(
        Guid segmentId,
        Guid sessionId,
        Guid nodeId,
        string sessionKey,
        string nodeKey
    )
    {
        this.SegmentId = segmentId;
        this.SessionId = sessionId;
        this.NodeId = nodeId;
        this.SessionKey = sessionKey;
        this.NodeKey = nodeKey;
    }

    internal Guid SegmentId { get; }

    internal Guid SessionId { get; }

    internal Guid NodeId { get; }

    /// <summary>Gets the public session key.</summary>
    /// <example><code>var sessionKey = segment.SessionKey;</code></example>
    public string SessionKey { get; }

    /// <summary>Gets the public node key.</summary>
    /// <example><code>var nodeKey = segment.NodeKey;</code></example>
    public string NodeKey { get; }
}
