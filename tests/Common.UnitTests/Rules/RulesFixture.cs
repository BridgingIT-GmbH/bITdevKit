// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Rules;

public class RulesFixture : IDisposable
{
    public RulesFixture()
    {
        // Reset settings before each test class
        this.ResetSettings();
    }

    public void Dispose()
    {
        // Reset settings after each test class
        this.ResetSettings();
    }

    private void ResetSettings()
    {
        Rule.Setup(builder =>
        {
            /* Default settings */
        });
    }
}