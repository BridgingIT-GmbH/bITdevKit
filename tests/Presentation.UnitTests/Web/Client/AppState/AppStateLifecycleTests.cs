// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable BL0006 // Drive native persistence with a renderer without replacing framework behavior.

/// <summary>Exercises native Blazor capture and restore against real persistence services.</summary>
public class AppStateLifecycleTests
{
    /// <summary>Missing snapshots require no initial model copy that could remain captured by lifecycle callbacks.</summary>
    [Fact]
    public void Attach_NoSnapshot_DoesNotCopyOrRetainInitialModel()
    {
        using var sut = new CountingState();
        var native = CreateNative();

        using var lifecycle = AppStateLifecycle.Attach(sut, native.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);

        lifecycle.Restored.ShouldBeFalse();
        sut.Copies.ShouldBe(0);
    }

    /// <summary>Prerender values initialize a fresh instance through native callbacks before feature initialization.</summary>
    [Fact]
    public async Task Attach_PrerenderSnapshot_RestoresIndependentRuntime()
    {
        using var source = CreateState();
        await source.SetPageAsync(17);
        var store = await Persist(source);
        var native = CreateNative();
        await native.RestoreStateAsync(store);
        using var sut = CreateState();

        using var lifecycle = AppStateLifecycle.Attach(sut, native.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);

        lifecycle.Restored.ShouldBeTrue();
        sut.CurrentState.Page.ShouldBe(17);
        sut.InstanceId.ShouldNotBe(source.InstanceId);
        sut.CanUndo.ShouldBeFalse();
        store.Values.Count.ShouldBe(1);
    }

    /// <summary>Snapshots are captured at persistence time rather than frozen when the handle attaches.</summary>
    [Fact]
    public async Task Attach_EditAfterAttachment_CapturesLatestValues()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        using var renderer = new PersistenceRenderer(services);
        var native = CreateNative();
        using var sut = CreateState();
        using var lifecycle = AppStateLifecycle.Attach(sut, native.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);
        lifecycle.Restored.ShouldBeFalse();
        await sut.SetPageAsync(23);
        var store = new MemoryStateStore();

        await renderer.Dispatcher.InvokeAsync(() => native.PersistStateAsync(store, renderer));

        var json = JsonSerializer.Deserialize<string>(store.Values.Single().Value);
        JsonSerializer.Deserialize(json, AppStateLifecycleJson.Default.CatalogViewModel).Page.ShouldBe(23);
    }

    /// <summary>Malformed native values or model payloads fall back to normal feature initialization.</summary>
    [Theory]
    [InlineData("not-json")]
    [InlineData("\"not-model-json\"")]
    [InlineData("null")]
    [InlineData("\"null\"")]
    public async Task Attach_CorruptSnapshot_KeepsUsableDefaults(string payload)
    {
        using var sut = CreateState();
        var store = new MemoryStateStore();
        store.Values[sut.CaptureHandoff().Key] = System.Text.Encoding.UTF8.GetBytes(payload);
        var native = CreateNative();
        await native.RestoreStateAsync(store);

        using var lifecycle = AppStateLifecycle.Attach(sut, native.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);

        lifecycle.Restored.ShouldBeFalse();
        sut.CurrentState.Page.ShouldBe(0);
        await sut.SetPageAsync(3);
        sut.CurrentState.Page.ShouldBe(3);
    }

    /// <summary>One lifecycle owner reserves the logical instance, and disposal releases both callbacks and reservation.</summary>
    [Fact]
    public async Task Dispose_LogicalOwner_ReleasesCallbacksAndKeepsState()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        using var renderer = new PersistenceRenderer(services);
        var native = CreateNative();
        using var sut = CreateState();
        var lifecycle = AppStateLifecycle.Attach(sut, native.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);
        Should.Throw<InvalidOperationException>(() => AppStateLifecycle.Attach(sut, native.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer));

        lifecycle.Dispose();
        lifecycle.Dispose();
        await sut.SetPageAsync(19);
        var store = new MemoryStateStore();
        await renderer.Dispatcher.InvokeAsync(() => native.PersistStateAsync(store, renderer));

        store.Values.ShouldBeEmpty();
        sut.CurrentState.Page.ShouldBe(19);
        using var replacement = AppStateLifecycle.Attach(sut, native.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);
        replacement.Restored.ShouldBeFalse();
    }

    /// <summary>A native snapshot cannot replace a filter edited before attachment.</summary>
    [Fact]
    public async Task Attach_ExistingUserEdit_PreservesInput()
    {
        using var source = CreateState();
        await source.SetPageAsync(17);
        var native = CreateNative();
        await native.RestoreStateAsync(await Persist(source));
        using var sut = CreateState();
        await sut.SetPageAsync(3);

        using var lifecycle = AppStateLifecycle.Attach(sut, native.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);

        lifecycle.Restored.ShouldBeFalse();
        sut.CurrentState.Page.ShouldBe(3);
    }

    /// <summary>Native last-snapshot restoration starts a new owner and can be disabled independently of prerender handoff.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Attach_CircuitSnapshot_RespectsRestorePolicy(bool restoreCircuit)
    {
        using var source = CreateState();
        await source.SetPageAsync(31);
        var native = CreateNative();
        await native.RestoreStateAsync(await Persist(source), RestoreContext.LastSnapshot);
        using var sut = CreateState();
        var previousVersion = sut.Version;

        using var lifecycle = AppStateLifecycle.Attach(sut, native.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer, restoreCircuit);

        lifecycle.Restored.ShouldBe(restoreCircuit);
        sut.CurrentState.Page.ShouldBe(restoreCircuit ? 31 : 0);
        sut.InstanceId.ShouldNotBe(source.InstanceId);
        sut.IsCurrent(source.Version).ShouldBeFalse();
        sut.IsCurrent(previousVersion).ShouldBe(!restoreCircuit);
        sut.CanUndo.ShouldBeFalse();
    }

    /// <summary>A circuit snapshot from another context cannot initialize the current runtime.</summary>
    [Fact]
    public async Task Attach_CircuitContextMismatch_KeepsDefaults()
    {
        using var source = CreateState();
        await source.ChangeContext(new("operator", "north"));
        await source.SetPageAsync(31);
        var native = CreateNative();
        await native.RestoreStateAsync(await Persist(source), RestoreContext.LastSnapshot);
        using var sut = CreateState();
        await sut.ChangeContext(new("other-operator", "south"));

        using var lifecycle = AppStateLifecycle.Attach(sut, native.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);

        lifecycle.Restored.ShouldBeFalse();
        sut.CurrentState.Page.ShouldBe(0);
    }

    /// <summary>Distinct stable instance keys preserve sibling state even when restored in reverse order.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Attach_TwoInstances_RestoresMatchingOwners(bool circuit)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        using var renderer = new PersistenceRenderer(services);
        var sourceNative = CreateNative();
        using var first = CreateState("left");
        using var second = CreateState("right");
        await first.SetPageAsync(17);
        await second.SetPageAsync(31);
        using var firstOwner = AppStateLifecycle.Attach(first, sourceNative.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);
        using var secondOwner = AppStateLifecycle.Attach(second, sourceNative.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);
        var store = new MemoryStateStore();
        await renderer.Dispatcher.InvokeAsync(() => sourceNative.PersistStateAsync(store, renderer));
        var targetNative = CreateNative();
        await targetNative.RestoreStateAsync(store, circuit ? RestoreContext.LastSnapshot : RestoreContext.InitialValue);
        using var secondTarget = CreateState("right");
        using var firstTarget = CreateState("left");

        using var restoredSecond = AppStateLifecycle.Attach(secondTarget, targetNative.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);
        using var restoredFirst = AppStateLifecycle.Attach(firstTarget, targetNative.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);

        restoredFirst.Restored.ShouldBeTrue();
        restoredSecond.Restored.ShouldBeTrue();
        firstTarget.CurrentState.Page.ShouldBe(17);
        secondTarget.CurrentState.Page.ShouldBe(31);
        store.Values.Count.ShouldBe(2);
    }

    /// <summary>Context invalidation during a model converter cannot restore old-context values.</summary>
    [Fact]
    public async Task Attach_ContextChangesDuringDeserialization_RejectsSnapshot()
    {
        using var source = CreateState();
        await source.SetPageAsync(31);
        var native = CreateNative();
        await native.RestoreStateAsync(await Persist(source));
        using var sut = CreateState();
        var options = new JsonSerializerOptions();
        options.Converters.Add(new CallbackConverter(beforeRead: () => { _ = sut.ChangeContext(new("other", "north")); }));

        using var lifecycle = AppStateLifecycle.Attach(sut, native.State,
            new AppStateLifecycleJson(options).CatalogViewModel, RenderMode.InteractiveServer);

        lifecycle.Restored.ShouldBeFalse();
        sut.CurrentState.Page.ShouldBe(0);
    }

    /// <summary>Capture rejects a serialized payload if its source context became obsolete while encoding.</summary>
    [Fact]
    public async Task Persist_ContextChangesDuringSerialization_OmitsObsoletePayload()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        using var renderer = new PersistenceRenderer(services);
        var native = CreateNative();
        using var sut = CreateState();
        await sut.SetPageAsync(31);
        var options = new JsonSerializerOptions();
        options.Converters.Add(new CallbackConverter(beforeWrite: () => { _ = sut.ChangeContext(new("other", "north")); }));
        using var lifecycle = AppStateLifecycle.Attach(sut, native.State,
            new AppStateLifecycleJson(options).CatalogViewModel, RenderMode.InteractiveServer);
        var store = new MemoryStateStore();

        await renderer.Dispatcher.InvokeAsync(() => native.PersistStateAsync(store, renderer));

        store.Values.ShouldBeEmpty();
        sut.CurrentState.Page.ShouldBe(0);
    }

    private static CatalogViewState CreateState(string instance = "default") => new(NullLogger<CatalogViewState>.Instance,
        new() { ApplicationId = "catalog-native", InstanceKey = instance, HistoryEnabled = true, MaxHistoryItems = 3 });

    private static ComponentStatePersistenceManager CreateNative() =>
        new(NullLogger<ComponentStatePersistenceManager>.Instance);

    private static async Task<MemoryStateStore> Persist(CatalogViewState state)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        using var renderer = new PersistenceRenderer(services);
        var native = CreateNative();
        using var lifecycle = AppStateLifecycle.Attach(state, native.State,
            AppStateLifecycleJson.Default.CatalogViewModel, RenderMode.InteractiveServer);
        var store = new MemoryStateStore();
        await renderer.Dispatcher.InvokeAsync(() => native.PersistStateAsync(store, renderer));
        return store;
    }

    private sealed class MemoryStateStore : IPersistentComponentStateStore
    {
        /// <summary>Gets captured native payloads for a fresh runtime.</summary>
        public Dictionary<string, byte[]> Values { get; } = [];

        /// <inheritdoc/>
        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() =>
            Task.FromResult<IDictionary<string, byte[]>>(this.Values.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray()));

        /// <inheritdoc/>
        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state)
        {
            this.Values.Clear();
            foreach (var entry in state) { this.Values.Add(entry.Key, entry.Value.ToArray()); }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public bool SupportsRenderMode(IComponentRenderMode renderMode) => renderMode is InteractiveServerRenderMode;
    }

    private sealed class PersistenceRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
    {
        /// <inheritdoc/>
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        /// <inheritdoc/>
        protected override void HandleException(Exception exception) => throw exception;

        /// <inheritdoc/>
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }

    private sealed class CallbackConverter(Action beforeRead = null, Action beforeWrite = null) : JsonConverter<CatalogViewModel>
    {
        /// <inheritdoc/>
        public override CatalogViewModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            beforeRead?.Invoke();
            return JsonSerializer.Deserialize(ref reader, AppStateLifecycleJson.Default.CatalogViewModel);
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, CatalogViewModel value, JsonSerializerOptions options)
        {
            beforeWrite?.Invoke();
            JsonSerializer.Serialize(writer, value, AppStateLifecycleJson.Default.CatalogViewModel);
        }
    }

    private sealed class CountingState() : CatalogViewState(NullLogger<CatalogViewState>.Instance, new())
    {
        /// <summary>Gets the number of model copies made by the state.</summary>
        public int Copies { get; private set; }

        /// <inheritdoc/>
        protected override CatalogViewModel CloneState(CatalogViewModel value)
        {
            this.Copies++;
            return base.CloneState(value);
        }
    }
}

/// <summary>Supplies generated metadata for native lifecycle model serialization.</summary>
[JsonSerializable(typeof(CatalogViewModel))]
public partial class AppStateLifecycleJson : JsonSerializerContext;
