    // MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using BridgingIT.DevKit.Application.Messaging;

public class EchoMessage : MessageBase
{
    public EchoMessage() // TODO: needed?
    {
    }

    public EchoMessage(string text)
    {
        this.Text = text;
    }

    public string Text { get; set; }
}