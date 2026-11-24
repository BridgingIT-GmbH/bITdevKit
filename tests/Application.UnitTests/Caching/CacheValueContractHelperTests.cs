// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises JSON option snapshots and the existing codec profile replacement rules.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheValueContractHelperTests</code></example>
public sealed class CacheValueContractHelperTests
{
    /// <summary>Captures property naming and converters before callers mutate options or resolve a codec.</summary>
    /// <returns>A task that completes after encoding and decoding the registered JSON representation.</returns>
    [Fact]
    public async Task WithJsonOptions_CallerChangesBeforeResolution_UsesCapturedConventions()
    {
        // Arrange
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter());
        var sut = new CacheValueContractBuilder<Card>().WithJsonOptions(options);
        options.PropertyNamingPolicy = null;
        options.Converters.Clear();
        using var services = new ServiceCollection().BuildServiceProvider();
        var codec = sut.Build("cards").GetCodec("json", services);
        var expected = new Card("West", CardStatus.Ready);
        using var payload = new MemoryStream();

        // Act
        var encoded = await codec.EncodeAsync(expected, payload);
        payload.Position = 0;
        using var json = await JsonDocument.ParseAsync(payload);
        payload.Position = 0;
        var decoded = await codec.DecodeAsync(payload);

        // Assert
        encoded.IsSuccess.ShouldBeTrue();
        json.RootElement.GetProperty("displayName").GetString().ShouldBe("West");
        json.RootElement.GetProperty("status").GetString().ShouldBe("Ready");
        decoded.IsSuccess.ShouldBeTrue();
        decoded.Value.ShouldBe(expected);
        options.IsReadOnly.ShouldBeFalse();
        codec.FormatId.ShouldBe("json");
        codec.FormatVersion.ShouldBe(1);
    }

    /// <summary>Replacing JSON options leaves custom profiles, their validators, and immutable sharing intact.</summary>
    [Fact]
    public void WithJsonOptions_ExistingProfilesAndAccounting_PreservesOtherContractSettings()
    {
        // Arrange
        var custom = Substitute.For<IValueCodec<Card>>();
        var customValidator = Substitute.For<IValueCodecValidator<Card>>();
        var previousJson = Substitute.For<IValueCodec<Card>>();
        var previousJsonValidator = Substitute.For<IValueCodecValidator<Card>>();
        var sut = new CacheValueContractBuilder<Card>()
            .WithCodec("compact", _ => custom, _ => customValidator)
            .WithJsonCodec(_ => previousJson, _ => previousJsonValidator)
            .WithImmutableSharing(_ => 128L);
        using var services = new ServiceCollection().BuildServiceProvider();

        // Act
        var contract = sut.WithJsonOptions(JsonSerializerOptions.Web).Build("cards");

        // Assert
        contract.GetCodec("compact", services).ShouldBeSameAs(custom);
        contract.GetValidator("compact", services).ShouldBeSameAs(customValidator);
        contract.GetCodec("json", services).ShouldBeOfType<SystemTextJsonValueCodec<Card>>();
        contract.GetValidator("json", services).ShouldBeNull();
        contract.SupportsDecodedSharing.ShouldBeTrue();
        contract.EstimateDecodedBytes(new("West", CardStatus.Ready)).ShouldBe(128L);
    }

    /// <summary>A later explicit JSON factory still replaces both the codec and validator selected by the helper.</summary>
    [Fact]
    public void WithJsonCodec_AfterJsonOptions_UsesExplicitCodecAndValidator()
    {
        // Arrange
        var custom = Substitute.For<IValueCodec<Card>>();
        var validator = Substitute.For<IValueCodecValidator<Card>>();
        var sut = new CacheValueContractBuilder<Card>().WithJsonOptions(JsonSerializerOptions.Web);
        using var services = new ServiceCollection().BuildServiceProvider();

        // Act
        var contract = sut.WithJsonCodec(_ => custom, _ => validator).Build("cards");

        // Assert
        contract.GetCodec("json", services).ShouldBeSameAs(custom);
        contract.GetValidator("json", services).ShouldBeSameAs(validator);
    }

    /// <summary>A rejected options argument leaves an already registered JSON codec and validator usable.</summary>
    [Fact]
    public void WithJsonOptions_NullOptions_RejectsWithoutReplacingProfile()
    {
        // Arrange
        var custom = Substitute.For<IValueCodec<Card>>();
        var validator = Substitute.For<IValueCodecValidator<Card>>();
        var sut = new CacheValueContractBuilder<Card>().WithJsonCodec(_ => custom, _ => validator);
        using var services = new ServiceCollection().BuildServiceProvider();

        // Act
        Should.Throw<ArgumentNullException>(() => sut.WithJsonOptions(null)).ParamName.ShouldBe("options");
        var contract = sut.Build("cards");

        // Assert
        contract.GetCodec("json", services).ShouldBeSameAs(custom);
        contract.GetValidator("json", services).ShouldBeSameAs(validator);
    }

    /// <summary>The convenience helper preserves reader rejection for duplicate persisted JSON formats.</summary>
    [Fact]
    public void WithJsonOptions_DuplicatePersistedFormat_RejectsAmbiguousReaders()
    {
        // Arrange
        var sut = new CacheValueContractBuilder<Card>()
            .WithCodec("other-json", _ => new SystemTextJsonValueCodec<Card>())
            .WithJsonOptions(JsonSerializerOptions.Web)
            .Build("cards");
        using var services = new ServiceCollection().BuildServiceProvider();

        // Act and assert
        Should.Throw<ArgumentException>(() => sut.CreateReaders(services));
    }

    /// <summary>An immutable fictional value whose JSON property and enum conventions are observable.</summary>
    /// <param name="DisplayName">The readable card label.</param>
    /// <param name="Status">The card's state.</param>
    /// <example><code>var card = new Card("West", CardStatus.Ready);</code></example>
    public sealed record Card(string DisplayName, CardStatus Status);

    /// <summary>A fictional state serialized through a caller-registered JSON converter.</summary>
    /// <example><code>var status = CardStatus.Ready;</code></example>
    public enum CardStatus
    {
        /// <summary>The card is ready for use.</summary>
        Ready
    }
}