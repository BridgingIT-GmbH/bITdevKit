// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using Microsoft.Extensions.DependencyInjection;

public class ValueCodecsBuilderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void WithCodec_ExplicitDefault_ResolvesSelectedWriterAndRetainsBothReaders(int version)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new SystemTextJsonValueCodec<Report>());
        services.AddValueCodecs(codecs => codecs
            .WithCodec<Report, ReportCodecV1>(isDefault: version == 1)
            .WithCodec<Report, ReportCodecV2>(isDefault: version == 2));
        using var provider = services.BuildServiceProvider();

        // Act
        var sut = provider.GetRequiredService<IValueCodec<Report>>();

        // Assert
        sut.FormatVersion.ShouldBe(version);
        var registry = provider.GetRequiredService<ValueCodecRegistry>();
        registry.TryGet<Report>(new("report", 1), out _).ShouldBeTrue();
        registry.TryGet<Report>(new("report", 2), out _).ShouldBeTrue();
        registry.TryGet<Report>(new("report", version), out var reader).ShouldBeTrue();
        sut.ShouldBeSameAs(reader);
        provider.GetServices<IValueCodec<Report>>().ShouldHaveSingleItem().ShouldBeSameAs(sut);
    }

    [Fact]
    public void WithCodec_LaterDefaultAcrossCalls_ReplacesWriterAndRetainsPreviousReader()
    {
        // Arrange
        var services = new ServiceCollection();
        var dependency = new SystemTextJsonValueCodec<Report>();
        var original = new ReportCodecV1(dependency);
        services.AddSingleton(dependency);
        services.AddValueCodecs(codecs => codecs.WithCodec<Report>(original, isDefault: true));
        services.AddValueCodecs(codecs => codecs.WithCodec<Report, ReportCodecV2>(isDefault: true));
        using var provider = services.BuildServiceProvider();

        // Act
        var sut = provider.GetRequiredService<IValueCodec<Report>>();

        // Assert
        sut.ShouldBeSameAs(provider.GetRequiredService<ReportCodecV2>());
        provider.GetServices<IValueCodec<Report>>().ShouldHaveSingleItem().ShouldBeSameAs(sut);
        var registry = provider.GetRequiredService<ValueCodecRegistry>();
        registry.TryGet<Report>(new("report", 1), out var oldReader).ShouldBeTrue();
        oldReader.ShouldBeSameAs(original);
        registry.TryGet<Report>(new("report", 2), out var currentReader).ShouldBeTrue();
        currentReader.ShouldBeSameAs(sut);
    }

    [Fact]
    public void WithCodec_TypedVersions_ResolvesDependenciesAndPreservesExistingRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var dependency = new SystemTextJsonValueCodec<Report>();
        var original = new ReportCodecV1(dependency);
        services.AddSingleton(dependency);
        services.AddSingleton(original);
        services.AddValueCodecs(codecs => codecs
            .WithCodec<Report, ReportCodecV1>()
            .WithCodec<Report, ReportCodecV2>());
        using var provider = services.BuildServiceProvider();

        // Act
        var sut = provider.GetRequiredService<ValueCodecRegistry>();

        // Assert
        sut.TryGet<Report>(new("report", 1), out var first).ShouldBeTrue();
        sut.TryGet<Report>(new("report", 2), out var second).ShouldBeTrue();
        first.ShouldBeSameAs(original);
        second.ShouldBeSameAs(provider.GetRequiredService<ReportCodecV2>());
        ((ReportCodecV2)second).Inner.ShouldBeSameAs(dependency);
        sut.TryGet<Report>(new("report", 3), out _).ShouldBeFalse();
        sut.TryGet<string>(new("report", 1), out _).ShouldBeFalse();
        provider.GetService<IValueCodec<Report>>().ShouldBeNull();
    }

    [Fact]
    public void WithCodec_ConfiguredInstancesAcrossCalls_UsesSharedRegistryWithoutReplacingSerializer()
    {
        // Arrange
        var services = new ServiceCollection();
        var serializer = new SystemTextJsonSerializer();
        var first = new SystemTextJsonValueCodec<Report>();
        var second = new SystemTextJsonValueCodec<string>();
        services.AddSingleton<ISerializer>(serializer);
        services.AddValueCodecs(codecs => codecs.WithCodec(first, isDefault: true));
        services.AddValueCodecs(codecs => codecs.WithCodec(second));
        using var provider = services.BuildServiceProvider();

        // Act
        var sut = provider.GetRequiredService<ValueCodecRegistry>();

        // Assert
        sut.TryGet<Report>(new("json", 1), out var reportReader).ShouldBeTrue();
        sut.TryGet<string>(new("json", 1), out var stringReader).ShouldBeTrue();
        reportReader.ShouldBeSameAs(first);
        stringReader.ShouldBeSameAs(second);
        provider.GetRequiredService<IValueCodec<Report>>().ShouldBeSameAs(first);
        provider.GetService<IValueCodec<string>>().ShouldBeNull();
        provider.GetServices<ValueCodecRegistry>().ShouldHaveSingleItem().ShouldBeSameAs(sut);
        provider.GetRequiredService<ISerializer>().ShouldBeSameAs(serializer);
    }

    [Fact]
    public void WithCodec_DuplicateContractFormat_RejectsRegistryResolution()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddValueCodecs(codecs => codecs
            .WithCodec(new SystemTextJsonValueCodec<Report>())
            .WithCodec<Report, SystemTextJsonValueCodec<Report>>());
        using var provider = services.BuildServiceProvider();

        // Act / Assert
        Should.Throw<ArgumentException>(() => provider.GetRequiredService<ValueCodecRegistry>());
    }

    [Fact]
    public void AddValueCodecs_NullArguments_RejectsAtRegistrationBoundary()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => ValueCodecServiceCollectionExtensions.AddValueCodecs(null, _ => { }));
        Should.Throw<ArgumentNullException>(() => services.AddValueCodecs(null));
        Should.Throw<ArgumentNullException>(() => services.AddValueCodecs(codecs => codecs.WithCodec<Report>(null)));
    }

    public sealed record Report(string Title);

    public class ReportCodecV1(SystemTextJsonValueCodec<Report> inner) : IValueCodec<Report>
    {
        public SystemTextJsonValueCodec<Report> Inner { get; } = inner;

        public string FormatId => "report";

        public virtual int FormatVersion => 1;

        public ValueCodecCapabilities Capabilities => this.Inner.Capabilities;

        public ValueTask<Result> EncodeAsync(Report value, Stream destination, CancellationToken cancellationToken = default) =>
            this.Inner.EncodeAsync(value, destination, cancellationToken);

        public ValueTask<Result<Report>> DecodeAsync(Stream source, CancellationToken cancellationToken = default) =>
            this.Inner.DecodeAsync(source, cancellationToken);
    }

    public sealed class ReportCodecV2(SystemTextJsonValueCodec<Report> inner) : ReportCodecV1(inner)
    {
        public override int FormatVersion => 2;
    }
}