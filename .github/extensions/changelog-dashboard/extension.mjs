import { createServer } from "node:http";
import { promises as fs } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { CanvasError, createCanvas, joinSession } from "@github/copilot-sdk/extension";

const servers = new Map();
const extensionDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(extensionDirectory, "..", "..", "..");
const changelogPath = path.join(repositoryRoot, "CHANGELOG.md");
const changelogRelativePath = "CHANGELOG.md";

const openInputSchema = {
    type: "object",
    additionalProperties: false,
    properties: {
        version: { type: "string" },
    },
};

const versionInputSchema = {
    type: "object",
    additionalProperties: false,
    properties: {
        version: { type: "string" },
    },
};

const exportInputSchema = {
    type: "object",
    additionalProperties: false,
    properties: {
        version: { type: "string" },
        format: {
            type: "string",
            enum: ["markdown", "text", "both"],
        },
    },
};

async function readChangelogState(selectedVersion = "") {
    const content = await fs.readFile(changelogPath, "utf8");
    const parsed = parseChangelog(content);
    const selectedRelease =
        findRelease(parsed.releases, selectedVersion) ??
        parsed.releases.find((release) => release.notes.length > 0) ??
        parsed.releases[0] ??
        createEmptyRelease();

    return {
        ...parsed,
        selectedVersion: selectedRelease.version,
        selectedRelease,
        sourcePath: changelogRelativePath,
        generatedAt: new Date().toISOString(),
    };
}

function parseChangelog(content) {
    const normalized = content.replace(/^\uFEFF/, "").replace(/\r\n/g, "\n");
    const title = normalized.match(/^#\s+(.+)$/m)?.[1]?.trim() || "Changelog";
    const headingMatches = [...normalized.matchAll(/^##\s+\[?([^\]\n]+)\]?(?:\s+-\s+([0-9]{4}-[0-9]{2}-[0-9]{2}))?\s*$/gm)];
    const firstHeadingIndex = headingMatches[0]?.index ?? normalized.length;
    const intro = normalized
        .slice(0, firstHeadingIndex)
        .split("\n")
        .map((line) => line.trim())
        .filter((line) => line && !line.startsWith("#"))
        .join(" ");

    const releases = headingMatches.map((match, index) => {
        const nextMatch = headingMatches[index + 1];
        const bodyStart = (match.index ?? 0) + match[0].length;
        const bodyEnd = nextMatch?.index ?? normalized.length;
        const body = normalized.slice(bodyStart, bodyEnd).trim();
        const notes = parseReleaseNotes(body);
        const categories = categorizeNotes(notes);

        return {
            version: match[1].trim(),
            date: match[2]?.trim() ?? "",
            body,
            notes,
            categories,
            summary: summarizeRelease(notes),
            stats: buildReleaseStats(notes, categories),
        };
    });

    return {
        title,
        intro,
        releases,
        totalNotes: releases.reduce((count, release) => count + release.notes.length, 0),
    };
}

function parseReleaseNotes(body) {
    return body
        .split("\n")
        .map((line) => line.trim())
        .filter((line) => line.startsWith("- "))
        .map((line) => line.slice(2).trim())
        .filter(Boolean);
}

function categorizeNotes(notes) {
    const categories = [
        {
            id: "feature",
            title: "Feature work",
            label: "Feature",
            notes: [],
        },
        {
            id: "improvement",
            title: "Improvements",
            label: "Improve",
            notes: [],
        },
        {
            id: "quality",
            title: "Quality and fixes",
            label: "Quality",
            notes: [],
        },
        {
            id: "maintenance",
            title: "Maintenance",
            label: "Maintain",
            notes: [],
        },
    ];

    const byId = new Map(categories.map((category) => [category.id, category]));
    for (const note of notes) {
        byId.get(classifyNote(note)).notes.push(note);
    }

    return categories.filter((category) => category.notes.length > 0);
}

function classifyNote(note) {
    const lower = note.toLowerCase();
    if (/^(added|add|introduced|introduce|enabled|enable|started|generate|support)/.test(lower)) {
        return "feature";
    }

    if (/^(improved|improve|expanded|refined|changed|updated|continued|enhanced)/.test(lower)) {
        return "improvement";
    }

    if (/^(fixed|fix|corrected|tightened|removed|reduced)/.test(lower)) {
        return "quality";
    }

    return "maintenance";
}

function summarizeRelease(notes) {
    if (notes.length === 0) {
        return "No release notes have been added for this section yet.";
    }

    if (notes.length === 1) {
        return notes[0];
    }

    return `${notes.length} notable changes spanning ${uniqueCategoryCount(notes)} release areas.`;
}

function uniqueCategoryCount(notes) {
    return new Set(notes.map(classifyNote)).size;
}

function buildReleaseStats(notes, categories) {
    return [
        { label: "Notes", value: String(notes.length).padStart(2, "0") },
        { label: "Areas", value: String(categories.length).padStart(2, "0") },
        {
            label: "Features",
            value: String(categories.find((category) => category.id === "feature")?.notes.length ?? 0).padStart(2, "0"),
        },
        {
            label: "Fixes",
            value: String(categories.find((category) => category.id === "quality")?.notes.length ?? 0).padStart(2, "0"),
        },
    ];
}

function createEmptyRelease() {
    return {
        version: "Unreleased",
        date: "",
        body: "",
        notes: [],
        categories: [],
        summary: "No changelog releases were found.",
        stats: [
            { label: "Notes", value: "00" },
            { label: "Areas", value: "00" },
            { label: "Features", value: "00" },
            { label: "Fixes", value: "00" },
        ],
    };
}

function findRelease(releases, version) {
    if (!version) {
        return null;
    }

    return releases.find((release) => release.version.toLowerCase() === version.toLowerCase()) ?? null;
}

function buildSnapshot(state) {
    const release = state.selectedRelease;

    return {
        title: `${state.title} ${release.version}`,
        sourcePath: state.sourcePath,
        version: release.version,
        date: release.date,
        summary: release.summary,
        noteCount: release.notes.length,
        categories: release.categories.map((category) => ({
            id: category.id,
            title: category.title,
            count: category.notes.length,
        })),
    };
}

function buildExportPayload(state, input = {}) {
    const requestedVersion = typeof input.version === "string" ? input.version : "";
    const format = typeof input.format === "string" ? input.format : "both";
    const release = findRelease(state.releases, requestedVersion) ?? state.selectedRelease;
    const markdown = buildReleaseMarkdown(release);
    const text = buildReleaseText(release);
    const fileNameBase = `changelog-${slugify(release.version)}`;

    if (format === "markdown") {
        return { version: release.version, fileNameBase, markdown };
    }

    if (format === "text") {
        return { version: release.version, fileNameBase, text };
    }

    return { version: release.version, fileNameBase, markdown, text };
}

function buildReleaseMarkdown(release) {
    const date = release.date ? ` - ${release.date}` : "";
    const notes = release.notes.map((note) => `- ${note}`).join("\n");
    return `## [${release.version}]${date}\n\n${notes}\n`;
}

function buildReleaseText(release) {
    const date = release.date ? ` (${release.date})` : "";
    const notes = release.notes.map((note) => `- ${stripMarkdown(note)}`).join("\n");
    return `${release.version}${date}\n\n${notes}\n`;
}

function stripMarkdown(value) {
    return value
        .replace(/`([^`]+)`/g, "$1")
        .replace(/\[([^\]]+)\]\([^)]+\)/g, "$1");
}

function slugify(value) {
    return value
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-+|-+$/g, "");
}

function sendHtml(res, html) {
    res.writeHead(200, {
        "Content-Type": "text/html; charset=utf-8",
        "Cache-Control": "no-store",
    });
    res.end(html);
}

function sendJson(res, value, statusCode = 200) {
    res.writeHead(statusCode, {
        "Content-Type": "application/json; charset=utf-8",
        "Cache-Control": "no-store",
    });
    res.end(JSON.stringify(value));
}

function sendError(res, error) {
    sendJson(res, { error: error.message }, 500);
}

async function readRequestJson(req) {
    const chunks = [];
    for await (const chunk of req) {
        chunks.push(chunk);
    }

    if (chunks.length === 0) {
        return {};
    }

    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
}

async function startServer(initialState) {
    let state = initialState;

    const server = createServer(async (req, res) => {
        try {
            const requestUrl = new URL(req.url ?? "/", "http://127.0.0.1");

            if (req.method === "GET" && requestUrl.pathname === "/") {
                sendHtml(res, renderHtml(state));
                return;
            }

            if (req.method === "GET" && requestUrl.pathname === "/api/changelog") {
                sendJson(res, state);
                return;
            }

            if (req.method === "POST" && requestUrl.pathname === "/actions/select-release") {
                const body = await readRequestJson(req);
                const selectedVersion = typeof body.version === "string" ? body.version : "";
                state = await readChangelogState(selectedVersion);
                sendJson(res, buildSnapshot(state));
                return;
            }

            if (req.method === "POST" && requestUrl.pathname === "/actions/refresh") {
                state = await readChangelogState(state.selectedVersion);
                sendJson(res, buildSnapshot(state));
                return;
            }

            if (req.method === "POST" && requestUrl.pathname === "/actions/export") {
                const body = await readRequestJson(req);
                sendJson(res, buildExportPayload(state, body));
                return;
            }

            sendJson(res, { error: "Not found" }, 404);
        } catch (error) {
            sendError(res, error);
        }
    });

    await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
    const address = server.address();
    const port = typeof address === "object" && address ? address.port : 0;

    return {
        server,
        url: `http://127.0.0.1:${port}/`,
        getState() {
            return state;
        },
        setState(nextState) {
            state = nextState;
        },
    };
}

function renderHtml(state) {
    const release = state.selectedRelease;
    const releases = state.releases
        .map((item) => {
            const selected = item.version === release.version ? "selected" : "";
            const date = item.date ? `<span>${escapeHtml(item.date)}</span>` : "";
            return `
                <button class="release-item ${selected}" type="button" data-version="${escapeHtml(item.version)}">
                    <strong>${escapeHtml(item.version)}</strong>
                    ${date}
                    <em>${item.notes.length} notes</em>
                </button>
            `;
        })
        .join("");
    const stats = release.stats
        .map(
            (stat, index) => `
                <article class="stat-card tone-${index % 4}">
                    <strong>${escapeHtml(stat.value)}</strong>
                    <span>${escapeHtml(stat.label)}</span>
                </article>
            `,
        )
        .join("");
    const categoryCards = release.categories.length > 0
        ? release.categories.map(renderCategoryCard).join("")
        : `<article class="empty-card">No notes yet. Add entries under <code>## [${escapeHtml(release.version)}]</code> in <code>${escapeHtml(state.sourcePath)}</code>.</article>`;
    const allNotes = release.notes
        .map((note) => `<li><span>${escapeHtml(stripMarkdown(note))}</span></li>`)
        .join("");
    const latestRelease = state.releases.find((item) => item.notes.length > 0) ?? release;

    return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Changelog Dashboard</title>
    <style>
      :root {
        color-scheme: dark;
        --bs-primary: #66d9ef;
        --bs-primary-rgb: 102,217,239;
        --bs-secondary: #fd971f;
        --bs-secondary-rgb: 253,151,31;
        --bs-success: #a6e22e;
        --bs-success-rgb: 166,226,46;
        --bs-warning: #fd971f;
        --bs-warning-rgb: 253,151,31;
        --bs-danger: #f92672;
        --bs-danger-rgb: 249,38,114;
        --bs-body-bg: #151414;
        --bs-body-color: #f8f8f2;
        --bs-tertiary-bg: #1d1c1c;
        --bs-secondary-bg: #242222;
        --bs-border-color: #302d2d;
        --bs-emphasis-color: #ffffff;
        --bs-secondary-color: #a59f99;
        --dashboard-surface: #171616;
        --dashboard-surface-muted: #1d1c1c;
        --dashboard-border: #302d2d;
        --dashboard-border-rgb: 248,248,242;
        --dashboard-panel-border: rgba(var(--dashboard-border-rgb), .12);
        --dashboard-panel-border-hover: rgba(var(--dashboard-border-rgb), .18);
        --dashboard-text: #f8f8f2;
        --dashboard-muted: #a59f99;
        --dashboard-code-bg: #0f0e0e;
        --dashboard-sidebar-bg: #111010;
        --dashboard-active-bg: rgba(var(--bs-secondary-rgb), .12);
        --dashboard-active-fg: var(--bs-secondary);
      }

      * {
        box-sizing: border-box;
      }

      body {
        margin: 0;
        min-height: 100vh;
        background: var(--bs-body-bg);
        color: var(--bs-body-color);
        font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace;
        font-size: .875rem;
        line-height: 1.45;
      }

      body::before {
        content: "";
        position: fixed;
        inset: 0;
        z-index: -1;
        pointer-events: none;
        background:
          repeating-linear-gradient(
            to bottom,
            rgba(var(--dashboard-border-rgb), .035),
            rgba(var(--dashboard-border-rgb), .035) 1px,
            transparent 1px,
            transparent 4px),
          linear-gradient(90deg, rgba(var(--dashboard-border-rgb), .018) 1px, transparent 1px);
        background-size: auto, 48px 48px;
        opacity: .45;
      }

      button,
      input {
        font: inherit;
      }

      .shell {
        display: grid;
        grid-template-columns: minmax(13rem, 16rem) minmax(0, 1fr);
        min-height: 100vh;
      }

      .sidebar {
        position: sticky;
        top: 0;
        align-self: start;
        height: 100vh;
        overflow: hidden;
        display: grid;
        grid-template-rows: auto auto minmax(0, 1fr);
        background: var(--dashboard-sidebar-bg);
        border-right: 1px solid var(--bs-border-color);
      }

      .sidebar-header {
        min-height: 3.15rem;
        padding: .8rem .75rem;
        border-bottom: 1px solid var(--bs-border-color);
      }

      .sidebar h2,
      .panel h2 {
        margin: 0;
        color: var(--bs-emphasis-color);
        font-size: 1rem;
        font-weight: 700;
        letter-spacing: .02em;
        line-height: 1.25;
      }

      .kicker {
        margin: 0 0 .35rem;
        color: var(--bs-secondary-color);
        font-size: .64rem;
        font-weight: 700;
        letter-spacing: .05em;
        text-transform: uppercase;
      }

      .search-wrap {
        padding: .65rem .55rem;
        border-bottom: 1px solid var(--bs-border-color);
      }

      .search {
        width: 100%;
        min-height: 2rem;
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .1rem;
        background: var(--dashboard-code-bg);
        color: var(--bs-body-color);
        padding: .35rem .55rem;
        outline-color: var(--dashboard-active-fg);
      }

      .search::placeholder {
        color: var(--bs-secondary-color);
      }

      .release-list {
        overflow: auto;
        padding: .5rem .35rem 1rem;
        scrollbar-width: thin;
        scrollbar-color: rgba(var(--bs-secondary-rgb), .45) transparent;
      }

      .release-item {
        width: 100%;
        border: 0;
        border-radius: .1rem;
        background: transparent;
        color: var(--bs-body-color);
        cursor: pointer;
        display: grid;
        gap: .05rem;
        margin: 0;
        padding: .34rem .6rem;
        text-align: left;
      }

      .release-item:hover,
      .release-item:focus,
      .release-item.selected {
        color: var(--dashboard-active-fg);
        background: var(--dashboard-active-bg);
      }

      .release-item strong {
        font-size: .84rem;
        font-weight: 600;
      }

      .release-item span,
      .release-item em {
        color: var(--bs-secondary-color);
        font-size: .72rem;
        font-style: normal;
      }

      .content {
        min-width: 0;
        padding: 1rem;
        display: grid;
        gap: 1rem;
      }

      .hero,
      .panel {
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .125rem;
        background: var(--dashboard-surface);
      }

      .hero {
        padding: 1rem;
      }

      .hero-grid {
        display: grid;
        grid-template-columns: minmax(0, 1fr) minmax(18rem, .45fr);
        gap: 1rem;
        align-items: start;
      }

      .brand {
        display: inline-flex;
        align-items: center;
        gap: .5rem;
        color: var(--bs-secondary-color);
        font-size: .7rem;
        font-weight: 700;
        letter-spacing: .05em;
        text-transform: uppercase;
      }

      .brand-mark {
        display: inline-block;
        width: .75rem;
        height: .75rem;
        border: 1px solid var(--bs-secondary);
        box-shadow: inset 0 0 0 2px var(--dashboard-surface);
        background: var(--dashboard-active-bg);
      }

      .brand-mark span {
        display: none;
      }

      h1 {
        margin: .45rem 0 .45rem;
        color: var(--bs-emphasis-color);
        font-size: clamp(1.45rem, 4vw, 2.25rem);
        font-weight: 700;
        letter-spacing: -.03em;
        line-height: 1.05;
      }

      .version-chip,
      .pill,
      .category-badge {
        display: inline-flex;
        align-items: center;
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .1rem;
        background: var(--dashboard-surface-muted);
        color: var(--bs-secondary-color);
        font-size: .72rem;
        font-weight: 600;
        line-height: 1;
        padding: .28rem .45rem;
      }

      .version-chip {
        margin-left: .5rem;
        color: var(--dashboard-active-fg);
        background: var(--dashboard-active-bg);
        vertical-align: middle;
      }

      .summary {
        max-width: 78ch;
        margin: 0;
        color: var(--bs-secondary-color);
      }

      .meta-row {
        display: flex;
        flex-wrap: wrap;
        gap: .4rem;
        margin-top: .8rem;
      }

      .stat-grid {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: .5rem;
      }

      .stat-card {
        min-height: 4.7rem;
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .125rem;
        background: var(--dashboard-surface-muted);
        padding: .75rem;
      }

      .stat-card strong {
        display: block;
        color: var(--bs-primary);
        font-size: 1.55rem;
        font-weight: 700;
        line-height: 1;
      }

      .stat-card span {
        display: block;
        margin-top: .4rem;
        color: var(--bs-secondary-color);
        font-size: .64rem;
        font-weight: 700;
        letter-spacing: .05em;
        text-transform: uppercase;
      }

      .stat-card.tone-1 strong { color: var(--bs-secondary); }
      .stat-card.tone-2 strong { color: var(--bs-success); }
      .stat-card.tone-3 strong { color: var(--bs-warning); }

      .toolbar {
        display: flex;
        flex-wrap: wrap;
        gap: .5rem;
      }

      .action-button {
        border: 1px solid rgba(var(--bs-secondary-rgb), .35);
        border-radius: .1rem;
        background: var(--dashboard-active-bg);
        color: var(--dashboard-active-fg);
        cursor: pointer;
        font-size: .78rem;
        font-weight: 700;
        padding: .42rem .65rem;
      }

      .action-button:hover,
      .action-button:focus {
        border-color: rgba(var(--bs-secondary-rgb), .6);
        background: rgba(var(--bs-secondary-rgb), .16);
      }

      .action-button.secondary {
        border-color: var(--dashboard-panel-border);
        background: var(--dashboard-surface-muted);
        color: var(--bs-body-color);
      }

      .panel {
        padding: 1rem;
      }

      .category-grid {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: .75rem;
        margin-top: .75rem;
      }

      .category-card,
      .empty-card {
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .125rem;
        background: var(--dashboard-surface-muted);
        padding: .85rem;
      }

      .category-card:hover {
        border-color: var(--dashboard-panel-border-hover);
      }

      .category-topline {
        display: flex;
        justify-content: space-between;
        gap: .75rem;
        align-items: center;
      }

      .category-badge {
        color: var(--bs-primary);
        text-transform: uppercase;
      }

      .category-card.improvement .category-badge { color: var(--bs-secondary); }
      .category-card.quality .category-badge { color: var(--bs-warning); }
      .category-card.maintenance .category-badge { color: var(--bs-success); }

      .category-card h3 {
        margin: .65rem 0 .45rem;
        color: var(--bs-emphasis-color);
        font-size: .95rem;
      }

      .category-card ul,
      .note-list {
        margin: 0;
        padding-left: 1.2rem;
        color: var(--bs-secondary-color);
      }

      .category-card li,
      .note-list li {
        margin: .45rem 0;
      }

      .note-list {
        columns: 2 22rem;
        margin-top: .75rem;
      }

      code {
        border-radius: .1rem;
        background: var(--dashboard-code-bg);
        color: var(--bs-primary);
        padding: .08rem .25rem;
      }

      .toast {
        position: fixed;
        right: 1rem;
        bottom: 1rem;
        max-width: 24rem;
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .125rem;
        background: var(--dashboard-surface);
        color: var(--bs-body-color);
        opacity: 0;
        padding: .65rem .8rem;
        pointer-events: none;
        transform: translateY(.5rem);
        transition: opacity 160ms ease, transform 160ms ease;
      }

      .toast.visible {
        opacity: 1;
        transform: translateY(0);
      }

      @media (max-width: 920px) {
        .shell,
        .hero-grid,
        .category-grid {
          grid-template-columns: 1fr;
        }

        .sidebar {
          position: static;
          height: auto;
          max-height: 24rem;
        }
      }
    </style>
  </head>
  <body>
    <main class="shell">
      <aside class="sidebar">
        <div class="sidebar-header">
          <p class="kicker">Release archive</p>
          <h2>${escapeHtml(state.releases.length)} releases</h2>
        </div>
        <div class="search-wrap">
          <input class="search" id="release-search" type="search" placeholder="Filter releases" />
        </div>
        <div class="release-list" id="release-list">${releases}</div>
      </aside>

      <section class="content">
        <section class="hero">
          <div class="hero-grid">
            <div>
              <div class="brand">
                <span class="brand-mark"><span></span><span></span><span></span><span></span></span>
                <span>${escapeHtml(state.title)} showcase</span>
              </div>
              <h1>${escapeHtml(release.version)}<span class="version-chip">release</span></h1>
              <p class="summary">${escapeHtml(release.summary)}</p>
              <div class="meta-row">
                <span class="pill">${escapeHtml(release.date || "No date")}</span>
                <span class="pill">Source: ${escapeHtml(state.sourcePath)}</span>
                <span class="pill">Latest with notes: ${escapeHtml(latestRelease.version)}</span>
              </div>
            </div>
            <div class="stat-grid">${stats}</div>
          </div>
        </section>

        <section class="panel">
          <div class="toolbar">
            <button class="action-button" id="refresh-button" type="button">Refresh from CHANGELOG.md</button>
            <button class="action-button secondary" data-copy-format="markdown" type="button">Copy markdown</button>
            <button class="action-button secondary" data-copy-format="text" type="button">Copy text</button>
          </div>
        </section>

        <section class="panel">
          <p class="kicker">Release highlights</p>
          <h2>Grouped notes for ${escapeHtml(release.version)}</h2>
          <div class="category-grid">${categoryCards}</div>
        </section>

        <section class="panel">
          <p class="kicker">Complete release note list</p>
          <h2>All entries</h2>
          <ol class="note-list">${allNotes || "<li>No entries yet.</li>"}</ol>
        </section>
      </section>
    </main>
    <div class="toast" id="toast" role="status" aria-live="polite"></div>
    <script>
      const toast = document.getElementById("toast");
      const releaseList = document.getElementById("release-list");
      const releaseSearch = document.getElementById("release-search");
      const refreshButton = document.getElementById("refresh-button");

      function showToast(message) {
        toast.textContent = message;
        toast.classList.add("visible");
        window.clearTimeout(showToast.timerId);
        showToast.timerId = window.setTimeout(() => toast.classList.remove("visible"), 2200);
      }

      async function postJson(url, body) {
        const response = await fetch(url, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(body ?? {}),
        });

        if (!response.ok) {
          throw new Error("Canvas action failed.");
        }

        return response.json();
      }

      async function selectRelease(version) {
        await postJson("/actions/select-release", { version });
        window.location.reload();
      }

      async function copyExport(format) {
        const payload = await postJson("/actions/export", { version: ${JSON.stringify(release.version)}, format });
        const text = format === "markdown" ? payload.markdown : payload.text;
        await navigator.clipboard.writeText(text);
        showToast(format === "markdown" ? "Markdown copied." : "Plain text copied.");
      }

      releaseList.addEventListener("click", async (event) => {
        const button = event.target.closest("[data-version]");
        if (!button) {
          return;
        }

        button.disabled = true;
        try {
          await selectRelease(button.dataset.version);
        } catch (error) {
          showToast(error instanceof Error ? error.message : "Could not select release.");
          button.disabled = false;
        }
      });

      releaseSearch.addEventListener("input", () => {
        const term = releaseSearch.value.trim().toLowerCase();
        document.querySelectorAll(".release-item").forEach((button) => {
          button.hidden = term && !button.textContent.toLowerCase().includes(term);
        });
      });

      refreshButton.addEventListener("click", async () => {
        refreshButton.disabled = true;
        try {
          await postJson("/actions/refresh", {});
          showToast("Changelog refreshed.");
          window.setTimeout(() => window.location.reload(), 250);
        } catch (error) {
          showToast(error instanceof Error ? error.message : "Refresh failed.");
        } finally {
          refreshButton.disabled = false;
        }
      });

      document.querySelectorAll("[data-copy-format]").forEach((button) => {
        button.addEventListener("click", async () => {
          button.disabled = true;
          try {
            await copyExport(button.dataset.copyFormat);
          } catch (error) {
            showToast(error instanceof Error ? error.message : "Copy failed.");
          } finally {
            button.disabled = false;
          }
        });
      });
    </script>
  </body>
</html>`;
}

function renderCategoryCard(category) {
    const notes = category.notes
        .slice(0, 4)
        .map((note) => `<li>${escapeHtml(stripMarkdown(note))}</li>`)
        .join("");

    return `
        <article class="category-card ${escapeHtml(category.id)}">
            <div class="category-topline">
                <span class="category-badge">${escapeHtml(category.label)}</span>
                <span class="pill">${category.notes.length} notes</span>
            </div>
            <h3>${escapeHtml(category.title)}</h3>
            <ul>${notes}</ul>
        </article>
    `;
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
}

const session = await joinSession({
    canvases: [
        createCanvas({
            id: "changelog-showcase",
            displayName: "Changelog showcase",
            description: "Browse this repository's CHANGELOG.md as an interactive release-notes showcase.",
            inputSchema: openInputSchema,
            actions: [
                {
                    name: "get_release_snapshot",
                    description: "Returns a concise snapshot of the selected CHANGELOG.md release.",
                    inputSchema: versionInputSchema,
                    handler: async (ctx) => {
                        const entry = servers.get(ctx.instanceId);
                        const selectedVersion = typeof ctx.input?.version === "string" ? ctx.input.version : "";
                        const state = entry?.getState() ?? await readChangelogState(selectedVersion);
                        const release = selectedVersion ? findRelease(state.releases, selectedVersion) : state.selectedRelease;
                        if (!release) {
                            throw new CanvasError("release_not_found", `Release '${selectedVersion}' was not found in CHANGELOG.md.`);
                        }

                        return buildSnapshot({ ...state, selectedRelease: release, selectedVersion: release.version });
                    },
                },
                {
                    name: "export_release_notes",
                    description: "Returns markdown and/or plain-text release notes from CHANGELOG.md.",
                    inputSchema: exportInputSchema,
                    handler: async (ctx) => {
                        const entry = servers.get(ctx.instanceId);
                        const selectedVersion = typeof ctx.input?.version === "string" ? ctx.input.version : "";
                        const state = entry?.getState() ?? await readChangelogState(selectedVersion);
                        return buildExportPayload(state, ctx.input);
                    },
                },
                {
                    name: "refresh_changelog",
                    description: "Reloads CHANGELOG.md from disk for the open canvas instance.",
                    inputSchema: versionInputSchema,
                    handler: async (ctx) => {
                        const entry = servers.get(ctx.instanceId);
                        if (!entry) {
                            throw new CanvasError(
                                "canvas_state_missing",
                                "Open the changelog showcase canvas before refreshing it.",
                            );
                        }

                        const selectedVersion = typeof ctx.input?.version === "string" ? ctx.input.version : entry.getState().selectedVersion;
                        const state = await readChangelogState(selectedVersion);
                        entry.setState(state);
                        return buildSnapshot(state);
                    },
                },
            ],
            open: async (ctx) => {
                const selectedVersion = typeof ctx.input?.version === "string" ? ctx.input.version : "";
                const state = await readChangelogState(selectedVersion);
                let entry = servers.get(ctx.instanceId);
                if (!entry) {
                    entry = await startServer(state);
                    servers.set(ctx.instanceId, entry);
                } else {
                    entry.setState(state);
                }

                return {
                    title: `${state.title} ${state.selectedRelease.version}`,
                    status: `${state.selectedRelease.notes.length} notes from ${state.sourcePath}`,
                    url: entry.url,
                };
            },
            onClose: async (ctx) => {
                const entry = servers.get(ctx.instanceId);
                if (!entry) {
                    return;
                }

                servers.delete(ctx.instanceId);
                await new Promise((resolve) => entry.server.close(resolve));
            },
        }),
    ],
});
