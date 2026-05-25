// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

public class OrchestrationDocumentationTests
{
    [Fact]
    public void FeaturesDocument_UsesCurrentProjectAndNamespaceNames()
    {
        var content = ReadFeaturesDocument();

        content.ShouldContain("Application.Orchestrations");
        content.ShouldContain("Infrastructure.EntityFramework.Orchestrations");
        content.ShouldContain("Presentation.Web.Orchestrations");
        content.ShouldContain("using BridgingIT.DevKit.Application.Orchestrations;");

        content.ShouldNotContain("`Application.Orchestration`");
        content.ShouldNotContain("`Infrastructure.EntityFramework/Orchestration`");
        content.ShouldNotContain("`Presentation.Web.Orchestration`");
        content.ShouldNotContain("using BridgingIT.DevKit.Application.Orchestration;");
    }

    [Fact]
    public void FeaturesDocument_DescribesCurrentTimerRecoveryModel()
    {
        var content = ReadFeaturesDocument();

        content.ShouldContain("OrchestrationExecutionSettings");
        content.ShouldContain("StartupDelay");
        content.ShouldContain("BackgroundSweepInterval");
        content.ShouldContain("BackgroundSweepBatchSize");
        content.ShouldContain("background recovery worker");
        content.ShouldContain("wait-plan metadata");
        content.ShouldContain("poll-based and lease-protected");
    }

    [Fact]
    public void FeaturesDocument_DoesNotClaimBuiltInHelpersAreOnlyDesignCandidates()
    {
        var content = ReadFeaturesDocument();

        content.ShouldContain("The built-in helpers above are part of the current public authoring surface.");
        content.ShouldNotContain("Those are design candidates, but they are not part of the current public authoring surface yet");
    }

    private static string ReadFeaturesDocument()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "docs", "features-orchestrations.md");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("The orchestration features document could not be located from the current test base directory.");
    }
}
