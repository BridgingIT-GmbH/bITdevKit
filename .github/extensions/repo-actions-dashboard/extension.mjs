import { spawn } from "node:child_process";
import { createServer } from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { CanvasError, createCanvas, joinSession } from "@github/copilot-sdk/extension";

const servers = new Map();
const extensionDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(extensionDirectory, "..", "..", "..");

const openInputSchema = {
    type: "object",
    additionalProperties: false,
    properties: {
        repo: { type: "string" },
        workflowLimit: { type: "integer", minimum: 1, maximum: 200 },
        runLimit: { type: "integer", minimum: 1, maximum: 100 },
    },
};

const workflowInputSchema = {
    type: "object",
    additionalProperties: false,
    required: ["workflowId"],
    properties: {
        workflowId: {
            oneOf: [{ type: "string" }, { type: "integer" }],
        },
    },
};

const runWorkflowInputSchema = {
    type: "object",
    additionalProperties: false,
    required: ["workflowId"],
    properties: {
        workflowId: {
            oneOf: [{ type: "string" }, { type: "integer" }],
        },
        ref: { type: "string" },
        inputs: {
            type: "object",
            additionalProperties: {
                oneOf: [{ type: "string" }, { type: "number" }, { type: "boolean" }],
            },
        },
    },
};

function normalizeConfig(input) {
    return {
        repo: typeof input?.repo === "string" && input.repo.trim() ? input.repo.trim() : "",
        workflowLimit: Number.isInteger(input?.workflowLimit) ? input.workflowLimit : 100,
        runLimit: Number.isInteger(input?.runLimit) ? input.runLimit : 25,
    };
}

async function resolveRepository(config) {
    const args = ["repo", "view"];
    if (config.repo) {
        args.push(config.repo);
    }

    args.push("--json", "nameWithOwner,defaultBranchRef,url");
    const { stdout } = await runGh(args);
    const repo = JSON.parse(stdout);

    return {
        nameWithOwner: repo.nameWithOwner,
        defaultBranch: repo.defaultBranchRef?.name || "main",
        url: repo.url,
        cwd: repositoryRoot,
    };
}

async function readState(config) {
    const repo = await resolveRepository(config);
    const [workflows, runs] = await Promise.all([
        listWorkflows(repo, config.workflowLimit),
        listRuns(repo, config.runLimit),
    ]);
    const latestRuns = new Map();

    for (const run of runs) {
        const key = String(run.workflowDatabaseId ?? "");
        if (key && !latestRuns.has(key)) {
            latestRuns.set(key, run);
        }
    }

    return {
        repo,
        workflows: workflows
            .slice()
            .sort((left, right) => left.name.localeCompare(right.name))
            .map((workflow) => ({
                ...workflow,
                latestRun: latestRuns.get(String(workflow.id)) ?? null,
            })),
        runs,
        updatedAt: new Date().toISOString(),
    };
}

async function listWorkflows(repo, limit) {
    const { stdout } = await runGh([
        "workflow",
        "list",
        "--all",
        "--json",
        "id,name,path,state",
        "-L",
        String(limit),
        "-R",
        repo.nameWithOwner,
    ]);

    return JSON.parse(stdout);
}

async function listRuns(repo, limit) {
    const { stdout } = await runGh([
        "run",
        "list",
        "--all",
        "--json",
        "attempt,conclusion,createdAt,databaseId,displayTitle,event,headBranch,headSha,name,number,startedAt,status,updatedAt,url,workflowDatabaseId,workflowName",
        "-L",
        String(limit),
        "-R",
        repo.nameWithOwner,
    ]);

    return JSON.parse(stdout);
}

async function getWorkflowDetails(entry, workflowId) {
    const state = await ensureState(entry);
    const workflow = findWorkflow(state.workflows, workflowId);
    if (!workflow) {
        throw new CanvasError("workflow_not_found", `Workflow '${workflowId}' was not found.`);
    }

    const { stdout } = await runGh([
        "workflow",
        "view",
        String(workflow.id),
        "--yaml",
        "--ref",
        state.repo.defaultBranch,
        "-R",
        state.repo.nameWithOwner,
    ]);

    return {
        repo: state.repo,
        workflow,
        dispatch: parseWorkflowDispatch(stdout),
        recentRuns: state.runs
            .filter((run) => String(run.workflowDatabaseId) === String(workflow.id))
            .slice(0, 10),
        workflowFileUrl: `${state.repo.url}/blob/${encodeURIComponent(state.repo.defaultBranch)}/${workflow.path}`,
        yaml: stdout,
    };
}

async function triggerWorkflow(entry, input) {
    const state = await ensureState(entry);
    const details = await getWorkflowDetails(entry, input.workflowId);
    if (!details.dispatch.supported) {
        throw new CanvasError("workflow_dispatch_unsupported", `Workflow '${details.workflow.name}' does not declare workflow_dispatch.`);
    }

    const ref = typeof input.ref === "string" && input.ref.trim() ? input.ref.trim() : state.repo.defaultBranch;
    const args = ["workflow", "run", String(details.workflow.id), "--ref", ref, "-R", state.repo.nameWithOwner];
    const inputs = sanitizeInputs(input.inputs);
    for (const [key, value] of Object.entries(inputs)) {
        args.push("-f", `${key}=${String(value)}`);
    }

    const { stdout } = await runGh(args);
    entry.state = await readState(entry.config);

    return {
        message: stdout.trim() || `Triggered workflow '${details.workflow.name}'.`,
        workflow: details.workflow,
        ref,
        inputs,
    };
}

function findWorkflow(workflows, workflowId) {
    const key = String(workflowId);
    return workflows.find((workflow) =>
        String(workflow.id) === key ||
        workflow.name === key ||
        workflow.path === key ||
        path.basename(workflow.path) === key);
}

function parseWorkflowDispatch(yaml) {
    const normalized = yaml.replace(/\r/g, "");
    if (!/(^|\n)\s*workflow_dispatch\s*:/m.test(normalized) && !/workflow_dispatch/.test(normalized.match(/(^|\n)\s*on\s*:\s*(.+)/m)?.[2] ?? "")) {
        return { supported: false, inputs: [] };
    }

    const inputs = [];
    const lines = normalized.split("\n");
    const inputsIndex = lines.findIndex((line) => /^\s*inputs\s*:\s*$/.test(line));
    if (inputsIndex < 0) {
        return { supported: true, inputs };
    }

    const baseIndent = countIndent(lines[inputsIndex]);
    let current = null;
    for (let index = inputsIndex + 1; index < lines.length; index++) {
        const line = lines[index];
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith("#")) {
            continue;
        }

        const indent = countIndent(line);
        if (indent <= baseIndent) {
            break;
        }

        const inputMatch = trimmed.match(/^([A-Za-z0-9_.-]+)\s*:/);
        if (indent === baseIndent + 2 && inputMatch) {
            current = {
                name: inputMatch[1],
                description: "",
                required: false,
                default: "",
                type: "string",
            };
            inputs.push(current);
            continue;
        }

        if (!current) {
            continue;
        }

        const separatorIndex = trimmed.indexOf(":");
        if (separatorIndex < 0) {
            continue;
        }

        const key = trimmed.slice(0, separatorIndex).trim();
        const value = unquote(trimmed.slice(separatorIndex + 1).trim());
        if (key === "description") {
            current.description = value;
        } else if (key === "required") {
            current.required = value === "true";
        } else if (key === "default") {
            current.default = value;
        } else if (key === "type") {
            current.type = value || "string";
        }
    }

    return { supported: true, inputs };
}

function countIndent(value) {
    return value.match(/^\s*/)?.[0].length ?? 0;
}

function unquote(value) {
    return value.replace(/^["']|["']$/g, "");
}

function sanitizeInputs(inputs) {
    if (!inputs || typeof inputs !== "object" || Array.isArray(inputs)) {
        return {};
    }

    return Object.fromEntries(Object.entries(inputs).filter(([, value]) =>
        ["string", "number", "boolean"].includes(typeof value)));
}

async function ensureState(entry, refresh = false) {
    if (!entry.state || refresh) {
        entry.state = await readState(entry.config);
    }

    return entry.state;
}

function runGh(args) {
    return new Promise((resolve, reject) => {
        const child = spawn("gh", args, {
            cwd: repositoryRoot,
            windowsHide: true,
            env: {
                ...process.env,
                GH_PAGER: "cat",
            },
        });

        let stdout = "";
        let stderr = "";
        child.stdout.on("data", (chunk) => {
            stdout += chunk.toString();
        });
        child.stderr.on("data", (chunk) => {
            stderr += chunk.toString();
        });
        child.on("error", reject);
        child.on("close", (code) => {
            if (code === 0) {
                resolve({ stdout, stderr });
                return;
            }

            reject(new Error(stderr.trim() || stdout.trim() || `gh exited with code ${code}`));
        });
    });
}

function sendJson(res, value, statusCode = 200) {
    res.writeHead(statusCode, {
        "Content-Type": "application/json; charset=utf-8",
        "Cache-Control": "no-store",
    });
    res.end(JSON.stringify(value));
}

function sendHtml(res, html) {
    res.writeHead(200, {
        "Content-Type": "text/html; charset=utf-8",
        "Cache-Control": "no-store",
    });
    res.end(html);
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

async function startServer(instanceId, config) {
    const entry = {
        instanceId,
        config,
        state: null,
        server: null,
        url: "",
    };

    entry.server = createServer(async (req, res) => {
        try {
            const requestUrl = new URL(req.url ?? "/", "http://127.0.0.1");

            if (req.method === "GET" && requestUrl.pathname === "/") {
                sendHtml(res, renderHtml());
                return;
            }

            if (req.method === "GET" && requestUrl.pathname === "/api/state") {
                sendJson(res, await ensureState(entry, requestUrl.searchParams.get("refresh") === "1"));
                return;
            }

            if (req.method === "GET" && requestUrl.pathname === "/api/workflow") {
                const workflowId = requestUrl.searchParams.get("id");
                if (!workflowId) {
                    sendJson(res, { error: "A workflow id is required." }, 400);
                    return;
                }

                sendJson(res, await getWorkflowDetails(entry, workflowId));
                return;
            }

            if (req.method === "POST" && requestUrl.pathname === "/api/run") {
                sendJson(res, await triggerWorkflow(entry, await readRequestJson(req)));
                return;
            }

            sendJson(res, { error: "Not found." }, 404);
        } catch (error) {
            sendJson(res, { error: error.message || "Request failed." }, 500);
        }
    });

    await new Promise((resolve) => entry.server.listen(0, "127.0.0.1", resolve));
    const address = entry.server.address();
    const port = typeof address === "object" && address ? address.port : 0;
    entry.url = `http://127.0.0.1:${port}/`;
    return entry;
}

function renderHtml() {
    return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Repo Actions Dashboard</title>
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
          repeating-linear-gradient(to bottom, rgba(248,248,242,.035), rgba(248,248,242,.035) 1px, transparent 1px, transparent 4px),
          linear-gradient(90deg, rgba(248,248,242,.018) 1px, transparent 1px);
        background-size: auto, 48px 48px;
        opacity: .45;
      }

      button,
      input,
      textarea {
        font: inherit;
      }

      a {
        color: var(--bs-primary);
      }

      .layout {
        display: grid;
        grid-template-columns: minmax(15rem, 18rem) minmax(0, 1fr);
        min-height: 100vh;
      }

      .sidebar {
        height: 100vh;
        overflow: auto;
        position: sticky;
        top: 0;
        background: #111010;
        border-right: 1px solid var(--bs-border-color);
        padding: .5rem .35rem 1rem;
      }

      .sidebar-header {
        min-height: 3.15rem;
        padding: .35rem .55rem .75rem;
        border-bottom: 1px solid var(--bs-border-color);
        margin-bottom: .5rem;
      }

      .kicker {
        margin: 0 0 .35rem;
        color: var(--bs-secondary-color);
        font-size: .64rem;
        font-weight: 700;
        letter-spacing: .05em;
        text-transform: uppercase;
      }

      h1,
      h2,
      h3 {
        margin: 0;
        color: var(--bs-emphasis-color);
      }

      h1 {
        font-size: 1rem;
        line-height: 1.25;
      }

      h2 {
        font-size: 1.05rem;
      }

      .muted {
        color: var(--bs-secondary-color);
      }

      .workflow-list {
        display: grid;
        gap: .15rem;
      }

      .workflow-link {
        border: 0;
        border-radius: .1rem;
        background: transparent;
        color: var(--bs-body-color);
        cursor: pointer;
        display: grid;
        gap: .05rem;
        padding: .34rem .6rem;
        text-align: left;
        width: 100%;
      }

      .workflow-link:hover,
      .workflow-link.active {
        color: var(--dashboard-active-fg);
        background: var(--dashboard-active-bg);
      }

      .workflow-link span {
        color: var(--bs-secondary-color);
        font-size: .72rem;
      }

      .content {
        display: grid;
        gap: 1rem;
        padding: 1rem;
      }

      .panel,
      .repo-card,
      .detail-card,
      .run-card {
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .125rem;
        background: var(--dashboard-surface);
      }

      .panel,
      .repo-card {
        padding: 1rem;
      }

      .toolbar,
      .meta,
      .actions {
        display: flex;
        flex-wrap: wrap;
        gap: .5rem;
        align-items: center;
      }

      .toolbar {
        justify-content: space-between;
      }

      .button {
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .1rem;
        background: var(--dashboard-surface-muted);
        color: var(--bs-body-color);
        cursor: pointer;
        font-weight: 700;
        min-height: 2.15rem;
        padding: .35rem .65rem;
      }

      .button:hover,
      .button:focus {
        border-color: rgba(var(--bs-secondary-rgb), .6);
        color: var(--dashboard-active-fg);
      }

      .button.primary {
        border-color: rgba(var(--bs-secondary-rgb), .35);
        background: var(--dashboard-active-bg);
        color: var(--dashboard-active-fg);
      }

      .summary {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: .5rem;
        margin-top: .75rem;
      }

      .summary-card,
      .detail-card,
      .run-card {
        background: var(--dashboard-surface-muted);
        padding: .75rem;
      }

      .summary-card strong {
        color: var(--bs-primary);
        display: block;
        font-size: 1.45rem;
        line-height: 1.2;
      }

      .grid {
        display: grid;
        grid-template-columns: minmax(0, 1fr) minmax(18rem, .55fr);
        gap: 1rem;
        align-items: start;
      }

      .runs,
      .details {
        display: grid;
        gap: .75rem;
      }

      .pill {
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .1rem;
        background: var(--dashboard-surface-muted);
        color: var(--bs-secondary-color);
        display: inline-flex;
        font-size: .72rem;
        font-weight: 700;
        line-height: 1;
        padding: .28rem .45rem;
      }

      .pill.success { color: var(--bs-success); }
      .pill.failure { color: var(--bs-danger); }
      .pill.running { color: var(--bs-primary); }
      .pill.warning { color: var(--bs-warning); }

      .code {
        background: var(--dashboard-code-bg);
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .125rem;
        color: var(--bs-secondary-color);
        max-height: 18rem;
        overflow: auto;
        padding: .75rem;
        white-space: pre-wrap;
      }

      dialog {
        width: min(44rem, calc(100vw - 2rem));
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .125rem;
        background: var(--dashboard-surface);
        color: var(--bs-body-color);
        padding: 0;
      }

      dialog::backdrop {
        background: rgba(0, 0, 0, .55);
      }

      .dialog-body {
        display: grid;
        gap: .75rem;
        padding: 1rem;
      }

      label {
        display: grid;
        gap: .35rem;
      }

      input,
      textarea {
        border: 1px solid var(--dashboard-panel-border);
        border-radius: .1rem;
        background: var(--dashboard-code-bg);
        color: var(--bs-body-color);
        padding: .45rem .55rem;
      }

      textarea {
        min-height: 6rem;
      }

      .empty,
      .status {
        color: var(--bs-secondary-color);
      }

      @media (max-width: 960px) {
        .layout,
        .grid {
          grid-template-columns: 1fr;
        }

        .sidebar {
          height: auto;
          max-height: 22rem;
          position: static;
        }
      }
    </style>
  </head>
  <body>
    <main class="layout">
      <aside class="sidebar">
        <div class="sidebar-header">
          <p class="kicker">GitHub Actions</p>
          <h1>Repo Actions Hub</h1>
          <div class="muted" id="sidebar-meta">Loading workflows...</div>
        </div>
        <div class="workflow-list" id="workflows"></div>
      </aside>

      <section class="content">
        <section class="panel toolbar">
          <div>
            <p class="kicker">Repository</p>
            <h2 id="repo-title">Loading...</h2>
            <div class="muted" id="repo-meta"></div>
          </div>
          <button class="button primary" id="refresh" type="button">Refresh</button>
        </section>

        <section class="repo-card" id="repo-card"></section>

        <section class="grid">
          <section class="panel">
            <p class="kicker">Workflow details</p>
            <div class="details" id="details">
              <div class="empty">Select a workflow to inspect dispatch support, YAML, and recent runs.</div>
            </div>
          </section>
          <section class="panel">
            <p class="kicker">Recent runs</p>
            <div class="runs" id="runs"></div>
          </section>
        </section>

        <div class="status" id="status"></div>
      </section>
    </main>

    <dialog id="run-dialog">
      <form class="dialog-body" id="run-form" method="dialog">
        <div>
          <p class="kicker">workflow_dispatch</p>
          <h2 id="dialog-title">Run workflow</h2>
          <div class="muted" id="dialog-subtitle"></div>
        </div>
        <label>
          <span>Ref</span>
          <input id="run-ref" name="ref" />
        </label>
        <label>
          <span>Inputs as JSON</span>
          <textarea id="run-inputs" placeholder='{"name":"value"}'></textarea>
        </label>
        <div class="actions">
          <button class="button" id="cancel-run" type="button">Cancel</button>
          <button class="button primary" type="submit">Run workflow</button>
        </div>
      </form>
    </dialog>

    <script>
      const state = { data: null, selectedWorkflowId: null, selectedDetails: null };
      const elements = {
        workflows: document.getElementById("workflows"),
        runs: document.getElementById("runs"),
        details: document.getElementById("details"),
        refresh: document.getElementById("refresh"),
        status: document.getElementById("status"),
        repoTitle: document.getElementById("repo-title"),
        repoMeta: document.getElementById("repo-meta"),
        repoCard: document.getElementById("repo-card"),
        sidebarMeta: document.getElementById("sidebar-meta"),
        runDialog: document.getElementById("run-dialog"),
        runForm: document.getElementById("run-form"),
        runRef: document.getElementById("run-ref"),
        runInputs: document.getElementById("run-inputs"),
        dialogTitle: document.getElementById("dialog-title"),
        dialogSubtitle: document.getElementById("dialog-subtitle"),
        cancelRun: document.getElementById("cancel-run"),
      };

      function escapeHtml(value) {
        return String(value ?? "")
          .replaceAll("&", "&amp;")
          .replaceAll("<", "&lt;")
          .replaceAll(">", "&gt;")
          .replaceAll('"', "&quot;")
          .replaceAll("'", "&#39;");
      }

      async function request(url, options = {}) {
        const response = await fetch(url, { headers: { "Content-Type": "application/json" }, ...options });
        const payload = await response.json();
        if (!response.ok) {
          throw new Error(payload.error || "Request failed.");
        }
        return payload;
      }

      function runClass(run) {
        if (!run) return "warning";
        if (run.status === "in_progress" || run.status === "queued") return "running";
        if (run.conclusion === "success") return "success";
        if (run.conclusion === "failure" || run.conclusion === "cancelled") return "failure";
        return "warning";
      }

      function runLabel(run) {
        if (!run) return "no runs";
        return run.conclusion || run.status || "unknown";
      }

      function summarize(data) {
        const running = data.runs.filter((run) => run.status === "in_progress" || run.status === "queued").length;
        const failures = data.runs.filter((run) => run.conclusion === "failure").length;
        return { workflows: data.workflows.length, running, failures };
      }

      function render() {
        const data = state.data;
        if (!data) return;
        const summary = summarize(data);
        elements.repoTitle.textContent = data.repo.nameWithOwner;
        elements.repoMeta.textContent = "Default branch " + data.repo.defaultBranch + " - updated " + new Date(data.updatedAt).toLocaleString();
        elements.sidebarMeta.textContent = data.workflows.length + " workflows";
        elements.repoCard.innerHTML =
          '<div class="meta"><a href="' + escapeHtml(data.repo.url) + '" target="_blank" rel="noreferrer">Open repository</a><span>Default branch: <strong>' + escapeHtml(data.repo.defaultBranch) + '</strong></span></div>' +
          '<div class="summary">' +
            '<article class="summary-card"><strong>' + summary.workflows + '</strong><span class="muted">Workflows</span></article>' +
            '<article class="summary-card"><strong>' + summary.running + '</strong><span class="muted">Running or queued</span></article>' +
            '<article class="summary-card"><strong>' + summary.failures + '</strong><span class="muted">Recent failures</span></article>' +
          '</div>';
        elements.workflows.innerHTML = data.workflows.map((workflow) =>
          '<button class="workflow-link ' + (String(workflow.id) === String(state.selectedWorkflowId) ? 'active' : '') + '" type="button" data-workflow-id="' + escapeHtml(workflow.id) + '">' +
            '<strong>' + escapeHtml(workflow.name) + '</strong>' +
            '<span>' + escapeHtml(workflow.path) + '</span>' +
            '<span class="pill ' + runClass(workflow.latestRun) + '">' + escapeHtml(runLabel(workflow.latestRun)) + '</span>' +
          '</button>').join("") || '<div class="empty">No workflows found.</div>';
        elements.runs.innerHTML = data.runs.map((run) =>
          '<article class="run-card">' +
            '<div class="meta"><span class="pill ' + runClass(run) + '">' + escapeHtml(runLabel(run)) + '</span><span>#' + escapeHtml(run.number) + '</span><span>' + escapeHtml(run.workflowName || run.name) + '</span></div>' +
            '<strong>' + escapeHtml(run.displayTitle || run.name) + '</strong>' +
            '<div class="muted">' + escapeHtml(run.headBranch || "") + ' - ' + escapeHtml(run.event || "") + ' - ' + escapeHtml(new Date(run.createdAt).toLocaleString()) + '</div>' +
            '<div><a href="' + escapeHtml(run.url) + '" target="_blank" rel="noreferrer">Open run</a></div>' +
          '</article>').join("") || '<div class="empty">No recent runs found.</div>';
      }

      async function load(refresh) {
        elements.status.textContent = refresh ? "Refreshing GitHub Actions..." : "Loading GitHub Actions...";
        try {
          state.data = await request("/api/state" + (refresh ? "?refresh=1" : ""));
          if (!state.selectedWorkflowId && state.data.workflows[0]) {
            state.selectedWorkflowId = state.data.workflows[0].id;
            await loadWorkflow(state.selectedWorkflowId, false);
          }
          render();
          elements.status.textContent = "";
        } catch (error) {
          elements.status.textContent = "Error: " + error.message;
        }
      }

      async function loadWorkflow(workflowId, rerender = true) {
        state.selectedWorkflowId = workflowId;
        elements.details.innerHTML = '<div class="empty">Loading workflow details...</div>';
        if (rerender) render();
        try {
          state.selectedDetails = await request("/api/workflow?id=" + encodeURIComponent(workflowId));
          renderDetails();
        } catch (error) {
          elements.details.innerHTML = '<div class="empty">Error: ' + escapeHtml(error.message) + '</div>';
        }
      }

      function renderDetails() {
        const details = state.selectedDetails;
        const dispatch = details.dispatch.supported
          ? '<span class="pill success">workflow_dispatch</span>'
          : '<span class="pill failure">not dispatchable</span>';
        const inputs = details.dispatch.inputs.length
          ? details.dispatch.inputs.map((input) => '<li><strong>' + escapeHtml(input.name) + '</strong> ' + escapeHtml(input.required ? "required" : "optional") + ' <span class="muted">' + escapeHtml(input.description || input.type) + '</span></li>').join("")
          : '<li class="muted">No declared inputs.</li>';
        const runs = details.recentRuns.map((run) => '<li><span class="pill ' + runClass(run) + '">' + escapeHtml(runLabel(run)) + '</span> ' + escapeHtml(run.displayTitle || run.name) + '</li>').join("");
        elements.details.innerHTML =
          '<article class="detail-card">' +
            '<div class="meta">' + dispatch + '<a href="' + escapeHtml(details.workflowFileUrl) + '" target="_blank" rel="noreferrer">Open workflow file</a></div>' +
            '<h2>' + escapeHtml(details.workflow.name) + '</h2>' +
            '<div class="muted">' + escapeHtml(details.workflow.path) + '</div>' +
            '<div class="actions">' + (details.dispatch.supported ? '<button class="button primary" id="open-run-dialog" type="button">Run workflow</button>' : '') + '</div>' +
          '</article>' +
          '<article class="detail-card"><p class="kicker">Inputs</p><ul>' + inputs + '</ul></article>' +
          '<article class="detail-card"><p class="kicker">Recent runs</p><ul>' + (runs || '<li class="muted">No runs for this workflow.</li>') + '</ul></article>' +
          '<pre class="code">' + escapeHtml(details.yaml) + '</pre>';
        document.getElementById("open-run-dialog")?.addEventListener("click", openRunDialog);
      }

      function openRunDialog() {
        const details = state.selectedDetails;
        elements.dialogTitle.textContent = "Run " + details.workflow.name;
        elements.dialogSubtitle.textContent = details.workflow.path;
        elements.runRef.value = state.data.repo.defaultBranch;
        elements.runInputs.value = "{}";
        elements.runDialog.showModal();
      }

      elements.workflows.addEventListener("click", (event) => {
        const button = event.target.closest("[data-workflow-id]");
        if (button) {
          loadWorkflow(button.dataset.workflowId);
        }
      });
      elements.refresh.addEventListener("click", () => load(true));
      elements.cancelRun.addEventListener("click", () => elements.runDialog.close());
      elements.runForm.addEventListener("submit", async (event) => {
        event.preventDefault();
        try {
          const inputs = elements.runInputs.value.trim() ? JSON.parse(elements.runInputs.value) : {};
          const result = await request("/api/run", {
            method: "POST",
            body: JSON.stringify({
              workflowId: state.selectedWorkflowId,
              ref: elements.runRef.value,
              inputs,
            }),
          });
          elements.runDialog.close();
          elements.status.textContent = result.message;
          await load(true);
          await loadWorkflow(state.selectedWorkflowId);
        } catch (error) {
          elements.status.textContent = "Run failed: " + error.message;
        }
      });

      load(false);
    </script>
  </body>
</html>`;
}

const session = await joinSession({
    canvases: [
        createCanvas({
            id: "repo-actions-hub",
            displayName: "Repo Actions Hub",
            description: "Browse GitHub Actions workflows for the current repository, inspect recent runs, and trigger workflow_dispatch runs.",
            inputSchema: openInputSchema,
            actions: [
                {
                    name: "get_state",
                    description: "Return the current workflow and recent run state for the canvas repository.",
                    handler: async (ctx) => ensureState(requireEntry(ctx.instanceId)),
                },
                {
                    name: "refresh",
                    description: "Refresh workflows and recent runs for the canvas repository.",
                    handler: async (ctx) => ensureState(requireEntry(ctx.instanceId), true),
                },
                {
                    name: "get_workflow_details",
                    description: "Inspect a workflow and report workflow_dispatch support and inputs.",
                    inputSchema: workflowInputSchema,
                    handler: async (ctx) => getWorkflowDetails(requireEntry(ctx.instanceId), ctx.input.workflowId),
                },
                {
                    name: "run_workflow",
                    description: "Trigger a workflow_dispatch run for a workflow in the current repository.",
                    inputSchema: runWorkflowInputSchema,
                    handler: async (ctx) => triggerWorkflow(requireEntry(ctx.instanceId), ctx.input),
                },
            ],
            open: async (ctx) => {
                const config = normalizeConfig(ctx.input);
                let entry = servers.get(ctx.instanceId);
                if (!entry) {
                    entry = await startServer(ctx.instanceId, config);
                    servers.set(ctx.instanceId, entry);
                } else {
                    entry.config = config;
                }

                const state = await ensureState(entry, true);
                return {
                    title: "Repo Actions Hub",
                    status: state.repo.nameWithOwner,
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

function requireEntry(instanceId) {
    const entry = servers.get(instanceId);
    if (!entry) {
        throw new CanvasError("canvas_instance_missing", `No canvas instance is open for '${instanceId}'.`);
    }

    return entry;
}
