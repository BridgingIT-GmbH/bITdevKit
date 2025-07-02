// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;

/// <summary>
/// Represents event arguments containing information about a file event.
/// Used for event handling in the file monitoring system.
/// </summary>
[DebuggerDisplay("Path={Event.FilePath}, Location={Event.LocationName}, Type={Event.EventType.ToString()}")]
public class FileEventArgs(FileEvent @event)
{
    /// <summary>
    /// Gets the file event information associated with this event.
    /// </summary>
    public FileEvent Event { get; } = @event;
}