// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests;

using BridgingIT.DevKit.Domain.Repositories;

public class EntityBulkInserterContractTests
{
    [Fact]
    public void Contract_WhenInspected_IsOwnedByDomainWithoutInfrastructureDependencies()
    {
        // Arrange
        var contractType = typeof(IEntityBulkInserter<PersonStub>);

        // Act
        var referencedAssemblies = contractType.Assembly.GetReferencedAssemblies();

        // Assert
        contractType.Namespace.ShouldBe("BridgingIT.DevKit.Domain.Repositories");
        contractType.Assembly.GetName().Name.ShouldBe("BridgingIT.DevKit.Domain");
        referencedAssemblies.ShouldNotContain(reference =>
            reference.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
        );
        referencedAssemblies.ShouldNotContain(reference =>
            string.Equals(reference.Name, "Microsoft.Data.SqlClient", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void InsertAsync_WhenInspected_HasCountResultAndCancellationSignature()
    {
        // Arrange
        var contractType = typeof(IEntityBulkInserter<PersonStub>);

        // Act
        var method = contractType.GetMethod(nameof(IEntityBulkInserter<PersonStub>.InsertAsync));
        var parameters = method?.GetParameters();

        // Assert
        method.ShouldNotBeNull();
        method.ReturnType.ShouldBe(typeof(Task<Result<long>>));
        parameters.ShouldNotBeNull();
        parameters.Length.ShouldBe(2);
        parameters[0].ParameterType.ShouldBe(typeof(IEnumerable<PersonStub>));
        parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
        parameters[1].HasDefaultValue.ShouldBeTrue();
    }
}
