const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const documentSource = fs.readFileSync(path.resolve(__dirname, '../../../../src/Presentation.Web.Storage/Documents/Dashboard/Pages/Index.cshtml'), 'utf8');
const documentStart = documentSource.indexOf('        function showConflictActions()');
const documentEnd = documentSource.indexOf('        function setPageUrl(state)', documentStart);
assert.ok(documentStart >= 0 && documentEnd > documentStart);

function documentFixture(firstFailure, initialValid = true) {
    let actions, valid = initialValid;
    const sent = [];
    const form = {
        elements: { mode: { value: 'edit' }, etag: { value: 'original-version' }, content: { value: '{"name":"retained draft"}' } },
        dataset: {},
        querySelector: selector => selector === '.modal-body' ? { prepend: value => { actions = value; } } : null
    };
    const save = { dataset: { documentsAction: '/actions/save' }, disabled: false };
    const context = {
        detailsForm: form, detailsSaveButton: save,
        detailsPartitionKey: { value: 'records' }, detailsRowKey: { value: '42' }, detailsContent: { focus() {} },
        document: { createElement: () => ({ dataset: {}, append(...children) { this.children = children; } }) },
        window: { bdkDashboard: { confirmAction: async () => true } },
        clearDetailsAlert() {}, validateDetailsForm: () => valid, setBusy() {}, showDetailsAlert() {},
        readResponseMessage: async () => 'rejected',
        FormData: class extends Map {
            constructor(value) { super(Object.entries(value.elements).map(([key, field]) => [key, field.value])); }
        },
        fetch: async (_url, options) => {
            sent.push(Object.fromEntries(options.body));
            if (sent.length === 1 && firstFailure === 'network') throw new Error('network failure');
            return { ok: false, status: sent.length === 1 ? firstFailure : 409 };
        }
    };
    vm.createContext(context);
    vm.runInContext(documentSource.slice(documentStart, documentEnd), context);
    context.showConflictActions();
    return {
        sent, form, save,
        force: () => actions.children[1].onclick(),
        normalSave: () => context.submitDocumentAction(form, save),
        delete: () => context.submitDocumentAction(form, { dataset: { documentsAction: '/actions/delete' } }, true),
        setValid: value => { valid = value; }
    };
}

for (const status of [400, 409, 413, 500, 'network']) {
    test(`document force overwrite is one submission after ${status} failure`, async () => {
        const sut = documentFixture(status);
        if (status === 'network') await assert.rejects(sut.force(), /network failure/);
        else await sut.force();
        await sut.normalSave();
        assert.deepEqual(sut.sent.map(item => item.forceOverwrite), ['true', 'false']);
        assert.deepEqual(sut.sent.map(item => item.etag), ['original-version', 'original-version']);
        assert.deepEqual(sut.sent.map(item => item.content), ['{"name":"retained draft"}', '{"name":"retained draft"}']);
        assert.equal(sut.save.disabled, false);
    });
}

test('document validation failure does not arm a later ordinary save', async () => {
    const sut = documentFixture(400, false);
    await sut.force();
    assert.equal(sut.sent.length, 0);
    sut.setValid(true);
    await sut.normalSave();
    assert.equal(sut.sent[0].forceOverwrite, 'false');
    assert.equal(sut.sent[0].etag, 'original-version');
});

test('document deletes never inherit a force-overwrite condition', async () => {
    const sut = documentFixture(400);
    await sut.delete();
    assert.equal(sut.sent[0].forceOverwrite, 'false');
    assert.equal(sut.sent[0].etag, 'original-version');
});

const blobSource = fs.readFileSync(path.resolve(__dirname, '../../../../src/Presentation.Web.Storage/Blobs/Dashboard/Pages/Index.cshtml'), 'utf8');
const uploadStart = blobSource.indexOf("            if (command === 'upload') {");
const replaceStart = blobSource.indexOf("            if (command === 'replace') {", uploadStart);
const replaceEnd = blobSource.indexOf("            if (command === 'delete') {", replaceStart);
assert.ok(uploadStart >= 0 && replaceStart > uploadStart && replaceEnd > replaceStart);

function blobFixture() {
    const elements = Object.fromEntries(['container', 'prefix', 'name', 'mode', 'etag', 'file', 'expiresAt', 'forceOverwrite'].map(key => [key, { value: '', disabled: false, checked: false }]));
    const note = { hidden: true };
    const form = {
        elements,
        reset() { for (const input of Object.values(elements)) { input.value = ''; input.checked = false; } },
        querySelector(selector) {
            if (selector === '[name="name"]') return elements.name;
            if (selector === '[data-blobs-replace-metadata-note]') return note;
            return null;
        }
    };
    const context = {
        target: { dataset: { container: 'records', name: '42.txt', etag: 'original-version', expiresAt: '2030-01-02T03:04:05Z' } },
        showModal: () => ({ querySelector: () => form }), setContextFields() {},
        toDateTimeLocal: value => value?.slice(0, 16) || ''
    };
    vm.createContext(context);
    return {
        elements, note,
        replace() { context.command = 'replace'; vm.runInContext(`(() => { ${blobSource.slice(replaceStart, replaceEnd)} })()`, context); },
        create() { context.command = 'upload'; vm.runInContext(`(() => { ${blobSource.slice(uploadStart, replaceStart)} })()`, context); }
    };
}

test('blob replacement displays retained expiration without allowing an accidental expiration edit', () => {
    const sut = blobFixture();
    sut.replace();
    assert.equal(sut.elements.expiresAt.value, '2030-01-02T03:04');
    assert.equal(sut.elements.expiresAt.disabled, true);
    assert.equal(sut.note.hidden, false);
    assert.equal(sut.elements.etag.value, 'original-version');
    assert.equal(sut.elements.name.readOnly, true);
});

test('new blob dialog re-enables expiration after a replacement dialog', () => {
    const sut = blobFixture();
    sut.replace();
    sut.create();
    assert.equal(sut.elements.expiresAt.disabled, false);
    assert.equal(sut.note.hidden, true);
    assert.equal(sut.elements.mode.value, 'new');
    assert.equal(sut.elements.etag.value, '');
    assert.equal(sut.elements.name.readOnly, false);
});
