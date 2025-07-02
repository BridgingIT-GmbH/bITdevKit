// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

public class SystemModule
{
    public string Name { get; set; }
    public bool Enabled { get; set; }
    public bool IsRegistered { get; set; }
    public int Priority { get; set; }
}