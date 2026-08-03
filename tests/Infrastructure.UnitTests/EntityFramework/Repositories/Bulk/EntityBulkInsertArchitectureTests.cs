// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories.Bulk;

public class EntityBulkInsertArchitectureTests
{
    [Fact]
    public void TerminalBulkInserter_HasNoRepositoryOrLegacyBehaviorDependencies()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "Infrastructure.EntityFramework",
            "Repositories",
            "Bulk",
            "EntityFrameworkEntityBulkInserter.cs"));

        source.ShouldNotContain("IGenericRepository");
        source.ShouldNotContain("CompatibilityAnalyzer");
        source.ShouldNotContain("BehaviorPipeline");
        source.ShouldNotContain("ExecuteRepositoryFallbackAsync");
    }

    [Fact]
    public void BulkInserterContractAndStandardBehaviors_AreOwnedByDomain()
    {
        var repositoryRoot = GetRepositoryRoot();
        var domainDirectory = Path.Combine(repositoryRoot, "src", "Domain", "Repositories");

        File.Exists(Path.Combine(domainDirectory, "IEntityBulkInserter.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(domainDirectory, "EntityBulkInserterBuilderContext.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(domainDirectory, "Behaviors", "BulkInserter", "EntityBulkInserterConcurrencyBehavior.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(domainDirectory, "Behaviors", "BulkInserter", "EntityBulkInserterDomainEventBehavior.cs")).ShouldBeTrue();
    }

    [Fact]
    public void InfrastructureBulkBehaviors_ContainOnlyTheEntityFrameworkOutboxDecorator()
    {
        var behaviorDirectory = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "Infrastructure.EntityFramework",
            "Repositories",
            "Bulk",
            "Behaviors");

        Directory.EnumerateFiles(behaviorDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ShouldBe(["EntityBulkInserterOutboxDomainEventBehavior.cs"]);
        var compatibilityDirectory = Path.Combine(Directory.GetParent(behaviorDirectory)!.FullName, "Compatibility");
        (!Directory.Exists(compatibilityDirectory) || !Directory.EnumerateFiles(
                compatibilityDirectory,
                "*.cs",
                SearchOption.TopDirectoryOnly).Any())
            .ShouldBeTrue();
    }

    [Fact]
    public void SharedBulkInsertSources_DoNotReferenceProviderNativeTypes()
    {
        // Arrange
        var bulkDirectory = Path.Combine(GetRepositoryRoot(), "src", "Infrastructure.EntityFramework", "Repositories", "Bulk");
        var prohibitedReferences = new[]
        {
            "Microsoft.Data.SqlClient",
            "SqlBulkCopy",
            "Npgsql",
            "Microsoft.Data.Sqlite",
            "System.Data.SQLite"
        };

        // Act
        var sourceFiles = Directory.EnumerateFiles(bulkDirectory, "*.cs", SearchOption.TopDirectoryOnly);

        // Assert
        foreach (var sourceFile in sourceFiles)
        {
            var source = File.ReadAllText(sourceFile);

            foreach (var prohibitedReference in prohibitedReferences)
            {
                source.ShouldNotContain(prohibitedReference, customMessage: sourceFile);
            }
        }
    }

    [Fact]
    public void EntityFrameworkProviderProjects_ReferenceSharedProject_AndSharedProjectReferencesNoProvider()
    {
        // Arrange
        var repositoryRoot = GetRepositoryRoot();
        var sharedProject = ReadProject(repositoryRoot, "Infrastructure.EntityFramework");
        var providerProjects = new[]
        {
            ReadProject(repositoryRoot, "Infrastructure.EntityFramework.SqlServer"),
            ReadProject(repositoryRoot, "Infrastructure.EntityFramework.Postgres"),
            ReadProject(repositoryRoot, "Infrastructure.EntityFramework.Sqlite")
        };

        // Assert
        foreach (var providerProject in providerProjects)
        {
            providerProject.ShouldContain("Infrastructure.EntityFramework\\Infrastructure.EntityFramework.csproj");
        }

        sharedProject.ShouldNotContain("Infrastructure.EntityFramework.SqlServer.csproj");
        sharedProject.ShouldNotContain("Infrastructure.EntityFramework.Postgres.csproj");
        sharedProject.ShouldNotContain("Infrastructure.EntityFramework.Sqlite.csproj");
    }

    private static string ReadProject(string repositoryRoot, string projectName)
    {
        var projectPath = Path.Combine(repositoryRoot, "src", projectName, $"{projectName}.csproj");

        return File.ReadAllText(projectPath);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bITdevKit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the bITdevKit repository root.");
    }
}
