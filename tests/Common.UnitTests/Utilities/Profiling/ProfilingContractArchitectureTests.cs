// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

public class ProfilingContractArchitectureTests
{
    [Fact]
    public void StorageContracts_AsyncResults_UseAbstractionResultsAndBackendIndependentData()
    {
        // Arrange
        var contracts = new[] { typeof(IProfilingStorageProvider), typeof(IOperationProfilingStore), typeof(IOperationProfilingQueryService) };
        var methods = contracts.SelectMany(type => type.GetMethods()).Where(method => method.Name.EndsWith("Async"));

        // Act and assert
        foreach (var method in methods)
        {
            method.ReturnType.GetGenericTypeDefinition().ShouldBe(typeof(Task<>));
            var resultType = method.ReturnType.GenericTypeArguments[0];
            typeof(IResult).IsAssignableFrom(resultType).ShouldBeTrue();
            resultType.Assembly.ShouldBe(typeof(IResult).Assembly);
            method.GetParameters().ShouldNotContain(parameter => typeof(IQueryable).IsAssignableFrom(parameter.ParameterType));
        }

        contracts.ShouldAllBe(type => type.Assembly == typeof(IResult).Assembly);
        typeof(ProfilingWriterLease).Assembly.ShouldBe(typeof(IResult).Assembly);
        typeof(ProfilingQueryBoundary).Assembly.ShouldBe(typeof(IResult).Assembly);
    }

    [Fact]
    public void RuntimeContracts_AssemblyDependencies_ExcludeImplementationLayers()
    {
        // Arrange
        var assembly = typeof(IRuntimeProfilingStore).Assembly;
        var forbidden = new[] { "Common.Results", "Common.Utilities", "Infrastructure", "Presentation", "AspNetCore" };

        // Act
        var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();

        // Assert
        assembly.ShouldBe(typeof(IResult).Assembly);
        references.ShouldNotContain(reference => forbidden.Any(reference.Contains));
        typeof(RuntimeProfilingSession).Assembly.ShouldBe(assembly);
        typeof(RuntimeProfilingArchive).Assembly.ShouldBe(assembly);
        typeof(RuntimeProfilingEvaluationResult).Assembly.ShouldBe(assembly);
        typeof(RuntimeProfilingNodeSessionData).Assembly.ShouldBe(assembly);
    }

    [Fact]
    public void IdentityFactories_RepeatedCalls_CreateDistinctValidatedIdentities()
    {
        // Arrange
        var identities = Enumerable.Range(0, 100).Select(_ => ProfilingIdentityFactory.CreateNode()).ToArray();

        // Act
        var uniqueIds = identities.Select(identity => identity.Id).Distinct().Count();

        // Assert
        uniqueIds.ShouldBe(identities.Length);
        identities.ShouldAllBe(identity => identity.Id != Guid.Empty && identity.Key.Length == 8);
    }
}
