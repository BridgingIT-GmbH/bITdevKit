// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.DevAppInstances;

using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;

public class DevAppInstanceDatabaseAdapterLocatorTests
{
    [Fact]
    public void Locate_ProviderAssemblies_ReturnsAdaptersInProviderOrder()
    {
        // Arrange
        var assemblies = DevAppInstanceDatabaseAdapterLocator.WellKnownAssemblyNames.Select(n => Assembly.Load(new AssemblyName(n)));

        // Act
        var result = DevAppInstanceDatabaseAdapterLocator.Locate(assemblies);

        // Assert
        result.ShouldBe(
            [SqlServerDevAppInstanceAdapter.Instance, PostgresDevAppInstanceAdapter.Instance, SqliteDevAppInstanceAdapter.Instance]);
    }

    [Fact]
    public void Locate_ApplicationWithProviderPackages_FindsEveryProvider()
    {
        // Act
        var result = DevAppInstanceDatabaseAdapterLocator.Locate();

        // Assert
        result.Select(a => a.Kind).Take(3).ShouldBe(
            [SqlServerDevAppInstanceAdapter.KindName, PostgresDevAppInstanceAdapter.KindName, SqliteDevAppInstanceAdapter.KindName]);
        result.Select(a => a.Kind).ShouldBeUnique();
    }

    [Fact]
    public void Locate_AssemblyWithoutAttribute_ReturnsNothing()
    {
        // Act
        var result = DevAppInstanceDatabaseAdapterLocator.Locate([typeof(string).Assembly]);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Locate_AdapterThatThrowsOnCreate_SkipsAdapterAndKeepsOthers()
    {
        // Arrange
        var assembly = new AttributedAssembly(typeof(ThrowingAdapter), typeof(SqliteDevAppInstanceAdapter));

        // Act
        var result = DevAppInstanceDatabaseAdapterLocator.Locate([assembly]);

        // Assert
        result.ShouldBe([SqliteDevAppInstanceAdapter.Instance]);
    }

    private sealed class AttributedAssembly(params Type[] adapterTypes) : Assembly
    {
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) =>
            adapterTypes.Select(t => new DevAppInstanceDatabaseAdapterAttribute(t)).ToArray();

        public override AssemblyName GetName() => new("AttributedAssembly");
    }

    private sealed class ThrowingAdapter : DevAppInstanceDatabaseAdapterBase
    {
        public static ThrowingAdapter Instance => throw new InvalidOperationException("Adapter failed to initialize.");

        public override string Kind => "throwing";

        protected override int MaxIdentifierLength => 63;

        public override Task<DevAppInstanceResetOutcome> ResetAsync(
            IReadOnlyList<DevAppInstanceInventoryResource> targets,
            DevAppInstanceContext context,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        protected override bool TryParse(string value, out string server, out string database) => throw new NotSupportedException();

        protected override string Rewrite(string value, string database) => throw new NotSupportedException();
    }
}
