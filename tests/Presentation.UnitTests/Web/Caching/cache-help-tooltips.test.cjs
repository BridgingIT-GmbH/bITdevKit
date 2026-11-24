const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../../../../src/Presentation.Web.Caching/Dashboard/Pages/Script.cshtml'), 'utf8');
const script = source.match(/<script>([\s\S]*?)<\/script>/)[1];

function fixture({ readyState = 'complete', bootstrap = true, deferredApply = false, applyError = null, applyResult } = {}) {
    const listeners = new Map();
    const events = [];
    const instances = new Map();
    const created = [];
    const state = { refreshes: 0, cancelledRefresh: 0, cancelledGrowing: 0 };
    const staticHelp = { id: 'static-help' };
    const initialHelp = { id: 'initial-help' };
    let fragment = [initialHelp], refresh, releaseApply;
    const applyGate = deferredApply ? new Promise(resolve => { releaseApply = resolve; }) : Promise.resolve();
    const workspace = {
        dataset: { pagePath: '/ops/caching', contentPath: '/ops/caching/content' },
        querySelector: () => null,
        querySelectorAll: () => [staticHelp, ...fragment],
        contains: element => element === staticHelp || fragment.includes(element),
        addEventListener: (name, callback) => listeners.set('workspace:' + name, { callback })
    };
    const content = { contains: element => fragment.includes(element) };
    const document = {
        readyState,
        getElementById: id => id === 'application-caching-workspace' ? workspace : id === 'cache-content' ? content : null,
        addEventListener: (name, callback, options) => listeners.set('document:' + name, { callback, once: options?.once }),
        removeEventListener: (name, callback) => {
            if (listeners.get('document:' + name)?.callback === callback) listeners.delete('document:' + name);
        }
    };
    const tooltipApi = {
        getOrCreateInstance(element, options) {
            if (!instances.has(element)) {
                const instance = {
                    element, options: { ...options }, hides: 0, disposals: 0,
                    hide() { this.hides++; events.push('hide:' + element.id); },
                    dispose() { this.disposals++; instances.delete(element); events.push('dispose:' + element.id); }
                };
                created.push(instance);
                instances.set(element, instance);
                events.push('create:' + element.id);
            }
            return instances.get(element);
        }
    };
    const window = {
        bootstrap: bootstrap ? { Tooltip: tooltipApi } : undefined,
        addEventListener: (name, callback) => listeners.set('window:' + name, { callback }),
        bdkDashboard: {
            createGrowingLists: () => ({
                busy: () => false,
                async applyResponse(root, replacement) {
                    assert.equal(root, content);
                    events.push('apply:start');
                    await applyGate;
                    if (applyError) throw applyError;
                    fragment = replacement;
                    events.push('apply:end');
                    return applyResult;
                },
                cancel: () => { state.cancelledGrowing++; }
            }),
            createRefresher: options => {
                refresh = options;
                return { refresh: () => { state.refreshes++; }, cancel: () => { state.cancelledRefresh++; } };
            }
        }
    };
    vm.runInNewContext(script, {
        window, document,
        location: { href: 'https://dashboard.test/ops/caching', origin: 'https://dashboard.test' },
        history: { replaceState() {} },
        URL
    });
    function emit(name, event = {}) {
        const listener = listeners.get(name);
        if (listener?.once) listeners.delete(name);
        return listener?.callback(event);
    }
    return {
        state, listeners, created, instances, events, staticHelp, initialHelp,
        apply: replacement => refresh.applyResponse(content, replacement),
        releaseApply: () => releaseApply?.(),
        installBootstrap: () => { window.bootstrap = { Tooltip: tooltipApi }; },
        ready: () => { document.readyState = 'complete'; return emit('document:DOMContentLoaded'); },
        pagehide: persisted => emit('window:pagehide', { persisted }),
        pageshow: persisted => emit('window:pageshow', { persisted }),
        dispose: () => window.bdkCachingView.dispose(),
        shouldRefresh: () => refresh.shouldRefresh()
    };
}

test('help tooltips initialize with keyboard focus, plain text and body placement', () => {
    const sut = fixture();

    assert.equal(sut.created.length, 2);
    for (const tooltip of sut.created) {
        assert.deepEqual(tooltip.options, { container: 'body', trigger: 'hover focus', html: false });
    }
    assert.equal(sut.state.refreshes, 0);
});

test('initialization waits for DOM readiness and can pick up Bootstrap loaded later', () => {
    const sut = fixture({ readyState: 'loading', bootstrap: false });
    assert.equal(sut.created.length, 0);
    assert.equal(sut.listeners.has('document:DOMContentLoaded'), true);

    sut.installBootstrap();
    sut.ready();
    sut.ready();

    assert.equal(sut.created.length, 2);
    assert.equal(sut.listeners.has('document:DOMContentLoaded'), false);
});

test('missing Bootstrap does not break content refresh and a later refresh can initialize help', async () => {
    const sut = fixture({ bootstrap: false });
    await sut.apply([{ id: 'without-bootstrap' }]);
    assert.equal(sut.created.length, 0);
    assert.equal(sut.shouldRefresh(), true);

    sut.installBootstrap();
    const latest = { id: 'latest-help' };
    await sut.apply([latest]);

    assert.equal(sut.instances.size, 2);
    assert.equal(sut.instances.has(latest), true);
    sut.dispose();
    assert.equal(sut.instances.size, 0);
});

test('asynchronous fragment refresh disposes old help before replacement and retains static help', async () => {
    const sut = fixture({ deferredApply: true });
    const staticTooltip = sut.instances.get(sut.staticHelp);
    const oldTooltip = sut.instances.get(sut.initialHelp);
    const replacement = { id: 'replacement-help' };

    const pending = sut.apply([replacement]);

    assert.equal(oldTooltip.disposals, 1);
    assert.equal(staticTooltip.disposals, 0);
    assert.equal(sut.created.length, 2);
    assert.ok(sut.events.indexOf('dispose:initial-help') < sut.events.indexOf('apply:start'));
    sut.releaseApply();
    await pending;

    assert.equal(sut.instances.get(sut.staticHelp), staticTooltip);
    assert.equal(sut.instances.has(replacement), true);
    assert.equal(sut.instances.size, 2);
    assert.ok(sut.events.indexOf('apply:end') < sut.events.indexOf('create:replacement-help'));
});

test('repeated fragment replacement releases detached help without accumulating instances', async () => {
    const sut = fixture();
    const first = { id: 'first-help' }, second = { id: 'second-help' };
    await sut.apply([first]);
    const firstTooltip = sut.instances.get(first);
    await sut.apply([second]);

    assert.equal(firstTooltip.disposals, 1);
    assert.equal(sut.instances.has(first), false);
    assert.equal(sut.instances.has(second), true);
    assert.equal(sut.instances.size, 2);
    assert.equal(sut.created.filter(tooltip => tooltip.element === sut.staticHelp).length, 1);
});

test('fragment application preserves a false growing-list result for the refresher', async () => {
    const sut = fixture({ applyResult: false });

    const result = await sut.apply([{ id: 'refreshed-help' }]);

    assert.equal(result, false);
    assert.equal(sut.instances.size, 2);
});

test('failed fragment application restores help on the retained visible content', async () => {
    const sut = fixture({ applyError: new Error('Fragment rejected') });
    const previous = sut.instances.get(sut.initialHelp);

    await assert.rejects(sut.apply([{ id: 'unapplied-help' }]), /Fragment rejected/);

    assert.equal(previous.disposals, 1);
    assert.equal(sut.instances.size, 2);
    assert.equal(sut.instances.has(sut.initialHelp), true);
    assert.notEqual(sut.instances.get(sut.initialHelp), previous);
});

test('cached page navigation hides help and restores without duplicate tooltip instances', () => {
    const sut = fixture();
    sut.pagehide(true);

    assert.ok(sut.created.every(tooltip => tooltip.hides === 1 && tooltip.disposals === 0));
    assert.equal(sut.shouldRefresh(), true);
    sut.pageshow(true);
    sut.pageshow(true);

    assert.equal(sut.created.length, 2);
    assert.equal(sut.instances.size, 2);
    assert.equal(sut.state.refreshes, 2);
});

test('navigation away hides and disposes owned tooltips and prevents further work', () => {
    const sut = fixture();
    sut.pagehide(false);

    assert.ok(sut.created.every(tooltip => tooltip.hides === 1 && tooltip.disposals === 1));
    assert.equal(sut.instances.size, 0);
    assert.equal(sut.shouldRefresh(), false);
    assert.ok(sut.state.cancelledRefresh > 0);
    assert.ok(sut.state.cancelledGrowing > 0);
});

test('disposing before DOM readiness removes deferred tooltip initialization', () => {
    const sut = fixture({ readyState: 'loading' });
    sut.dispose();
    sut.ready();

    assert.equal(sut.listeners.has('document:DOMContentLoaded'), false);
    assert.equal(sut.created.length, 0);
    assert.equal(sut.shouldRefresh(), false);
});

test('a fragment finishing after disposal does not recreate tooltip instances', async () => {
    const sut = fixture({ deferredApply: true });
    const pending = sut.apply([{ id: 'late-help' }]);
    sut.dispose();
    sut.releaseApply();
    await pending;

    assert.equal(sut.created.length, 2);
    assert.equal(sut.instances.size, 0);
    assert.equal(sut.shouldRefresh(), false);
});