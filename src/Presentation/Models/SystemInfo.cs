// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

/// <summary>
/// Represents system info.
/// </summary>
public class SystemInfo
{
    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    public Dictionary<string, object> Request { get; set; }

    /// <summary>
    /// Gets or sets the runtime.
    /// </summary>
    public Dictionary<string, string> Runtime { get; set; }

    /// <summary>
    /// Gets or sets the memory.
    /// </summary>
    public Dictionary<string, string> Memory { get; set; }

    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    public Dictionary<string, string> Configuration { get; set; }

    /// <summary>
    /// Gets or sets the custom metadata.
    /// </summary>
    public Dictionary<string, string> CustomMetadata { get; set; }

    /// <summary>
    /// Gets or sets the uptime.
    /// </summary>
    public string Uptime { get; set; }
}
