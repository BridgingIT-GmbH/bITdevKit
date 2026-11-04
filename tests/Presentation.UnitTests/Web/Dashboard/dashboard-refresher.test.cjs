// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Run: node --test tests/Presentation.UnitTests/Web/Dashboard/dashboard-refresher.test.cjs
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../../../../src/Presentation.Web/Dashboard/Pages/_DashboardLayout.cshtml'), 'utf8');
const factory = source.match(/window\.bdkDashboard\.createRefresher = (function\(options\) \{[\s\S]*?\n        \});/)[1];

function fixture({ type = 'text/html; charset=utf-8', body = '<p>fresh</p>', status = 200, redirected = false, responseType, applyResponse } = {}) {
    const content = { innerHTML: '<p>previous</p>', querySelector: () => null, querySelectorAll: () => [] };
    const label = { textContent: 'previous status', classList: { toggle() {} } };
    const errors = [];
    let stale = false;
    let accepted = 0;
    const document = {
        hidden: false,
        addEventListener() {},
        createElement() {
            let html = '';
            return {
                set innerHTML(value) { html = value; },
                content: { querySelector: () => html.includes('data-dashboard-query-error') ? {} : null }
            };
        }
    };
    const window = {
        location: { origin: 'https://example.test' },
        scrollX: 0, scrollY: 0, scrollTo() {}, setTimeout, clearTimeout,
        bdkDashboard: {
            resolveElement: value => value || null,
            showRefreshError: () => { stale = true; },
            clearRefreshError: () => { stale = false; }
        }
    };
    const createRefresher = vm.runInNewContext('(' + factory + ')', {
        window, document, URL, AbortController,
        fetch: async () => ({ ok: status >= 200 && status < 300, status, redirected, headers: { get: () => type }, text: async () => body })
    });
    const refresher = createRefresher({
        contentElement: content, statusElement: label, contentUrl: '/ops/data', responseType, applyResponse,
        onSuccess: () => { accepted++; }, onError: error => errors.push(error)
    });
    return { refresh: () => refresher.refresh(false), content, label, errors, stale: () => stale, accepted: () => accepted };
}

test('JSON diagnostics responses reach their renderer and publish a successful update', async () => {
    let snapshot;
    const sut = fixture({ responseType: 'json', type: 'application/json; charset=utf-8', body: '{"samples":[{"readItems":3}],"load":{"phase":"Completed"}}', applyResponse: (_, text) => { snapshot = JSON.parse(text); } });
    await sut.refresh();
    assert.equal(snapshot?.samples[0].readItems, 3);
    assert.equal(snapshot.load.phase, 'Completed');
    assert.equal(sut.accepted(), 1);
    assert.equal(sut.stale(), false);
    assert.match(sut.label.textContent, /^Updated .* UTC$/);
});

test('HTML remains the default response and replaces a successful fragment', async () => {
    const sut = fixture();
    await sut.refresh();
    assert.equal(sut.content.innerHTML, '<p>fresh</p>');
    assert.equal(sut.accepted(), 1);
    assert.equal(sut.stale(), false);
});

test('HTML fragment consumers reject JSON and retain their previous content', async () => {
    const sut = fixture({ type: 'application/json', body: '{}' });
    await sut.refresh();
    assert.equal(sut.content.innerHTML, '<p>previous</p>');
    assert.equal(sut.accepted(), 0);
    assert.equal(sut.stale(), true);
});

test('JSON consumers reject HTML before invoking their renderer', async () => {
    let calls = 0;
    const sut = fixture({ responseType: 'json', applyResponse: () => { calls++; } });
    await sut.refresh();
    assert.equal(calls, 0);
    assert.equal(sut.accepted(), 0);
    assert.equal(sut.stale(), true);
});

test('HTML query errors retain previous results even with HTTP 200', async () => {
    const sut = fixture({ body: '<div data-dashboard-query-error>Invalid query</div>' });
    await sut.refresh();
    assert.equal(sut.content.innerHTML, '<p>previous</p>');
    assert.equal(sut.accepted(), 0);
    assert.equal(sut.stale(), true);
});

test('JSON is not interpreted as an HTML fragment', async () => {
    let snapshot;
    const sut = fixture({ responseType: 'json', type: 'application/json', body: '{"label":"<div data-dashboard-query-error>"}', applyResponse: (_, text) => { snapshot = JSON.parse(text); } });
    await sut.refresh();
    assert.equal(snapshot?.label, '<div data-dashboard-query-error>');
    assert.equal(sut.accepted(), 1);
});

test('malformed JSON keeps the prior chart state and reports stale data', async () => {
    let snapshot = { samples: [1] };
    const sut = fixture({ responseType: 'json', type: 'application/json', body: '{broken', applyResponse: (_, text) => { snapshot = JSON.parse(text); } });
    await sut.refresh();
    assert.deepEqual(snapshot, { samples: [1] });
    assert.equal(sut.accepted(), 0);
    assert.equal(sut.stale(), true);
});

for (const failure of [{ redirected: true }, { status: 503 }]) {
    test('unsuccessful transport retains HTML results: ' + JSON.stringify(failure), async () => {
        const sut = fixture(failure);
        await sut.refresh();
        assert.equal(sut.content.innerHTML, '<p>previous</p>');
        assert.equal(sut.accepted(), 0);
        assert.equal(sut.stale(), true);
    });
}
