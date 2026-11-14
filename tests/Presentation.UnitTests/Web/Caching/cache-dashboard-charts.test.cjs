const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../../../../src/Presentation.Web.Caching/Dashboard/Pages/DiagnosticCharts.cshtml'), 'utf8');
const script = source.match(/<script>([\s\S]*?)<\/script>/)[1];

function fixture({ overviewMetrics = {}, overviewFamilies = null } = {}) {
    const calls = [];
    const purged = [];
    const cards = new Map();
    const history = { textContent: '' };
    const listeners = new Map();
    const frames = new Map();
    let configuration, resize, payload, cancellations = 0, refreshes = 0, disconnected = false, previousDisposed = false, frameId = 0;
    const advanced = {
        open: false,
        addEventListener: (name, callback) => listeners.set('advanced:' + name, callback),
        removeEventListener: name => listeners.delete('advanced:' + name)
    };
    function card(key, level) {
        const id = level === 'overview' ? key : 'advanced:' + key;
        if (!cards.has(id)) {
            const metrics = level === 'overview' && overviewMetrics[key] ? overviewMetrics[key] :
                payload.charts.find(chart => chart.family.key === key).series.map(series => series.descriptor.metric);
            const host = { key, level, hidden: false, plotted: false, get clientWidth() { return level === 'overview' || advanced.open ? 640 : 0; } };
            host.classList = { contains: () => host.plotted };
            const state = { textContent: '', hidden: false };
            const unit = { textContent: '' };
            cards.set(id, {
                host, state, unit,
                dataset: { cacheChartLevel: level, cacheChartMetrics: metrics.join(',') },
                querySelector: query => query === '[data-cache-chart-plot]' ? host : query === '[data-cache-chart-state]' ? state : unit
            });
        }
        return cards.get(id);
    }
    const root = {
        querySelector(selector) {
            if (selector === '[data-cache-history-window]') return history;
            if (selector === '[data-cache-advanced]') return advanced;
            const key = selector.match(/^\[data-cache-chart="([^"]+)"\]$/)?.[1];
            return key ? card(key, 'overview') : null;
        },
        querySelectorAll(selector) {
            if (selector === '.js-plotly-plot') return [...cards.values()].map(card => card.host).filter(host => host.plotted);
            const key = selector.match(/^\[data-cache-chart="([^"]+)"\]$/)?.[1];
            if (!key) return [];
            return [
                ...(overviewFamilies === null || overviewFamilies.includes(key) ? [card(key, 'overview')] : []),
                card(key, 'advanced')
            ];
        }
    };
    const workspace = {
        dataset: { historyPath: '/ops/caching/history', pagePath: '/ops/caching/diagnostics' },
        querySelector: () => root
    };
    const plotly = {
        react: async (host, traces, layout) => { host.plotted = true; calls.push({ host, traces, layout }); },
        purge: host => { host.plotted = false; purged.push(host); }
    };
    const window = {
        Plotly: plotly,
        bdkCachingCharts: { dispose: () => { previousDisposed = true; } },
        addEventListener: (name, callback) => listeners.set('window:' + name, callback),
        bdkDashboard: {
            loadPlotly: async () => plotly,
            getChartTheme: () => ({ primary: 'blue', secondary: 'purple', success: 'green', danger: 'red', warning: 'gold', info: 'cyan' }),
            createPlotlyLayout: options => ({ ...options, xaxis: {}, yaxis: {} }),
            createRefresher: options => {
                configuration = options;
                return { refresh: () => { refreshes++; }, cancel: () => { cancellations++; } };
            }
        }
    };
    vm.runInNewContext(script, {
        window,
        document: {
            hidden: false,
            getElementById: () => workspace,
            addEventListener: (name, callback) => listeners.set(name, callback),
            removeEventListener: name => listeners.delete(name)
        },
        location: { origin: 'https://dashboard.test', search: '' },
        innerWidth: 1280,
        URL,
        ResizeObserver: class { constructor(callback) { resize = callback; } observe() {} disconnect() { disconnected = true; } },
        requestAnimationFrame: callback => { frames.set(++frameId, callback); return frameId; },
        cancelAnimationFrame: id => frames.delete(id)
    });
    async function flushFrames() {
        for (const [id, callback] of frames) { frames.delete(id); await callback(); }
    }
    return {
        calls, purged, cards, history, listeners, advanced,
        apply: value => { payload = value; return configuration.applyResponse(root, JSON.stringify(value)); },
        toggle: async open => { advanced.open = open; await listeners.get('advanced:toggle')?.(); await flushFrames(); },
        resize: async () => { resize(); await flushFrames(); },
        theme: () => listeners.get('bdk-dashboard-theme-changed')?.(),
        dispose: () => window.bdkCachingCharts.dispose(),
        shouldRefresh: () => configuration.shouldRefresh(),
        state: () => ({ cancellations, refreshes, disconnected, previousDisposed })
    };
}

function series(metric, unit, values) {
    return {
        descriptor: { metric, unit, collection: 'items' },
        points: values.map((value, index) => ({ at: `2026-11-11T09:00:0${index}Z`, value }))
    };
}

function history(charts) {
    return {
        charts, scope: 'NodeLocal', windowMinutes: 15,
        observedAt: '2026-11-11T09:15:00Z', availableFrom: '2026-11-11T09:00:00Z', availableTo: '2026-11-11T09:15:00Z'
    };
}

function chart(key, unit, series, availability = 'Available', reason = null) {
    return { family: { key, unit }, series, availability, reason };
}

test('byte axes and hover show explicit matching IEC sizes instead of decimal SI k', async () => {
    const sut = fixture();
    await sut.apply(history([chart('capacity', 'bytes', [series('ResidentBytes', 'bytes', [0, 904512, null])], 0)]));

    const { traces, layout } = sut.calls[0];
    assert.equal(layout.yaxis.title.text, 'KiB');
    assert.equal(layout.yaxis.tickformat, ',.2f');
    assert.equal(sut.cards.get('capacity').unit.textContent, 'KiB');
    assert.deepEqual(Array.from(traces[0].y), [0, 883.3125, null]);
    assert.deepEqual(Array.from(traces[0].customdata), ['0 B', '883.31 KiB', null]);
    assert.match(traces[0].hovertemplate, /customdata/);
    assert.equal(traces[0].connectgaps, false);
    assert.equal(sut.cards.get('capacity').state.hidden, true);
    assert.match(sut.history.textContent, /^Current node · 15 min · /);
    assert.match(sut.history.textContent, /UTC$/);
});

for (const [value, unit, label] of [
    [0, 'B', '0 B'], [1023, 'B', '1023 B'], [1024, 'KiB', '1 KiB'],
    [1152, 'KiB', '1.13 KiB'], [1536, 'KiB', '1.5 KiB'],
    [1048576, 'MiB', '1 MiB'], [1073741824, 'GiB', '1 GiB'],
    [1099511627776, 'TiB', '1 TiB'], [1125899906842624, 'PiB', '1 PiB'],
    [1152921504606846976, 'EiB', '1 EiB']
]) {
    test(`byte display keeps the ${label} boundary explicit`, async () => {
        const sut = fixture();
        await sut.apply(history([chart('capacity', 'bytes', [series('ResidentBytes', 'bytes', [value])])]));

        assert.equal(sut.calls[0].layout.yaxis.title.text, unit);
        assert.equal(sut.calls[0].traces[0].customdata[0], label);
    });
}

test('byte scaling ignores count magnitudes and leaves a secondary count axis unchanged', async () => {
    const sut = fixture();
    await sut.apply(history([chart('capacity', 'bytes', [
        series('ResidentBytes', 'bytes', [904512]), series('ActiveOwners', 'count', [9000000000000])
    ])]));

    const { traces, layout } = sut.calls[0];
    assert.equal(layout.yaxis.title.text, 'KiB');
    assert.equal(layout.yaxis2.title.text, 'count');
    assert.equal(traces[1].yaxis, 'y2');
    assert.equal(traces[1].y[0], 9000000000000);
    assert.equal(traces[1].customdata, undefined);
});

test('non-byte rates, ratios and missing points retain their source values and axes', async () => {
    const sut = fixture();
    await sut.apply(history([chart('traffic', 'events/s', [
        series('Request', 'events/s', [1.25, null, 0]), series('LookupHitRatio', 'ratio', [0.5, null, 1])
    ])]));

    const { traces, layout } = sut.calls[0];
    assert.equal(layout.yaxis.title.text, 'events/s');
    assert.equal(layout.yaxis2.title.text, 'ratio');
    assert.deepEqual(Array.from(traces[0].y), [1.25, null, 0]);
    assert.deepEqual(Array.from(traces[1].y), [0.5, null, 1]);
    assert.equal(traces[1].yaxis, 'y2');
    assert.equal(traces[0].customdata, undefined);
});

test('unavailable history remains distinct from zero and removes an obsolete plot', async () => {
    const sut = fixture();
    await sut.apply(history([chart('capacity', 'bytes', [series('ResidentBytes', 'bytes', [904512])])]));
    await sut.apply(history([chart('capacity', 'bytes', [series('ResidentBytes', 'bytes', [null])], 'Disabled', 'History disabled')]));

    const card = sut.cards.get('capacity');
    assert.equal(sut.calls.length, 1);
    assert.equal(card.host.hidden, true);
    assert.equal(card.state.hidden, false);
    assert.equal(card.state.textContent, 'History disabled');
    assert.equal(sut.purged.length, 1);
});

test('partial-history warnings remain visible even when observations can be plotted', async () => {
    const sut = fixture();
    await sut.apply(history([chart('capacity', 'bytes', [series('ResidentBytes', 'bytes', [1024])], 'Available', 'Showing the first 16 series.')]));

    assert.equal(sut.cards.get('capacity').state.hidden, false);
    assert.equal(sut.cards.get('capacity').state.textContent, 'Showing the first 16 series.');
});

test('disabled sampling remains visible when retained history still has values', async () => {
    const sut = fixture();
    await sut.apply(history([chart('capacity', 'bytes', [series('ResidentBytes', 'bytes', [1024])], 'Disabled')]));

    assert.equal(sut.cards.get('capacity').host.hidden, false);
    assert.equal(sut.cards.get('capacity').state.hidden, false);
    assert.equal(sut.cards.get('capacity').state.textContent, 'Disabled');
});

test('disposal cancels refresh, disconnects resizing and purges owned charts', async () => {
    const sut = fixture();
    await sut.apply(history([chart('capacity', 'bytes', [series('ResidentBytes', 'bytes', [1024])])]));
    sut.dispose();

    assert.equal(sut.state().previousDisposed, true);
    assert.equal(sut.state().cancellations, 1);
    assert.equal(sut.state().disconnected, true);
    assert.equal(sut.purged.length, 1);
    assert.equal(sut.listeners.has('bdk-dashboard-theme-changed'), false);
    assert.equal(sut.listeners.has('advanced:toggle'), false);
    assert.equal(sut.shouldRefresh(), false);
});

function overviewFixture() {
    return fixture({
        overviewFamilies: ['traffic', 'latency', 'capacity', 'reliability'],
        overviewMetrics: {
            traffic: ['Request', 'LookupHitRatio'],
            latency: ['OperationP95'],
            capacity: ['ResidentBytes', 'TransientBytes'],
            reliability: ['ProviderFailure', 'SourceFailure', 'WaitTimeout', 'SourceTimeout']
        }
    });
}

function detailedHistory(resident = 904512) {
    return history([
        chart('traffic', 'events/s', [series('Request', 'events/s', [10]), series('PositiveHit', 'events/s', [8]),
            series('LookupHitRatio', 'ratio', [0.8]), series('HotBinaryReuse', 'events/s', [2])]),
        chart('throughput', 'entries/s', [series('ReadSuccess', 'entries/s', [9])]),
        chart('latency', 's', [series('OperationP50', 's', [0.01]), series('OperationP95', 's', [0.05]), series('OperationP99', 's', [0.1])]),
        chart('shared', 'events/s', [series('SourceAttempt', 'events/s', [1]), series('Join', 'events/s', [3])]),
        chart('capacity', 'bytes', [series('ResidentBytes', 'bytes', [resident]), series('TransientBytes', 'bytes', [1024]), series('HotPinnedBytes', 'bytes', [512])]),
        chart('reliability', 'events/s', [series('ProviderFailure', 'events/s', [0]), series('SourceFailure', 'events/s', [0]),
            series('WaitTimeout', 'events/s', [0]), series('SourceTimeout', 'events/s', [0]), series('TransferFailed', 'events/s', [0])])
    ]);
}

test('overview draws four compact families while advanced diagnostics starts collapsed', async () => {
    const sut = overviewFixture();
    await sut.apply(detailedHistory());

    assert.equal(sut.advanced.open, false);
    assert.deepEqual(sut.calls.map(call => call.host.key), ['traffic', 'latency', 'capacity', 'reliability']);
    assert.ok(sut.calls.every(call => call.host.level === 'overview'));
    assert.deepEqual(sut.calls.map(call => call.traces.length), [2, 1, 2, 4]);
    assert.deepEqual(Array.from(sut.calls[0].traces, trace => trace.name), ['items · Request', 'items · Lookup Hit Ratio']);
    assert.equal(sut.calls[0].layout.yaxis2.title.text, 'ratio');
    assert.equal(sut.state().refreshes, 1);
});

test('opening advanced renders every detailed family at visible width without requesting more history', async () => {
    const sut = overviewFixture();
    await sut.apply(detailedHistory());
    await sut.toggle(true);

    const detailed = sut.calls.filter(call => call.host.level === 'advanced');
    assert.deepEqual(detailed.map(call => call.host.key), ['traffic', 'throughput', 'latency', 'shared', 'capacity', 'reliability']);
    assert.deepEqual(detailed.map(call => call.traces.length), [4, 1, 3, 2, 3, 5]);
    assert.ok(detailed.every(call => call.layout.width === 640));
    assert.equal(sut.state().refreshes, 1);
});

test('closed advanced charts stop rendering and reopening draws the newest cached samples', async () => {
    const sut = overviewFixture();
    await sut.apply(detailedHistory());
    await sut.toggle(true);
    await sut.toggle(false);
    sut.calls.length = 0;

    await sut.apply(detailedHistory(1048576));

    assert.equal(sut.calls.length, 4);
    assert.ok(sut.calls.every(call => call.host.level === 'overview'));
    await sut.toggle(true);
    const capacity = sut.calls.findLast(call => call.host.key === 'capacity' && call.host.level === 'advanced');
    assert.equal(capacity.traces[0].customdata[0], '1 MiB');
    assert.equal(capacity.layout.yaxis.title.text, 'MiB');
    assert.equal(sut.state().refreshes, 1);
});

test('overview byte scaling depends only on its selected series rather than hidden advanced values', async () => {
    const sut = fixture({ overviewMetrics: { capacity: ['ResidentBytes'] } });
    await sut.apply(history([chart('capacity', 'bytes', [
        series('ResidentBytes', 'bytes', [1024]), series('HotPinnedBytes', 'bytes', [1099511627776])
    ])]));

    assert.equal(sut.calls[0].traces.length, 1);
    assert.equal(sut.calls[0].layout.yaxis.title.text, 'KiB');
    assert.equal(sut.calls[0].traces[0].y[0], 1);
    await sut.toggle(true);
    const detailed = sut.calls.find(call => call.host.level === 'advanced');
    assert.equal(detailed.layout.yaxis.title.text, 'TiB');
    assert.equal(detailed.traces[1].customdata[0], '1 TiB');
});

test('a basic subset without samples stays unavailable even when detailed series have values', async () => {
    const sut = fixture({ overviewMetrics: { capacity: ['ResidentBytes'] } });
    await sut.apply(history([chart('capacity', 'bytes', [series('HotPinnedBytes', 'bytes', [1024])])]));

    assert.equal(sut.calls.length, 0);
    assert.equal(sut.cards.get('capacity').host.hidden, true);
    assert.equal(sut.cards.get('capacity').state.hidden, false);
    assert.equal(sut.cards.get('capacity').state.textContent, 'No samples');
    await sut.toggle(true);
    assert.equal(sut.calls.length, 1);
    assert.equal(sut.calls[0].host.level, 'advanced');
});

test('resize and theme redraws honor the current advanced disclosure state', async () => {
    const sut = overviewFixture();
    await sut.apply(detailedHistory());
    sut.calls.length = 0;
    await sut.resize();
    await sut.theme();

    assert.equal(sut.calls.length, 8);
    assert.ok(sut.calls.every(call => call.host.level === 'overview'));
    await sut.toggle(true);
    sut.calls.length = 0;
    await sut.theme();
    assert.equal(sut.calls.filter(call => call.host.level === 'advanced').length, 6);
    await sut.toggle(false);
    sut.calls.length = 0;
    await sut.resize();
    assert.equal(sut.calls.length, 4);
    assert.ok(sut.calls.every(call => call.host.level === 'overview'));
});

test('disposal removes disclosure handling and purges both overview and advanced charts', async () => {
    const sut = overviewFixture();
    await sut.apply(detailedHistory());
    await sut.toggle(true);
    sut.dispose();

    assert.equal(sut.purged.length, 10);
    assert.equal(sut.listeners.has('advanced:toggle'), false);
    assert.equal(sut.listeners.has('bdk-dashboard-theme-changed'), false);
    assert.equal(sut.state().disconnected, true);
    assert.equal(sut.shouldRefresh(), false);
    const calls = sut.calls.length;
    await sut.toggle(false);
    await sut.toggle(true);
    await sut.resize();
    assert.equal(sut.calls.length, calls);
});