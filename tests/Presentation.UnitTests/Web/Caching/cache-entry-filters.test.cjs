const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../../../../src/Presentation.Web.Caching/Dashboard/Pages/Script.cshtml'), 'utf8');
const script = source.match(/<script>([\s\S]*?)<\/script>/)[1];

function fixture({ schemas = [{ name: 'REGION', kind: 'String', form: 'Scalar' }], selected = '', operator = '', value = '', upper = '', entries = true, operations = false } = {}) {
    const listeners = new Map();
    const elements = new Map();
    const state = { refreshes: 0, cancelledRefresh: 0, cancelledGrowing: 0, previousDisposed: false };
    let refresh;
    function element(id, properties = {}) {
        const result = {
            id, value: '', disabled: false, hidden: false, textContent: '', dataset: {},
            addEventListener: (name, callback) => listeners.set(id + ':' + name, callback),
            removeEventListener: (name, callback) => {
                if (listeners.get(id + ':' + name) === callback) listeners.delete(id + ':' + name);
            },
            ...properties
        };
        elements.set(id, result);
        return result;
    }
    function select(id, options, initial) {
        const result = element(id, { children: options });
        let selectedValue = initial;
        Object.defineProperties(result, {
            value: {
                get: () => result.children.some(option => option.value === selectedValue) ? selectedValue : result.children[0]?.value || '',
                set: value => { selectedValue = result.children.some(option => option.value === value) ? value : ''; }
            },
            selectedOptions: { get: () => result.children.filter(option => option.value === result.value) }
        });
        result.replaceChildren = () => { result.children = []; selectedValue = ''; };
        result.append = option => result.children.push(option);
        return result;
    }
    const workspace = element('application-caching-workspace', {
        dataset: { pagePath: '/ops/caching/collections/items/entries', contentPath: '/ops/caching/collections/items/entries/content' },
        querySelector: () => null
    });
    element('cache-content');
    element('cache-filter-reset');
    if (entries) {
        select('cache-dimension', [
            { value: '', dataset: {} },
            ...schemas.map(schema => ({ value: schema.name, dataset: { kind: schema.kind, form: schema.form, null: schema.nullable ? 'true' : 'false' } }))
        ], selected).name = 'dimension';
        select('cache-dimension-operator', ['equal', 'contains', 'overlap', 'present', 'absent', 'null', 'contains-null', ''].map(value => ({ value })), operator).name = 'dimensionOperator';
        element('cache-dimension-value', { name: 'dimensionValue', value });
        element('cache-dimension-upper', { name: 'dimensionUpper', value: upper });
        for (const id of ['cache-dimension-value-field', 'cache-dimension-upper-field', 'cache-dimension-value-label', 'cache-dimension-upper-label']) element(id);
    }
    if (operations) {
        select('cache-selection-kind', [{ value: 'prefix' }, { value: 'key' }], 'prefix');
        element('cache-selection', { placeholder: 'All entries when empty' });
    }
    const window = {
        bdkCachingView: { dispose: () => { state.previousDisposed = true; } },
        addEventListener: (name, callback) => listeners.set('window:' + name, callback),
        bdkDashboard: {
            createGrowingLists: () => ({ busy: () => false, applyResponse() {}, cancel: () => { state.cancelledGrowing++; } }),
            createRefresher: options => {
                refresh = options;
                return { refresh: () => { state.refreshes++; }, cancel: () => { state.cancelledRefresh++; } };
            }
        }
    };
    vm.runInNewContext(script, {
        window,
        document: {
            readyState: 'complete',
            getElementById: id => elements.get(id) || null,
            createElement: tag => ({ tagName: tag, value: '', textContent: '' }),
            addEventListener: (name, callback) => listeners.set('document:' + name, callback),
            removeEventListener: (name, callback) => {
                if (listeners.get('document:' + name) === callback) listeners.delete('document:' + name);
            }
        },
        location: { href: 'https://dashboard.test/ops/caching/collections/items/entries', origin: 'https://dashboard.test', assign() {} },
        history: { replaceState() {} },
        URL
    });
    return {
        elements, listeners, state, workspace,
        choose: name => { elements.get('cache-dimension').value = name; listeners.get('cache-dimension:change')?.(); },
        condition: value => { elements.get('cache-dimension-operator').value = value; listeners.get('cache-dimension-operator:change')?.(); },
        operatorValues: () => elements.get('cache-dimension-operator').children.map(option => option.value),
        submitted: () => Object.fromEntries([...elements.values()].filter(element => element.name && !element.disabled).map(element => [element.name, element.value])),
        selection: value => { elements.get('cache-selection-kind').value = value; listeners.get('cache-selection-kind:change')?.(); },
        dispose: () => window.bdkCachingView.dispose(),
        shouldRefresh: () => refresh.shouldRefresh()
    };
}

test('no selected dimension excludes stale condition and operands from form submission', () => {
    const sut = fixture({ value: 'old value', upper: 'old upper', operator: 'overlap' });

    assert.deepEqual(sut.submitted(), { dimension: '' });
    assert.equal(sut.elements.get('cache-dimension-value-field').hidden, true);
    assert.equal(sut.elements.get('cache-dimension-upper-field').hidden, true);
    assert.equal(sut.elements.get('cache-dimension-operator').disabled, true);
    assert.equal(sut.state.refreshes, 0);
});

for (const [form, expected] of [
    ['Scalar', ['equal', 'present', 'absent']],
    ['Set', ['contains', 'present', 'absent']],
    ['Interval', ['overlap', 'present', 'absent']]
]) {
    test(`${form} dimensions offer only matching shape operators and operand fields`, () => {
        const sut = fixture({ schemas: [{ name: 'FILTER', kind: 'Integer', form }], selected: 'FILTER' });

        assert.deepEqual(sut.operatorValues(), expected);
        assert.equal(sut.elements.get('cache-dimension-value').disabled, false);
        assert.equal(sut.elements.get('cache-dimension-upper').disabled, form !== 'Interval');
        assert.equal(sut.elements.get('cache-dimension-upper-field').hidden, form !== 'Interval');
    });
}

for (const [form, nullOperator] of [['Scalar', 'null'], ['Set', 'contains-null']]) {
    test(`nullable ${form} dimensions expose ${nullOperator} without accepting value operands`, () => {
        const sut = fixture({ schemas: [{ name: 'FILTER', kind: 'String', form, nullable: true }], selected: 'FILTER', value: 'retained input' });
        assert.ok(sut.operatorValues().includes(nullOperator));

        sut.condition(nullOperator);

        assert.deepEqual(sut.submitted(), { dimension: 'FILTER', dimensionOperator: nullOperator });
        assert.equal(sut.elements.get('cache-dimension-value-field').hidden, true);
        assert.equal(sut.elements.get('cache-dimension-upper-field').hidden, true);
    });
}

test('intervals never offer a null operator even if a schema carries the nullable flag', () => {
    const sut = fixture({ schemas: [{ name: 'STOCK', kind: 'Integer', form: 'Interval', nullable: true }], selected: 'STOCK' });
    assert.deepEqual(sut.operatorValues(), ['overlap', 'present', 'absent']);
});

test('presence and missing conditions omit operands and restore entered values when returning to overlap', () => {
    const sut = fixture({ schemas: [{ name: 'STOCK', kind: 'Integer', form: 'Interval' }], selected: 'STOCK', operator: 'overlap', value: '10', upper: '20' });
    for (const condition of ['present', 'absent']) {
        sut.condition(condition);
        assert.deepEqual(sut.submitted(), { dimension: 'STOCK', dimensionOperator: condition });
    }

    sut.condition('overlap');

    assert.deepEqual(sut.submitted(), { dimension: 'STOCK', dimensionOperator: 'overlap', dimensionValue: '10', dimensionUpper: '20' });
});

test('changing schemas clears stale operands and selects a compatible operator', () => {
    const sut = fixture({ schemas: [
        { name: 'REGION', kind: 'String', form: 'Scalar' },
        { name: 'STOCK', kind: 'Integer', form: 'Interval' },
        { name: 'LABELS', kind: 'String', form: 'Set' }
    ], selected: 'REGION', operator: 'equal', value: 'WEST' });

    sut.choose('STOCK');
    assert.deepEqual(sut.submitted(), { dimension: 'STOCK', dimensionOperator: 'overlap', dimensionValue: '', dimensionUpper: '' });
    sut.elements.get('cache-dimension-value').value = '10';
    sut.elements.get('cache-dimension-upper').value = '20';
    sut.choose('LABELS');
    assert.deepEqual(sut.submitted(), { dimension: 'LABELS', dimensionOperator: 'contains', dimensionValue: '' });
    sut.choose('');
    assert.deepEqual(sut.submitted(), { dimension: '' });
});

test('initial route-supplied condition and operands survive control initialization', () => {
    const sut = fixture({ schemas: [{ name: 'WHEN', kind: 'Timestamp', form: 'Interval' }], selected: 'WHEN', operator: 'overlap',
        value: '2026-11-11T12:00:00Z', upper: '2026-11-12T12:00:00Z' });

    assert.deepEqual(sut.submitted(), { dimension: 'WHEN', dimensionOperator: 'overlap', dimensionValue: '2026-11-11T12:00:00Z', dimensionUpper: '2026-11-12T12:00:00Z' });
    assert.equal(sut.elements.get('cache-dimension-value-label').textContent, 'Lower bound (UTC)');
    assert.equal(sut.elements.get('cache-dimension-upper-label').textContent, 'Upper bound (UTC, exclusive)');
});

for (const [kind, placeholder, inputMode] of [
    ['String', 'Dimension value', 'text'], ['Boolean', 'true or false', 'text'],
    ['Integer', 'Whole number', 'numeric'], ['Identifier', 'GUID', 'text'],
    ['Timestamp', '2026-11-11T12:00:00Z', 'text']
]) {
    test(`${kind} values have an appropriate entry hint without converting their text`, () => {
        const sut = fixture({ schemas: [{ name: 'VALUE', kind, form: 'Scalar' }], selected: 'VALUE', value: '9223372036854775807' });
        const input = sut.elements.get('cache-dimension-value');

        assert.equal(input.placeholder, placeholder);
        assert.equal(input.inputMode, inputMode);
        assert.equal(input.value, '9223372036854775807');
        assert.equal(sut.elements.get('cache-dimension-value-label').textContent, kind === 'Timestamp' ? 'Value (UTC)' : 'Value');
    });
}

test('operation selection hints distinguish an empty prefix from an exact key', () => {
    const sut = fixture({ entries: false, operations: true });
    assert.equal(sut.elements.get('cache-selection').placeholder, 'All entries when empty');

    sut.selection('key');
    assert.equal(sut.elements.get('cache-selection').placeholder, 'Enter an exact key');
    sut.selection('prefix');
    assert.equal(sut.elements.get('cache-selection').placeholder, 'All entries when empty');
});

test('pages without either filter control still initialize their existing refresher', () => {
    const sut = fixture({ entries: false });
    assert.equal(sut.shouldRefresh(), true);
    assert.equal(sut.state.previousDisposed, true);
});

test('disposal removes filter and selection handlers and cancels refresh and growing lists', () => {
    const sut = fixture({ selected: 'REGION', operations: true });
    assert.equal(sut.listeners.has('cache-dimension:change'), true);
    assert.equal(sut.listeners.has('cache-dimension-operator:change'), true);
    assert.equal(sut.listeners.has('cache-selection-kind:change'), true);

    sut.dispose();

    assert.equal(sut.listeners.has('cache-dimension:change'), false);
    assert.equal(sut.listeners.has('cache-dimension-operator:change'), false);
    assert.equal(sut.listeners.has('cache-selection-kind:change'), false);
    assert.equal(sut.state.cancelledRefresh, 1);
    assert.equal(sut.state.cancelledGrowing, 1);
    assert.equal(sut.shouldRefresh(), false);
});