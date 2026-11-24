const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../../../../src/Presentation.Web.Storage/Dashboard/Pages/DimensionFilterNavigationScript.cshtml'), 'utf8');
const script = source.match(/<script[^>]*>([\s\S]*?)<\/script>/)[1];
const endpoint = 'https://dashboard.test/ops/storage/blobs';
const filter = JSON.stringify({ name: 'Region', operator: 'Equal', operand: { type: 'string', value: 'EU' } });
const draft = JSON.stringify({ all: [JSON.parse(filter), { name: 'Year', operator: 'Equal', operand: { type: 'integer', value: '' } }] });
const applied = { store: 'reports', container: 'weather', prefix: 'demo/', dimensionFilter: filter, take: '25' };
const scoped = endpoint + '?' + new URLSearchParams(applied);

function fixture({
    cache = new Map(), url = scoped, query = applied, appliedValid = true, text = query.dimensionFilter || '',
    open = true, initialized = true, navigationType = 'navigate', pathRoot = '/ops', stores = ['reports', 'archive']
} = {}) {
    const input = { value: text };
    const disclosure = { open };
    const editor = {
        dataset: { supported: 'true', initialized: String(initialized) },
        querySelector: selector => selector === 'details' ? disclosure : input
    };
    const owner = {
        dataset: { dimensionFilter: query.dimensionFilter, dimensionFilterValid: String(appliedValid) },
        querySelector: selector => selector === '[data-storage-dimension-editor]' ? editor : {
            name: 'store', options: stores.map(value => ({ value }))
        }
    };
    const location = new URL(url);
    let destination;
    location.replace = href => { destination = new URL(href); };
    let onLeave;
    const window = {
        performance: { getEntriesByType: () => [{ type: navigationType }] },
        addEventListener: (name, callback) => { if (name === 'pagehide') onLeave = callback; },
        bdkDashboard: {
            state: {
                key: (feature, preference, route) => [feature, preference, pathRoot, route].join(':'),
                session: {
                    getJson: (key, fallback) => cache.has(key) ? JSON.parse(cache.get(key)) : fallback,
                    setJson: (key, value) => cache.set(key, JSON.stringify(value)),
                    remove: key => cache.delete(key)
                }
            }
        }
    };
    vm.runInNewContext(script, { window, location, URL, URLSearchParams });
    const navigation = window.bdkDashboard.createDimensionFilterNavigation({
        root: () => owner,
        buildQuery: () => new URL(endpoint + '/content?' + new URLSearchParams(query))
    });
    return { cache, input, disclosure, navigation, leave: () => onLeave(), destination: () => destination };
}

test('returning from a tab restores unfinished values and disclosure without replacing the applied search', () => {
    const prior = fixture({ text: draft, open: false, query: { ...applied, continuationToken: 'next', selectedRowKey: 'old' } });
    prior.leave();
    const returning = fixture({ cache: prior.cache, initialized: false });

    assert.equal(returning.navigation.resume(), false);
    assert.equal(returning.navigation.restoreDraft(), true);
    assert.equal(returning.input.value, draft);
    assert.equal(returning.disclosure.open, false);
});

test('a bare visit resumes the last active search within the current dashboard root', () => {
    const prior = fixture({ text: draft });
    prior.leave();
    const returning = fixture({ cache: prior.cache, url: endpoint + '?dashboardView=window', query: { store: 'archive' } });

    assert.equal(returning.navigation.resume(), true);
    assert.equal(returning.destination().pathname, new URL(endpoint).pathname);
    assert.equal(returning.destination().searchParams.get('dimensionFilter'), filter);
    assert.equal(returning.destination().searchParams.get('container'), 'weather');
    assert.equal(returning.destination().searchParams.get('dashboardView'), 'window');
    returning.leave();
    assert.equal(fixture({ cache: prior.cache, initialized: false }).navigation.restoreDraft(), true);
    assert.equal(fixture({ cache: prior.cache, pathRoot: '/other', url: endpoint, query: { store: 'archive' } }).navigation.resume(), false);
    assert.equal(fixture({ cache: prior.cache, stores: ['archive'], url: endpoint, query: { store: 'archive' } }).navigation.resume(), false);
});

test('explicit URL filters and browser history keep their own searches', () => {
    const prior = fixture({ text: draft });
    prior.leave();
    const explicit = fixture({ cache: prior.cache, url: endpoint + '?store=archive', query: { store: 'archive' }, initialized: false });
    assert.equal(explicit.navigation.resume(), false);
    assert.equal(explicit.navigation.restoreDraft(), false);

    const back = fixture({ cache: prior.cache, url: endpoint, query: { store: 'archive' }, navigationType: 'back_forward' });
    assert.equal(back.navigation.resume(), false);
    const historicalSearch = fixture({ cache: prior.cache, initialized: false, navigationType: 'back_forward' });
    assert.equal(historicalSearch.navigation.restoreDraft(), true);
    assert.equal(historicalSearch.input.value, draft);
});

test('drafts from two searches stay separate, including an empty draft for an active filter', () => {
    const first = fixture({ text: '' });
    first.leave();
    const second = fixture({ cache: first.cache, query: { ...applied, container: 'archive' }, text: draft });
    second.leave();

    const returning = fixture({ cache: first.cache, initialized: false });
    assert.equal(returning.navigation.restoreDraft(), true);
    assert.equal(returning.input.value, '');
    const archive = fixture({ cache: first.cache, query: { ...applied, container: 'archive' }, initialized: false });
    assert.equal(archive.navigation.restoreDraft(), true);
    assert.equal(archive.input.value, draft);
});

test('a cleared search stops bare navigation from restoring an older filter', () => {
    const first = fixture({ text: draft });
    first.leave();
    const cleared = fixture({ cache: first.cache, query: { store: 'archive' }, text: '' });
    cleared.leave();
    const returning = fixture({ cache: first.cache, url: endpoint, query: { store: 'archive' } });
    assert.equal(returning.navigation.resume(), false);
});

test('reset suppresses a pending pagehide write from the cleared editor', () => {
    const sut = fixture({ text: draft });
    sut.leave();
    sut.navigation.reset();
    sut.leave();

    const returning = fixture({ cache: sut.cache, initialized: false });
    assert.equal(returning.navigation.restoreDraft(), false);
    assert.equal(fixture({ cache: sut.cache, url: endpoint, query: { store: 'archive' } }).navigation.resume(), false);
});
