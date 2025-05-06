// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

public class SystemInfo
{
    public Dictionary<string, object> Request { get; set; }

    public Dictionary<string, string> Runtime { get; set; }

    public Dictionary<string, string> Memory { get; set; }

    public Dictionary<string, string> Configuration { get; set; }

    public Dictionary<string, string> CustomMetadata { get; set; }

    public string Uptime { get; set; }
}