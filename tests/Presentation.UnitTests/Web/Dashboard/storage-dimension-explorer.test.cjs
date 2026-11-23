const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../../../../src/Presentation.Web.Storage/Dashboard/Pages/DimensionExplorerScript.cshtml'), 'utf8');
const script = source.match(/<script[^>]*>([\s\S]*?)<\/script>/)[1];

test('rejected storage queries reuse one alert and leave the filters and results in place', () => {
    const filters = { textContent: 'Filter draft' };
    const results = { textContent: 'Previous results' };
    const children = [filters, results];
    const root = {
        querySelector: selector => children.find(child => child.selector === selector),
        prepend: element => children.unshift(element)
    };
    const window = { bdkDashboard: {} };
    vm.runInNewContext(script, { window });
    const explorer = window.bdkDashboard.createDimensionExplorer({ owner: { addEventListener() {} }, root: () => root });

    function alert(textContent) {
        return {
            textContent,
            selector: '[data-dashboard-query-error]',
            cloneNode: () => alert(textContent),
            replaceWith(replacement) { children.splice(children.indexOf(this), 1, replacement); }
        };
    }

    explorer.showQueryError(alert('Full scans require approval.'));
    assert.deepEqual(children.map(child => child.textContent), ['Full scans require approval.', filters.textContent, results.textContent]);

    explorer.showQueryError(alert('The selected comparison is unavailable.'));
    assert.deepEqual(children.map(child => child.textContent), ['The selected comparison is unavailable.', filters.textContent, results.textContent]);
    assert.equal(children[1], filters);
    assert.equal(children[2], results);
});
