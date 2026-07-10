import { createServer } from "node:http";
import { promises as fs } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { joinSession, createCanvas } from "@github/copilot-sdk/extension";

const servers = new Map();
const extensionDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(extensionDirectory, "..", "..", "..");

function toPosixPath(value) {
    return value.replaceAll(path.sep, "/");
}

function parseFrontmatter(content) {
    const normalized = content.replace(/^\uFEFF/, "");
    if (!normalized.startsWith("---")) {
        return { data: {}, body: normalized };
    }

    const lines = normalized.split(/\r?\n/);
    if (lines[0].trim() !== "---") {
        return { data: {}, body: normalized };
    }

    const data = {};
    let endIndex = -1;

    for (let index = 1; index < lines.length; index++) {
        const line = lines[index];
        if (line.trim() === "---") {
            endIndex = index;
            break;
        }

        const separatorIndex = line.indexOf(":");
        if (separatorIndex <= 0) {
            continue;
        }

        const key = line.slice(0, separatorIndex).trim();
        const rawValue = line.slice(separatorIndex + 1).trim();
        data[key] = rawValue.replace(/^["']|["']$/g, "");
    }

    if (endIndex < 0) {
        return { data: {}, body: normalized };
    }

    return {
        data,
        body: lines.slice(endIndex + 1).join("\n"),
    };
}

function findTitle(body, fileName, frontmatter) {
    if (frontmatter.title) {
        return frontmatter.title;
    }

    const heading = body.match(/^#\s+(.+)$/m);
    if (heading) {
        return heading[1].trim();
    }

    return fileName
        .replace(/\.prompt\.md$/i, "")
        .replace(/\.md$/i, "")
        .replace(/^spec-/, "")
        .replaceAll("-", " ");
}

function findAbout(body, frontmatter) {
    if (frontmatter.summary) {
        return frontmatter.summary;
    }

    if (frontmatter.description) {
        return frontmatter.description;
    }

    const quote = body.match(/^>\s+(.+)$/m);
    if (quote) {
        return quote[1].trim();
    }

    const paragraph = body
        .split(/\n\s*\n/)
        .map((part) => part.trim())
        .find((part) => part && !part.startsWith("#") && part !== "[TOC]");

    return paragraph?.replace(/\s+/g, " ").slice(0, 240) ?? "";
}

function getPromptPath(specPath) {
    return specPath.replace(/\.md$/i, ".prompt.md");
}

async function fileExists(filePath) {
    try {
        const stat = await fs.stat(filePath);
        return stat.isFile();
    } catch (error) {
        if (error?.code === "ENOENT") {
            return false;
        }

        throw error;
    }
}

async function listMarkdownFiles(directory) {
    const entries = await fs.readdir(directory, { withFileTypes: true });
    const files = [];

    for (const entry of entries) {
        const fullPath = path.join(directory, entry.name);
        if (entry.isDirectory()) {
            files.push(...await listMarkdownFiles(fullPath));
            continue;
        }

        const name = entry.name.toLowerCase();
        if (entry.isFile() && name.endsWith(".md") && !name.endsWith(".prompt.md") && name !== "prompts.md") {
            files.push(fullPath);
        }
    }

    return files;
}

async function readSpecs() {
    const specsDirectory = path.join(repositoryRoot, "docs", "specs");
    const files = await listMarkdownFiles(specsDirectory);

    const specs = await Promise.all(files.map(async (filePath) => {
        const content = await fs.readFile(filePath, "utf8");
        const { data, body } = parseFrontmatter(content);
        const relativePath = toPosixPath(path.relative(repositoryRoot, filePath));
        const fileName = path.basename(filePath);
        const status = data.status?.trim() || "unknown";
        const promptPath = getPromptPath(relativePath);
        const promptExists = await fileExists(path.join(repositoryRoot, ...promptPath.split("/")));

        return {
            title: findTitle(body, fileName, data),
            about: findAbout(body, data),
            status,
            statusKey: status.toLowerCase().replace(/[^a-z0-9]+/g, "-") || "unknown",
            path: relativePath,
            promptPath,
            promptExists,
        };
    }));

    specs.sort((left, right) => left.title.localeCompare(right.title));

    const statuses = specs.reduce((result, spec) => {
        result[spec.status] = (result[spec.status] ?? 0) + 1;
        return result;
    }, {});

    return {
        generatedAt: new Date().toISOString(),
        total: specs.length,
        statuses,
        specs,
    };
}

function findSpecByPath(specs, specPath) {
    const spec = specs.find((item) => item.path === specPath);
    if (!spec) {
        throw new Error(`Unknown spec path: ${specPath}`);
    }

    return spec;
}

function sendJson(res, value) {
    res.writeHead(200, {
        "Content-Type": "application/json; charset=utf-8",
        "Cache-Control": "no-store",
    });
    res.end(JSON.stringify(value));
}

function sendError(res, error) {
    res.writeHead(500, {
        "Content-Type": "application/json; charset=utf-8",
        "Cache-Control": "no-store",
    });
    res.end(JSON.stringify({ error: error.message }));
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

function sendBadRequest(res, message) {
    res.writeHead(400, {
        "Content-Type": "application/json; charset=utf-8",
        "Cache-Control": "no-store",
    });
    res.end(JSON.stringify({ error: message }));
}

async function openSpecForAgent(specPath) {
    const data = await readSpecs();
    const spec = findSpecByPath(data.specs, specPath);

    await session.send({
        prompt: `The specs dashboard user selected ${spec.path}.

Create a new project session in this repository for an agent to work with this spec. Name the session after the spec title: "${spec.title}".

Start the new session with a kickoff prompt that tells the agent to read ${spec.path} and focus only on understanding the specification.

The new session should summarize the spec's intent, scope, key concepts, domain terms, constraints, and open questions. It must not create an implementation plan or edit files until the user explicitly asks for implementation work. Do not edit files in this parent session for this click.`,
        attachments: [{ type: "file", path: spec.path }],
    });

    return {
        path: spec.path,
        title: spec.title,
        message: "Requested a new agent session for this spec.",
    };
}

async function openPromptForAgent(promptPath) {
    const data = await readSpecs();
    const spec = data.specs.find((item) => item.promptPath === promptPath);
    if (!spec || !spec.promptExists) {
        throw new Error(`Unknown implementation prompt path: ${promptPath}`);
    }

    await session.send({
        prompt: `The specs dashboard user selected the implementation prompt file ${spec.promptPath} for ${spec.path}.

Create a new project session in this repository for an agent to work with this implementation prompt file. Name the session after the spec title: "${spec.title} prompts".

Start the new session with a kickoff prompt that tells the agent to read ${spec.promptPath} and ${spec.path}, understand the phased implementation prompts, and wait for the user to choose which phase to execute. The new session must not implement anything until the user explicitly selects a phase.`,
    });

    return {
        path: spec.promptPath,
        title: spec.title,
        message: "Requested a new agent session for this implementation prompt file.",
    };
}

async function requestImplementationPlan(specPath) {
    const data = await readSpecs();
    const spec = findSpecByPath(data.specs, specPath);
    if (spec.promptExists) {
        return {
            path: spec.promptPath,
            title: spec.title,
            message: "Implementation prompt file already exists.",
            exists: true,
        };
    }

    await session.send({
        prompt: `The specs dashboard user requested an implementation planning workflow for ${spec.path}.

Create a new project session in this repository for an agent to prepare the implementation plan for this specification. Name the session after the spec title: "${spec.title} plan".

Start the new session with a kickoff prompt that tells the agent to perform this gated workflow:

1. Read ${spec.path} completely as the feature specification.
2. Read docs/specs/prompts.md completely and use it as the live reference workflow for preparing phase-based implementation prompts. Do not duplicate stale assumptions from memory; follow the current file content.
3. Understand the specification before creating any file. The agent must summarize the spec intent, scope, non-goals, key concepts, domain terms, repository architecture constraints, implementation boundaries, likely affected areas, required tests, and risk areas.
4. Identify all open issues, unclear requirements, contradictions, missing decisions, and implementation questions.
5. If any open issues or questions exist, ask the user to resolve them and wait. Do not create ${spec.promptPath} while questions remain unanswered.
6. Only after the specification is fully understood and all open issues/questions are cleared, create ${spec.promptPath}.
7. Generate a Markdown prompt file ending in .prompt.md for the spec. The output must contain phased implementation prompts intended to be executed by the user together with an AI agent.
8. Include an architecture analysis prompt first.
9. Include shared governance instructions.
10. Split implementation into bounded sequential phases with explicit non-goals, required tests, architecture rules, validation checkpoints, and stop/review points.
11. Prevent one agent from implementing the whole specification in a single pass.
12. Preserve repository architecture boundaries and testing expectations.
13. Do not implement the spec itself; only create the prompt file after the understanding and question-resolution gate is complete.

Do not edit files in this parent session for this click.`,
    });

    return {
        path: spec.promptPath,
        title: spec.title,
        message: "Requested a new implementation planning session.",
        exists: false,
    };
}

function renderHtml() {
    return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Specs Dashboard</title>
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
        --bs-secondary-color: #a59f99;
        --bs-border-color: #302d2d;
        --bs-emphasis-color: #ffffff;
        --dashboard-surface: #171616;
        --dashboard-surface-muted: #1d1c1c;
        --dashboard-panel-border: rgba(248,248,242,.12);
        --dashboard-panel-border-hover: rgba(248,248,242,.18);
        --dashboard-code-bg: #0f0e0e;
        --dashboard-active-bg: rgba(var(--bs-secondary-rgb), .12);
        --dashboard-active-fg: var(--bs-secondary);
      }

      * {
        box-sizing: border-box;
      }

      body {
        margin: 0;
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
          repeating-linear-gradient(to bottom, rgba(248,248,242,.035), rgba(248,248,242,.035) 1px, transparent 1px, transparent 4px),
          linear-gradient(90deg, rgba(248,248,242,.018) 1px, transparent 1px);
        background-size: auto, 48px 48px;
        opacity: .45;
      }

      main {
        display: grid;
        gap: 1rem;
        padding: 1rem;
      }

      header,
      .spec-card,
      .empty {
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .125rem;
        background: var(--dashboard-surface);
      }

      header {
        display: grid;
        gap: .75rem;
        padding: 1rem;
      }

      h1 {
        margin: 0;
        color: var(--bs-emphasis-color);
        font-size: 1.45rem;
        font-weight: 700;
        letter-spacing: -.03em;
        line-height: 1.05;
      }

      .muted {
        color: var(--bs-secondary-color);
      }

      .toolbar,
      .summary {
        display: flex;
        flex-wrap: wrap;
        gap: .5rem;
        align-items: center;
      }

      .summary-card {
        min-width: 8.5rem;
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .125rem;
        background: var(--dashboard-surface-muted);
        padding: .75rem;
      }

      .summary-card strong {
        display: block;
        color: var(--bs-primary);
        font-size: 1.45rem;
        font-weight: 700;
        line-height: 1.3rem;
      }

      input,
      select,
      button {
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .1rem;
        background: var(--dashboard-surface-muted);
        color: var(--bs-body-color);
        font: inherit;
        min-height: 2.15rem;
        padding: .35rem .65rem;
      }

      input {
        min-width: min(24rem, 100%);
        flex: 1;
      }

      input::placeholder {
        color: var(--bs-secondary-color);
      }

      button {
        cursor: pointer;
        font-weight: 700;
      }

      button:hover,
      button:focus {
        border-color: rgba(var(--bs-secondary-rgb), .6);
        color: var(--dashboard-active-fg);
      }

      #refresh,
      .secondary-action {
        border-color: rgba(var(--bs-secondary-rgb), .35);
        background: var(--dashboard-active-bg);
        color: var(--dashboard-active-fg);
      }

      .card-actions {
        display: flex;
        flex-wrap: wrap;
        gap: .5rem;
      }

      .secondary-action {
        min-height: 2rem;
      }

      .spec-grid {
        display: grid;
        gap: .75rem;
        grid-template-columns: repeat(auto-fit, minmax(min(100%, 22rem), 1fr));
      }

      .spec-card {
        display: grid;
        gap: .55rem;
        padding: .85rem;
      }

      .spec-card:hover {
        border-color: var(--dashboard-panel-border-hover);
      }

      .spec-card h2 {
        margin: 0;
        color: var(--bs-emphasis-color);
        font-size: 1rem;
        line-height: 1.3rem;
      }

      .badge {
        align-self: start;
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .1rem;
        background: var(--dashboard-surface-muted);
        display: inline-flex;
        font-size: .72rem;
        font-weight: 700;
        line-height: 1rem;
        padding: .2rem .45rem;
        width: fit-content;
      }

      .badge.implemented {
        color: var(--bs-success);
      }

      .badge.draft {
        color: var(--bs-warning);
      }

      .badge.unknown {
        color: var(--bs-secondary-color);
      }

      .path {
        color: var(--bs-primary);
        font-size: .78rem;
        overflow-wrap: anywhere;
      }

      .path-link {
        align-items: start;
        background: transparent;
        border: 0;
        color: var(--bs-primary);
        cursor: pointer;
        display: inline;
        font: inherit;
        min-height: 0;
        padding: 0;
        text-align: left;
        text-decoration: none;
      }

      .path-link:hover {
        color: var(--dashboard-active-fg);
        text-decoration: underline;
      }

      .prompt-link {
        margin-top: -.15rem;
      }

      .empty {
        color: var(--bs-secondary-color);
        padding: 1.5rem;
        text-align: center;
      }
    </style>
  </head>
  <body>
    <main>
      <header>
        <div>
          <h1>Specs dashboard</h1>
          <div class="muted" id="metadata">Loading specs from docs/specs...</div>
          <div class="muted" id="openStatus">Select a Markdown path to open that spec in a new agent session.</div>
        </div>
        <section class="summary" id="summary"></section>
        <section class="toolbar">
          <input id="search" type="search" placeholder="Search title, summary, status, or path" />
          <select id="statusFilter" aria-label="Filter by status">
            <option value="">All statuses</option>
          </select>
          <button id="refresh" type="button">Refresh</button>
        </section>
      </header>
      <section class="spec-grid" id="specs"></section>
    </main>
    <script>
      const state = { specs: [], statuses: {} };
      const metadata = document.getElementById("metadata");
      const summary = document.getElementById("summary");
      const specsContainer = document.getElementById("specs");
      const search = document.getElementById("search");
      const statusFilter = document.getElementById("statusFilter");
      const refresh = document.getElementById("refresh");
      const openStatus = document.getElementById("openStatus");

      function escapeHtml(value) {
        return String(value ?? "")
          .replaceAll("&", "&amp;")
          .replaceAll("<", "&lt;")
          .replaceAll(">", "&gt;")
          .replaceAll('"', "&quot;")
          .replaceAll("'", "&#39;");
      }

      function renderSummary(data) {
        const cards = [
          ["Total", data.total],
          ...Object.entries(data.statuses).sort(([left], [right]) => left.localeCompare(right)),
        ];

        summary.innerHTML = cards.map(([label, count]) =>
          '<article class="summary-card"><strong>' + escapeHtml(count) + '</strong><span class="muted">' + escapeHtml(label) + '</span></article>'
        ).join("");

        statusFilter.innerHTML = '<option value="">All statuses</option>' + Object.keys(data.statuses)
          .sort((left, right) => left.localeCompare(right))
          .map((status) => '<option value="' + escapeHtml(status) + '">' + escapeHtml(status) + '</option>')
          .join("");
      }

      function renderSpecs() {
        const query = search.value.trim().toLowerCase();
        const selectedStatus = statusFilter.value;
        const filtered = state.specs.filter((spec) => {
          const searchable = [spec.title, spec.about, spec.status, spec.path].join(" ").toLowerCase();
          return (!selectedStatus || spec.status === selectedStatus) && (!query || searchable.includes(query));
        });

        if (filtered.length === 0) {
          specsContainer.innerHTML = '<div class="empty">No specs match the current filters.</div>';
          return;
        }

        specsContainer.innerHTML = filtered.map((spec) =>
          '<article class="spec-card">' +
            '<span class="badge ' + escapeHtml(spec.statusKey) + '">' + escapeHtml(spec.status) + '</span>' +
            '<h2>' + escapeHtml(spec.title) + '</h2>' +
            '<p class="muted">' + escapeHtml(spec.about || "No summary available.") + '</p>' +
            '<div class="path"><button class="path-link" type="button" data-path="' + escapeHtml(spec.path) + '" title="Open this spec in a new agent session">' + escapeHtml(spec.path) + '</button></div>' +
            '<div class="card-actions">' +
              (spec.promptExists
                ? '<button class="secondary-action" type="button" data-prompt-path="' + escapeHtml(spec.promptPath) + '" title="Open this implementation prompt file in a new agent session">Open implementation prompts</button>'
                : '<button class="secondary-action" type="button" data-create-plan-path="' + escapeHtml(spec.path) + '" title="Open a new session to understand this spec, clear open questions, and generate the implementation prompt file">Create Implementation Plan</button>') +
            '</div>' +
            (spec.promptExists
              ? '<div class="path prompt-link"><button class="path-link" type="button" data-prompt-path="' + escapeHtml(spec.promptPath) + '">' + escapeHtml(spec.promptPath) + '</button></div>'
              : '<div class="muted">No implementation prompt file yet. Expected: <span class="path">' + escapeHtml(spec.promptPath) + '</span></div>') +
          '</article>'
        ).join("");
      }

      async function loadSpecs() {
        refresh.disabled = true;
        metadata.textContent = "Loading specs from docs/specs...";
        const response = await fetch("/api/specs", { cache: "no-store" });
        if (!response.ok) {
          throw new Error(await response.text());
        }

        const data = await response.json();
        state.specs = data.specs;
        state.statuses = data.statuses;
        renderSummary(data);
        renderSpecs();
        metadata.textContent = data.total + " specs loaded. Last refreshed " + new Date(data.generatedAt).toLocaleString() + ".";
        refresh.disabled = false;
      }

      search.addEventListener("input", renderSpecs);
      statusFilter.addEventListener("change", renderSpecs);
      specsContainer.addEventListener("click", async (event) => {
        const link = event.target.closest("[data-path], [data-prompt-path], [data-create-plan-path]");
        if (!link) {
          return;
        }

        link.disabled = true;
        const specPath = link.dataset.path;
        const promptPath = link.dataset.promptPath;
        const planPath = link.dataset.createPlanPath;
        const targetPath = specPath || promptPath || planPath;
        const endpoint = specPath ? "/api/open-spec" : promptPath ? "/api/open-prompt" : "/api/create-implementation-plan";
        const payload = specPath ? { path: specPath } : promptPath ? { path: promptPath } : { path: planPath };
        const action = specPath ? "a new agent session for" : promptPath ? "a new agent session for" : "a new implementation planning session for";

        openStatus.textContent = "Requesting " + action + " " + targetPath + "...";

        try {
          const response = await fetch(endpoint, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload),
          });

          const result = await response.json();
          if (!response.ok) {
            throw new Error(result.error || "Could not request a new session.");
          }

          openStatus.textContent = result.message + " " + result.path;
          if (endpoint === "/api/create-implementation-plan" && result.exists) {
            await loadSpecs();
          }
        } catch (error) {
          openStatus.textContent = "Request failed: " + error.message;
        } finally {
          link.disabled = false;
        }
      });
      refresh.addEventListener("click", () => loadSpecs().catch((error) => {
        metadata.textContent = "Could not refresh specs: " + error.message;
        refresh.disabled = false;
      }));

      loadSpecs().catch((error) => {
        metadata.textContent = "Could not load specs: " + error.message;
        refresh.disabled = false;
      });
    </script>
  </body>
</html>`;
}

async function startServer() {
    const server = createServer(async (req, res) => {
        try {
            const requestUrl = new URL(req.url ?? "/", "http://127.0.0.1");

            if (requestUrl.pathname === "/api/specs") {
                sendJson(res, await readSpecs());
                return;
            }

            if (requestUrl.pathname === "/api/open-spec" && req.method === "POST") {
                const input = await readRequestJson(req);
                if (typeof input.path !== "string" || !input.path.startsWith("docs/specs/") || !input.path.endsWith(".md")) {
                    sendBadRequest(res, "Expected a docs/specs Markdown path.");
                    return;
                }

                try {
                    sendJson(res, await openSpecForAgent(input.path));
                } catch (error) {
                    sendBadRequest(res, error.message);
                }
                return;
            }

            if (requestUrl.pathname === "/api/open-prompt" && req.method === "POST") {
                const input = await readRequestJson(req);
                if (typeof input.path !== "string" || !input.path.startsWith("docs/specs/") || !input.path.endsWith(".prompt.md")) {
                    sendBadRequest(res, "Expected a docs/specs implementation prompt Markdown path.");
                    return;
                }

                try {
                    sendJson(res, await openPromptForAgent(input.path));
                } catch (error) {
                    sendBadRequest(res, error.message);
                }
                return;
            }

            if (requestUrl.pathname === "/api/create-implementation-plan" && req.method === "POST") {
                const input = await readRequestJson(req);
                if (typeof input.path !== "string" || !input.path.startsWith("docs/specs/") || !input.path.endsWith(".md") || input.path.endsWith(".prompt.md")) {
                    sendBadRequest(res, "Expected a docs/specs specification Markdown path.");
                    return;
                }

                try {
                    sendJson(res, await requestImplementationPlan(input.path));
                } catch (error) {
                    sendBadRequest(res, error.message);
                }
                return;
            }

            res.writeHead(200, {
                "Content-Type": "text/html; charset=utf-8",
                "Cache-Control": "no-store",
            });
            res.end(renderHtml());
        } catch (error) {
            sendError(res, error);
        }
    });

    await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
    const address = server.address();
    const port = typeof address === "object" && address ? address.port : 0;
    return { server, url: `http://127.0.0.1:${port}/` };
}

const session = await joinSession({
    canvases: [
        createCanvas({
            id: "specs-dashboard",
            displayName: "Specs dashboard",
            description: "Dashboard showing spec statuses and summaries from docs/specs.",
            actions: [
                {
                    name: "refresh",
                    description: "Read docs/specs and return the current specs dashboard data.",
                    handler: async () => readSpecs(),
                },
            ],
            open: async (ctx) => {
                let entry = servers.get(ctx.instanceId);
                if (!entry) {
                    entry = await startServer();
                    servers.set(ctx.instanceId, entry);
                }

                const data = await readSpecs();
                return {
                    title: "Specs dashboard",
                    status: `${data.total} specs`,
                    url: entry.url,
                };
            },
            onClose: async (ctx) => {
                const entry = servers.get(ctx.instanceId);
                if (entry) {
                    servers.delete(ctx.instanceId);
                    await new Promise((resolve) => entry.server.close(() => resolve()));
                }
            },
        }),
    ],
});
