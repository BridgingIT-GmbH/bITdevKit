// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Auditing;

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

public class AuditContractTests
{
    [Fact]
    public void Build_MinimalAndExplicitDefaults_HaveEquivalentEffectiveOptions()
    {
        var sut = AuditScopeOptionsBuilder.Create<Customer>(nameof(Customer.RenameAsync)).Build();
        var explicitOptions = AuditScopeOptionsBuilder.Create<Customer>(nameof(Customer.RenameAsync))
            .Key(typeof(Customer).FullName + ".RenameAsync").DisplayName("Customer: Rename")
            .Kind("Business").Mode(AuditScopeMode.JoinOrStart).Build();

        sut.Descriptor.Key.ShouldBe(explicitOptions.Descriptor.Key);
        sut.Descriptor.DisplayName.ShouldBe(explicitOptions.Descriptor.DisplayName);
        sut.Kind.ShouldBe(explicitOptions.Kind);
        sut.Mode.ShouldBe(explicitOptions.Mode);
        sut.Descriptor.KeyDerived.ShouldBeTrue();
        explicitOptions.Descriptor.KeyDerived.ShouldBeFalse();
    }

    [Fact]
    public void Build_ChangedBuilder_DoesNotChangePreviousSnapshot()
    {
        var builder = AuditDescriptorBuilder.For<Customer>().Resource<Customer>("C-1").Detail("count", AuditValue.From(1L));
        var sut = builder.Build();

        builder.Resource<Customer>("C-2").Detail("count", AuditValue.From(2L)).Key("customers.changed");

        sut.Resources.Count.ShouldBe(1);
        sut.Details["count"].Data.GetInt64().ShouldBe(1);
        sut.Key.ShouldBe(typeof(Customer).FullName);
        builder.Build().Resources.Count.ShouldBe(2);
    }

    [Fact]
    public void Build_ConstructedGenericContext_PreservesArgumentsWithoutAssemblyMetadata()
    {
        var type = typeof(GenericOperation<Customer, RenameCustomerCommand, Dictionary<string, Customer>>);
        type.FullName.Length.ShouldBeGreaterThan(512);

        var options = AuditScopeOptionsBuilder.Create(type, "Execute").Build();
        var another = AuditScopeOptionsBuilder.Create(typeof(GenericOperation<Customer, Customer, Dictionary<string, Customer>>), "Execute").Build();

        options.Descriptor.Key.Length.ShouldBeLessThanOrEqualTo(512);
        options.Descriptor.Key.ShouldContain(typeof(Customer).FullName);
        options.Descriptor.Key.ShouldContain(typeof(RenameCustomerCommand).FullName);
        options.Descriptor.Key.ShouldContain("System.Collections.Generic.Dictionary");
        options.Descriptor.Key.ShouldEndWith(".Execute");
        options.Descriptor.Key.ShouldNotContain("Version=");
        options.Descriptor.Key.ShouldNotContain("PublicKeyToken=");
        options.Descriptor.Key.ShouldNotBe(another.Descriptor.Key);
    }

    [Fact]
    public void Build_RuntimeAndTypedContext_ProduceEquivalentNames()
    {
        var sut = AuditScopeOptionsBuilder.Create(typeof(Customer), "RenameAsync").Build();
        var typed = new AuditScopeOptionsBuilder().For<Customer>("RenameAsync").Build();

        sut.Descriptor.Key.ShouldBe(typed.Descriptor.Key);
        sut.Descriptor.DisplayName.ShouldBe("Customer: Rename");
        AuditDescriptorBuilder.For<RenameCustomerCommand>().Build().DisplayName.ShouldBe("Rename customer");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid\nkey")]
    public void Build_InvalidKey_RejectsWithoutEchoingInput(string key)
    {
        var error = Should.Throw<ArgumentException>(() => AuditScopeOptionsBuilder.Create(key).Build());
        error.Message.ShouldStartWith("Audit metadata has an invalid structure.");
    }

    [Theory]
    [InlineData("AuditId")]
    [InlineData("outcome")]
    [InlineData("UserId")]
    [InlineData("ContentHash")]
    public void Detail_ReservedIdentity_RejectsReplacement(string key)
    {
        Should.Throw<ArgumentException>(() => AuditDescriptorBuilder.For<Customer>().Detail(key, AuditValue.From("secret")));
    }

    [Fact]
    public void Json_DisposedSourceAndChangedArray_RetainsCopiedValues()
    {
        AuditValue sut;
        using (var source = JsonDocument.Parse("{\"allowed\":1}")) sut = AuditValue.Json(source.RootElement);
        var values = new[] { "before" };
        var strings = AuditValue.From(values);
        values[0] = "after";

        sut.Data.GetProperty("allowed").GetInt32().ShouldBe(1);
        strings.Data[0].GetString().ShouldBe("before");
        AuditValue.From((string)null).Marker.ShouldBeNull();
        AuditValue.Policy("Redacted").Marker.ShouldBe("Redacted");
        Should.Throw<ArgumentOutOfRangeException>(() => AuditValue.From(double.NaN));
    }

    [Fact]
    public void Resource_NonInvariantCulture_KeepsInvariantSelectedId()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var sut = AuditDescriptorBuilder.For<Customer>().Resource<Customer>(12.5m).Build();
            sut.Resources.Single().Id.ShouldBe("12.5");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task RunAsync_NoOp_SkipsMetadataAndInvokesBusinessOnce()
    {
        var sut = new NoOpAuditContext();
        var calls = 0;
        using var suppressed = sut.Suppress();
        using var scope = sut.BeginScope(_ => throw new InvalidOperationException("Metadata must not run"));

        var result = await sut.RunAsync(_ => throw new InvalidOperationException("Metadata must not run"), (handle, ct) =>
        {
            calls++;
            handle.IsRecording.ShouldBeFalse();
            return Task.FromResult(42);
        });

        calls.ShouldBe(1);
        result.ShouldBe(42);
        sut.Current.ShouldBeNull();
        scope.AuditId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public async Task RunAsync_NoOpFailure_PreservesOriginalExceptionAndCancellation()
    {
        var sut = new NoOpAuditContext();
        var original = new InvalidOperationException("business failure");
        var error = await Should.ThrowAsync<InvalidOperationException>(() => sut.RunAsync<int>(
            AuditScopeOptionsBuilder.Create("business").Build(), (_, _) => Task.FromException<int>(original)));
        error.ShouldBeSameAs(original);

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var cancel = await Should.ThrowAsync<OperationCanceledException>(() => sut.RunAsync<int>(
            AuditScopeOptionsBuilder.Create("business").Build(), (_, ct) => Task.FromCanceled<int>(ct), canceled.Token));
        cancel.CancellationToken.ShouldBe(canceled.Token);
    }

    [Fact]
    public void AddNoOpAuditContext_RepeatedRegistration_PreservesHostImplementation()
    {
        var custom = Substitute.For<IAuditContext>();
        var services = new ServiceCollection().AddSingleton(custom);
        services.AddNoOpAuditContext().AddNoOpAuditContext();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAuditContext>().ShouldBeSameAs(custom);
        services.Count(d => d.ServiceType == typeof(IAuditContext)).ShouldBe(1);
    }

    [Fact]
    public void Registration_MetadataOnly_HasStableBoundedIdentity()
    {
        var sut = new AuditBoundaryRegistration("Requester", "dispatch", "Selected", "CustomerRename");
        sut.Selection.ShouldBe("selected");
        sut.Identity.ShouldBe(new AuditBoundaryRegistration("Requester", "dispatch", "selected", "CustomerRename").Identity);
        Should.Throw<ArgumentException>(() => new AuditBoundaryRegistration("Requester", "dispatch", "made-up"));
        Should.Throw<ArgumentException>(() => new AuditResourceReference("Customer", ""));
    }

    [Theory]
    [InlineData("raw")]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeg")]
    public void Policy_InvalidHash_RejectsWithoutEchoingDigest(string digest)
    {
        var error = Should.Throw<ArgumentException>(() => AuditValue.Policy("Hashed", digest));
        error.Message.ShouldStartWith("An audit hash must be a SHA-256 hexadecimal digest or an omitted digest.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    public void Policy_ValidOrOmittedHash_RetainsDigest(string digest)
    {
        var sut = AuditValue.Policy("Hashed", digest);
        sut.Marker.ShouldBe("Hashed");
        sut.Data.GetString().ShouldBe(digest);
    }

    [Theory]
    [InlineData("\"raw\"")]
    [InlineData("42")]
    [InlineData("{\"value\":\"raw\"}")]
    public void Deserialize_InvalidHash_RejectsMarkedPayload(string data)
    {
        Should.Throw<ArgumentException>(() => JsonSerializer.Deserialize<AuditValue>("{\"Data\":" + data + ",\"Marker\":\"Hashed\"}"));
    }

    private sealed class Customer { public void RenameAsync() { } }
    private sealed class RenameCustomerCommand;

    private sealed class GenericOperation<TInput, THandler, TData>;
}
