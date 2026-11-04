const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../../../../src/Presentation.Web/Dashboard/Pages/_DashboardLayout.cshtml'), 'utf8').replace(/\r\n/g, '\n');
const handler = source.match(/document.addEventListener\('click', async event => \{\n            const button = event.target.closest\('\[data-dashboard-copy\]'\);[\s\S]*?\n        \}\);/)[0];

function fixture(label, fails = false) {
    let click, reset, copied;
    const attributes = { 'aria-label': label };
    const icon = { className: 'bi bi-copy' };
    const button = {
        dataset: { dashboardCopy: '019a4e77-1111-7777-9999-123456789abc' },
        title: label,
        getAttribute: name => attributes[name],
        setAttribute: (name, value) => { attributes[name] = value; },
        querySelector: () => icon,
        focus() {}
    };
    const document = {
        addEventListener: (_, listener) => { click = listener; },
        createElement: () => ({ style: {}, select() {}, remove() {} }),
        body: { append() {} },
        execCommand: () => false
    };
    vm.runInNewContext(handler, {
        document,
        navigator: { clipboard: { writeText: async value => { if (fails) throw new Error('denied'); copied = value; } } },
        clearTimeout() {},
        setTimeout: callback => { reset = callback; return 1; }
    });
    return { button, attributes, icon, click: () => click({ target: { closest: () => button } }), reset: () => reset(), copied: () => copied };
}

for (const label of ['Copy correlation ID', 'Copy occurrence ID']) {
    test(label + ' copies the exact value and restores its own accessible label', async () => {
        const sut = fixture(label);
        await sut.click();
        assert.equal(sut.copied(), sut.button.dataset.dashboardCopy);
        assert.equal(sut.button.title, 'Copied');
        assert.equal(sut.attributes['aria-label'], label + ': copied');
        // A second click during feedback must not replace the original labels.
        await sut.click();
        sut.reset();
        assert.equal(sut.button.title, label);
        assert.equal(sut.attributes['aria-label'], label);
        assert.equal(sut.icon.className, 'bi bi-copy');
    });
}

test('failed copy reports failure and restores the occurrence label', async () => {
    const sut = fixture('Copy occurrence ID', true);
    await sut.click();
    assert.equal(sut.button.title, 'Copy failed');
    assert.equal(sut.attributes['aria-label'], 'Copy occurrence ID: failed');
    sut.reset();
    assert.equal(sut.button.title, 'Copy occurrence ID');
});
