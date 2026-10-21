import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

// The individual SVGs and catalog.json are the editable sources.
const directory = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(directory, '../../..');
const catalog = JSON.parse(fs.readFileSync(path.join(directory, 'catalog.json'), 'utf8'));
const { icons } = catalog;
const ids = new Set(icons.map(icon => icon.id));
if (ids.size !== icons.length) throw new Error('Duplicate icon IDs.');
const escape = text => text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;');

for (const icon of icons) {
  if (!/^[a-z][a-z0-9-]*$/.test(icon.id)) throw new Error(`Invalid ID: ${icon.id}`);
  if (icon.parent && !ids.has(icon.parent)) throw new Error(`Unknown parent: ${icon.parent}`);
  for (const source of icon.sources) {
    if (!fs.existsSync(path.join(root, source))) throw new Error(`Missing source: ${source}`);
  }
}
const guides = [...fs.readFileSync(path.join(root, 'mkdocs.yml'), 'utf8')
  .matchAll(/reference\/((?:features-|common-|testing-)[a-z0-9-]+\.md)/g)]
  .map(match => `docs/${match[1]}`);
const covered = new Set(icons.flatMap(icon => icon.sources));
for (const guide of guides) {
  if (!covered.has(guide)) throw new Error(`Unmapped feature guide: ${guide}`);
}

const symbols = icons.map(icon => {
  const svg = fs.readFileSync(path.join(directory, `${icon.id}.svg`), 'utf8');
  if (!svg.includes('viewBox="0 0 32 32"') || /<(?:script|image|foreignObject)\b|\bon\w+=|\bhref=/i.test(svg)) {
    throw new Error(`Unexpected SVG content: ${icon.id}`);
  }
  const content = svg.replace(/^[\s\S]*?<svg\b[^>]*>/, '')
    .replace(/<title\b[^>]*>[\s\S]*?<\/title>/, '').replace(/<\/svg>\s*$/, '').trim();
  return `<symbol id="feature-${icon.id}" viewBox="0 0 32 32" fill="none">\n${content}\n</symbol>`;
}).join('\n');
const sprite = `<svg xmlns="http://www.w3.org/2000/svg">\n${symbols}\n</svg>\n`;

// Derive the preview tokens with the dashboard's own palette and contrast code.
// Fail on a changed source structure instead of silently using stale colors.
const theme = fs.readFileSync(path.join(root, 'src/Presentation.Web/Pages/_DashboardTheme.cshtml'), 'utf8');
const markers = ['const palettes =', 'const themes =', 'const rgb =', 'window.bdkTheme ='];
const offsets = markers.map(marker => theme.indexOf(marker));
if (offsets.some(offset => offset < 0) || offsets.some((offset, i) => i && offset <= offsets[i - 1])) {
  throw new Error('Dashboard theme structure changed. Update the theme extraction markers.');
}
const definitions = theme.slice(offsets[0], offsets[1]) + theme.slice(offsets[2], offsets[3]);
const tokens = vm.runInNewContext(definitions + `JSON.stringify(Object.entries(palettes).map(([id, palette]) => {
  const variables = themeVariables(palette);
  return { id, label: palette.label, mode: palette.mode, variables: Object.fromEntries([
    '--bs-body-bg', '--bs-body-color', '--bs-secondary-color', '--dashboard-surface',
    '--dashboard-surface-inset', '--dashboard-panel-border', '--dashboard-panel-border-hover',
    '--dashboard-info-text', '--dashboard-primary-text', '--dashboard-analysis-text'
  ].map(key => [key, variables[key]])) };
}))`, {}, { timeout: 1000 });
const groups = [...new Set(icons.map(icon => icon.group))];
const metadata = icons.map(icon => ({ ...icon, sources: icon.sources.map(source => ({
  label: path.basename(source), href: path.relative(directory, path.join(root, source)).split(path.sep).join('/')
})) }));
const scriptData = value => JSON.stringify(value).replaceAll('<', '\\u003c');
const html = `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>bITdevKit · Feature icons</title>
  <style>
    * { box-sizing: border-box; }
    :root { --icon-size: 40px; }
    body { margin: 0; background: var(--bs-body-bg); color: var(--bs-body-color); font: 13px/1.5 ui-monospace, monospace; }
    button, input, select { font: inherit; }
    button, select, input[type="checkbox"], input[type="range"] { cursor: pointer; }
    a { color: var(--dashboard-primary-text); text-underline-offset: 3px; }
    button:focus-visible, a:focus-visible, select:focus-visible, input:focus-visible { outline: 2px solid var(--dashboard-primary-text); outline-offset: 4px; }
    main { max-width: 1560px; margin: auto; padding: 40px 32px; }
    header { display: flex; justify-content: space-between; align-items: end; gap: 24px; margin-bottom: 28px; }
    .eyebrow { color: var(--dashboard-info-text); font-size: 11px; letter-spacing: .12em; text-transform: uppercase; }
    h1 { margin: 4px 0 10px; font-size: 30px; line-height: 1.2; font-weight: 600; letter-spacing: -.05em; }
    p { margin: 0; color: var(--bs-secondary-color); }
    .downloads { display: flex; flex-wrap: wrap; gap: 16px; font-size: 11px; }
    .controls { display: flex; flex-wrap: wrap; align-items: end; gap: 12px 18px; padding: 18px; margin-bottom: 24px; background: var(--dashboard-surface); border: 1px solid var(--dashboard-panel-border); border-radius: 6px; }
    label { display: grid; gap: 6px; color: var(--bs-secondary-color); font-size: 11px; }
    label.search { flex: 1 1 200px; }
    input[type="search"], select { width: 100%; min-height: 36px; background: var(--dashboard-surface-inset); color: var(--bs-body-color); border: 1px solid var(--dashboard-panel-border); border-radius: 4px; padding: 7px 10px; }
    .size-control { min-width: 100px; min-height: 59px; }
    input[type="range"] { width: 110px; min-height: 30px; accent-color: var(--dashboard-primary-text); }
    .checkbox { display: flex; align-items: center; gap: 8px; min-height: 36px; }
    input[type="checkbox"] { accent-color: var(--dashboard-primary-text); }
    .summary { margin-bottom: 24px; font-size: 11px; }
    section { margin-bottom: 32px; }
    h2 { display: flex; align-items: baseline; gap: 10px; margin: 0 0 12px; font-size: 14px; font-weight: 500; }
    h2 span { color: var(--bs-secondary-color); font-size: 10px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(155px, 1fr)); gap: 10px; }
    .tile { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 12px; min-height: 140px; padding: 20px 10px 14px; background: var(--dashboard-surface); border: 1px solid var(--dashboard-panel-border); border-radius: 6px; color: var(--bs-body-color); text-align: center; transition: border-color .12s; }
    .tile:hover { border-color: var(--dashboard-primary-text); }
    .tile .glyph { display: grid; place-items: center; min-height: 64px; }
    .tile svg { width: var(--icon-size); height: var(--icon-size); }
    .name { font-size: 11px; line-height: 1.4; }
    .parent { color: var(--bs-secondary-color); font-size: 9px; display: block; margin-top: 4px; }
    .monochrome .glyph { --dashboard-info-text: var(--bs-body-color); --dashboard-primary-text: var(--bs-body-color); --dashboard-analysis-text: var(--bs-body-color); }
    .empty { padding: 40px 0; }
    footer { border-top: 1px solid var(--dashboard-panel-border); padding-top: 20px; color: var(--bs-secondary-color); font-size: 10px; }
    dialog { width: min(650px, calc(100% - 32px)); max-height: calc(100% - 32px); border: 1px solid var(--dashboard-panel-border-hover); border-radius: 8px; background: var(--dashboard-surface); color: var(--bs-body-color); padding: 28px; overflow: auto; }
    dialog::backdrop { background: #0009; }
    .dialog-header { display: flex; align-items: start; justify-content: space-between; gap: 16px; }
    .dialog-header h2 { font-size: 20px; margin-bottom: 8px; }
    .close { color: var(--bs-body-color); background: none; border: 1px solid var(--dashboard-panel-border); border-radius: 4px; padding: 4px 10px; }
    .hero { display: grid; place-items: center; min-height: 200px; }
    .hero svg { width: 128px; height: 128px; }
    .sizes { display: flex; justify-content: space-between; align-items: end; flex-wrap: wrap; gap: 16px; padding: 20px 0; border-top: 1px solid var(--dashboard-panel-border); }
    figure { display: grid; justify-items: center; gap: 10px; margin: 0; }
    figcaption { color: var(--bs-secondary-color); font-size: 10px; }
    .dialog-links { display: flex; flex-wrap: wrap; gap: 16px; margin-top: 12px; }
    .sources { display: grid; gap: 6px; padding-top: 20px; font-size: 10px; overflow-wrap: anywhere; }
    @media (max-width: 700px) { main { padding: 24px 16px; } header { align-items: start; flex-direction: column; } .grid { grid-template-columns: repeat(auto-fill, minmax(132px, 1fr)); } .controls { padding: 14px; } }
    @media (prefers-reduced-motion: reduce) { .tile { transition: none; } }
  </style>
</head>
<body>
  <svg xmlns="http://www.w3.org/2000/svg" width="0" height="0" aria-hidden="true" style="position:absolute;overflow:hidden"><defs>
${symbols}
  </defs></svg>
  <main>
    <header>
      <div><div class="eyebrow">bITdevKit / feature icons</div><h1>Feature icons</h1><p>${icons.length} icons · ${groups.length} families · 32 × 32 grid · 1.6 unit stroke</p></div>
      <nav class="downloads" aria-label="Catalog files"><a href="features.svg" download>SVG sprite</a><a href="catalog.json" download>Catalog JSON</a><a href="README.md">Usage</a></nav>
    </header>
    <div class="controls" role="search" aria-label="Filter icons">
      <label class="search" for="search">Find an icon<input id="search" type="search" placeholder="Storage, signals, validation…" autocomplete="off"></label>
      <label for="family">Family<select id="family"><option value="">All families</option>${groups.map(group => `<option>${escape(group)}</option>`).join('')}</select></label>
      <label for="level">Level<select id="level"><option value="">Features + subfeatures</option><option value="feature">Features</option><option value="subfeature">Subfeatures</option></select></label>
      <label for="theme">Theme<select id="theme"></select></label>
      <label class="size-control" for="size">Size <output id="size-value" for="size">40 px</output><input id="size" type="range" min="16" max="64" step="4" value="40"></label>
      <label class="checkbox" for="mono"><input id="mono" type="checkbox">Monochrome</label>
    </div>
    <p class="summary" id="summary" role="status" aria-live="polite"></p>
    <div id="catalog"></div>
    <footer>Each glyph opens a size preview and its source references. Colors follow the dashboard palettes. Provider icons describe capabilities and are not vendor logos.</footer>
  </main>
  <dialog id="detail" aria-labelledby="detail-title">
    <div class="dialog-header"><div><h2 id="detail-title"></h2><p id="detail-family"></p></div><button class="close" type="button" id="close-detail" aria-label="Close icon preview">Close</button></div>
    <div class="hero glyph" id="detail-hero"></div><div class="sizes" id="detail-sizes"></div>
    <nav class="dialog-links" aria-label="Icon files"><a id="download-icon" download>Download SVG</a><a id="open-icon" target="_blank" rel="noopener">Open SVG</a></nav>
    <div class="sources" id="detail-sources"></div>
  </dialog>
  <script>
    const icons = ${scriptData(metadata)};
    const themes = ${tokens};
    const groups = ${scriptData(groups)};
    const byId = new Map(icons.map(icon => [icon.id, icon]));
    const el = id => document.getElementById(id);
    const glyph = (id, size) => {
      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      svg.setAttribute('width', size);
      svg.setAttribute('height', size);
      svg.setAttribute('viewBox', '0 0 32 32');
      svg.setAttribute('aria-hidden', 'true');
      svg.setAttribute('focusable', 'false');
      const use = document.createElementNS(svg.namespaceURI, 'use');
      use.setAttribute('href', '#feature-' + id);
      svg.append(use);
      return svg;
    };
    const applyTheme = id => {
      const theme = themes.find(theme => theme.id === id) || themes[0];
      for (const [key, value] of Object.entries(theme.variables)) document.documentElement.style.setProperty(key, value);
      document.documentElement.style.colorScheme = theme.mode;
      el('theme').value = theme.id;
    };
    for (const theme of themes) {
      const option = document.createElement('option');
      option.value = theme.id;
      option.textContent = theme.label;
      el('theme').append(option);
    }
    applyTheme('tokyo-night');
    const showDetail = icon => {
      el('detail-title').textContent = icon.title;
      el('detail-family').textContent = icon.group + (icon.parent ? ' / ' + byId.get(icon.parent).title : '');
      el('detail-hero').replaceChildren(glyph(icon.id, 128));
      el('detail-sizes').replaceChildren();
      for (const size of [16, 20, 24, 32, 48, 64]) {
        const figure = document.createElement('figure');
        figure.className = 'glyph';
        const caption = document.createElement('figcaption');
        caption.textContent = size + ' px';
        figure.append(glyph(icon.id, size), caption);
        el('detail-sizes').append(figure);
      }
      for (const id of ['download-icon', 'open-icon']) el(id).href = icon.id + '.svg';
      el('detail-sources').replaceChildren();
      for (const source of icon.sources) {
        const link = document.createElement('a');
        link.href = source.href;
        link.textContent = source.label;
        el('detail-sources').append(link);
      }
      if (!el('detail').open) el('detail').showModal();
    };
    const render = () => {
      const query = el('search').value.toLowerCase().trim();
      const selected = icons.filter(icon => (!el('family').value || icon.group === el('family').value)
        && (!el('level').value || (icon.parent ? 'subfeature' : 'feature') === el('level').value)
        && [icon.id, icon.title, icon.group, byId.get(icon.parent)?.title || '', ...icon.sources.map(source => source.label)].join(' ').toLowerCase().includes(query));
      el('catalog').replaceChildren();
      for (const group of groups) {
        const items = selected.filter(icon => icon.group === group);
        if (!items.length) continue;
        const section = document.createElement('section');
        const heading = document.createElement('h2');
        heading.append(document.createTextNode(group));
        const count = document.createElement('span');
        count.textContent = items.length;
        heading.append(count);
        const grid = document.createElement('div');
        grid.className = 'grid';
        for (const icon of items) {
          const button = document.createElement('button');
          button.type = 'button';
          button.className = 'tile';
          button.dataset.icon = icon.id;
          const visual = document.createElement('span');
          visual.className = 'glyph';
          visual.append(glyph(icon.id, 40));
          const label = document.createElement('span');
          label.className = 'name';
          label.textContent = icon.title;
          if (icon.parent) {
            const parent = document.createElement('span');
            parent.className = 'parent';
            parent.textContent = byId.get(icon.parent).title;
            label.append(parent);
          }
          button.append(visual, label);
          button.addEventListener('click', () => showDetail(icon));
          grid.append(button);
        }
        section.append(heading, grid);
        el('catalog').append(section);
      }
      if (!selected.length) {
        const empty = document.createElement('p');
        empty.className = 'empty';
        empty.textContent = 'No matching icons. Try a different search or family.';
        el('catalog').append(empty);
      }
      el('summary').textContent = selected.length + ' of ' + icons.length + ' icons · ' + selected.filter(icon => !icon.parent).length + ' features · ' + selected.filter(icon => icon.parent).length + ' subfeatures';
    };
    for (const id of ['search', 'family', 'level']) el(id).addEventListener('input', render);
    el('theme').addEventListener('change', () => applyTheme(el('theme').value));
    el('size').addEventListener('input', () => {
      document.documentElement.style.setProperty('--icon-size', el('size').value + 'px');
      el('size-value').textContent = el('size').value + ' px';
    });
    el('mono').addEventListener('change', () => document.body.classList.toggle('monochrome', el('mono').checked));
    el('close-detail').addEventListener('click', () => el('detail').close());
    render();
  </script>
</body>
</html>
`;

const outputs = new Map([['features.svg', sprite], ['index.html', html]]);
for (const [filename, content] of outputs) {
  const output = path.join(directory, filename);
  if (process.argv.includes('--check')) {
    if (!fs.existsSync(output) || fs.readFileSync(output, 'utf8') !== content) {
      throw new Error(`Generated file is stale: ${filename}. Run node docs/assets/features/build-catalog.mjs`);
    }
  } else {
    fs.writeFileSync(output, content);
  }
}
console.log(`${process.argv.includes('--check') ? 'Verified' : 'Built'} ${icons.length} icons, ${groups.length} families, ${JSON.parse(tokens).length} themes.`);
