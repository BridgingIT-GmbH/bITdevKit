const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../../../../src/Presentation.Web/Dashboard/Pages/StateScript.cshtml'), 'utf8');
const script = source.match(/<script[^>]*>([\s\S]*?)<\/script>/)[1];

function storage(entries = {}) {
    const values = new Map(Object.entries(entries));
    return {
        get length() { return values.size; },
        key: index => [...values.keys()][index] ?? null,
        getItem: key => values.get(key) ?? null,
        setItem: (key, value) => values.set(key, String(value)),
        removeItem: key => values.delete(key)
    };
}

function fixture({ local = storage(), session = storage(), accepted = true } = {}) {
    let click, destination;
    const messages = [];
    const window = {
        localStorage: local, sessionStorage: session,
        location: { assign: value => { destination = value; } },
        bdkDashboard: {
            confirmAction: async () => accepted,
            showMessage: async value => messages.push(value)
        }
    };
    vm.runInNewContext(script, {
        window,
        document: {
            currentScript: { dataset: { dashboardRoot: '/tenant/ops' } },
            addEventListener: (_, listener) => { click = listener; }
        }
    });
    const button = { disabled: false };
    return { state: window.bdkDashboard.state, local, session, messages, button,
        click: () => click({ target: { closest: () => button } }), destination: () => destination };
}

test('existing string and JSON preferences retain their keys and formats', () => {
    const sut = fixture({ local: storage({ theme: 'white', 'bdk.dashboard.test': '{"size":20}' }) });
    assert.equal(sut.state.get('theme'), 'white');
    assert.equal(sut.state.getJson('bdk.dashboard.test').size, 20);
    for (const value of ['', 0, false]) {
        sut.state.set('bdk.dashboard.raw', value);
        assert.equal(sut.local.getItem('bdk.dashboard.raw'), String(value));
        sut.state.setJson('bdk.dashboard.json', value);
        assert.equal(sut.state.getJson('bdk.dashboard.json'), value);
    }
    sut.state.remove('theme');
    assert.equal(sut.state.get('theme'), null);
});

test('missing, corrupt and invalid preferences return caller defaults', () => {
    const sut = fixture({ local: storage({ corrupt: '{', interval: '999', shape: '[]' }) });
    assert.equal(sut.state.get('missing', 'default'), 'default');
    assert.equal(sut.state.getJson('corrupt', 42), 42);
    assert.equal(sut.state.get('interval', '5000', value => ['0', '5000'].includes(value)), '5000');
    assert.equal(sut.state.getJson('shape', 42, value => !Array.isArray(value)), 42);
});

test('blocked storage and quota failures retain changes in memory', () => {
    const blocked = new Proxy({}, { get() { throw new Error('Storage denied'); } });
    const sut = fixture({ local: blocked, session: blocked });
    for (const store of [sut.state, sut.state.session]) {
        assert.equal(store.get('missing', 'fallback'), 'fallback');
        store.set('bdk.dashboard.test', 'saved');
        assert.equal(store.get('bdk.dashboard.test'), 'saved');
        store.setJson('bdk.dashboard.json', { size: 20 });
        assert.equal(store.getJson('bdk.dashboard.json').size, 20);
        store.remove('bdk.dashboard.test');
        assert.equal(store.get('bdk.dashboard.test'), null);
    }
});

test('new preference keys include dashboard root, feature and optional route', () => {
    const sut = fixture();
    assert.equal(sut.state.key('orders', 'columns', '/recent'), 'bdk.dashboard.orders.columns:/tenant/ops:/recent');
    assert.notEqual(sut.state.key('orders', 'columns', '/recent'), sut.state.key('orders', 'columns', '/archived'));
});

test('reset clears all dashboard families in both stores and retains unrelated application data', () => {
    const owned = ['theme', 'dashboardTexture', 'sidebarCollapsed', 'dashboardSidebarGroups',
        'bdk.dashboard.jobs.rowsPerLoad', 'bdk.dashboard.history:/ops/jobs', 'bdk.profiling.filters:/ops',
        'bdk.documentStorage.clientId', 'bdk.blobStorage.prefixHistory',
        'bdk.storage.diagnostics.load-input:/ops:test', 'bdk:storage:throughput-test:/ops',
        'bdk.custom-feature.view:/ops/custom'];
    const values = Object.fromEntries(owned.map(key => [key, 'saved']));
    const unrelated = { 'app.token': 'secret', 'bdk.otherFeature': 'keep', 'AppState_user': 'keep' };
    const sut = fixture({ local: storage({ ...values, ...unrelated }), session: storage({ ...values, ...unrelated }) });
    sut.state.set(sut.state.key('custom', 'panel'), 'true');
    assert.equal(sut.state.reset(), true);
    for (const store of [sut.local, sut.session]) {
        for (const key of owned) assert.equal(store.getItem(key), null, key);
        for (const [key, value] of Object.entries(unrelated)) assert.equal(store.getItem(key), value, key);
    }
    assert.equal(sut.state.get(sut.state.key('custom', 'panel')), null);
    // Pagehide and toggle listeners must not recreate discarded state before navigation.
    sut.state.set('theme', 'white');
    sut.state.session.setJson('bdk.dashboard.history:/ops/jobs', { count: 10 });
    assert.equal(sut.local.getItem('theme'), null);
    assert.equal(sut.session.getItem('bdk.dashboard.history:/ops/jobs'), null);
});

test('reset button cancellation retains preferences and current location', async () => {
    const sut = fixture({ local: storage({ theme: 'white' }), accepted: false });
    await sut.click();
    assert.equal(sut.local.getItem('theme'), 'white');
    assert.equal(sut.destination(), undefined);
    assert.equal(sut.button.disabled, false);
});

test('confirmed reset returns to the configured dashboard root without current filters', async () => {
    const sut = fixture({ local: storage({ theme: 'white' }) });
    await sut.click();
    assert.equal(sut.destination(), '/tenant/ops');
    assert.equal(sut.local.getItem('theme'), null);
});

test('partial reset failure clears the other store and reports failure without navigating', async () => {
    const local = storage({ theme: 'white' });
    local.removeItem = () => { throw new Error('Storage denied'); };
    const sut = fixture({ local, session: storage({ 'bdk.dashboard.console.sessionId': 'session' }) });
    await sut.click();
    assert.equal(sut.destination(), undefined);
    assert.equal(sut.messages[0].title, 'Reset incomplete');
    assert.equal(sut.session.getItem('bdk.dashboard.console.sessionId'), null);
    assert.equal(sut.button.disabled, false);
    sut.state.set('bdk.dashboard.test', 'usable');
    assert.equal(sut.state.get('bdk.dashboard.test'), 'usable');
});

test('dashboard consumers access browser storage through the shared helper', () => {
    const root = path.resolve(__dirname, '../../../../src');
    const files = fs.readdirSync(root, { recursive: true }).filter(file =>
        file.endsWith('.cshtml') && (file.includes('Dashboard') || file.endsWith(path.join('Pages', 'Diagnostics.cshtml'))) &&
        file !== path.join('Presentation.Web', 'Dashboard', 'Pages', 'StateScript.cshtml'));
    assert.ok(files.length > 100);
    for (const file of files) {
        assert.doesNotMatch(fs.readFileSync(path.join(root, file), 'utf8'), /\b(?:localStorage|sessionStorage)\s*\./, file);
    }
});
