"""Render the shared dashboard logo and canonical feature SVGs in documentation."""

from html import escape, unescape
from html.parser import HTMLParser
import json
from pathlib import Path
import posixpath
import re
import xml.etree.ElementTree as ET
from urllib.parse import urljoin, urlsplit

from markupsafe import Markup
from mkdocs.plugins import get_plugin_logger


ROOT = Path(__file__).resolve().parents[3]
ASSETS = ROOT / "docs/assets/features"
SVG_NS = "http://www.w3.org/2000/svg"
ET.register_namespace("", SVG_NS)
IMAGE = re.compile(r'<img\b(?P<attributes>[^>]*\bclass="bdk-doc-icon"[^>]*)>')
TITLE = re.compile(
    r'^(?P<icons>(?:<img\b[^>]*\bclass="bdk-doc-icon"[^>]*> *)+)\r?\n\r?\n# (?P<title>[^\r\n]+)',
    re.MULTILINE,
)
ANCHOR = re.compile(r'<a\b(?P<attributes>[^>]*)>(?P<label>.*?)</a>', re.DOTALL)
LINK = re.compile(
    r'\]\((?P<target>[^)\s#]+?\.md)(?:#[^)\s]*)?(?:\s+"[^"]*")?\)'
    r'|<a\b[^>]*\bhref="(?P<href>[^"#?]+)[^"]*"'
)
GUIDE_CHIP = re.compile(
    r'<b>Guide</b>\s*(?:<a\b[^>]*\bhref="(?P<href>[^"]+)"|\[[^\]]*\]\((?P<target>[^)\s]+))'
)
FRONT_MATTER = re.compile(r'\A---[ \t]*\r?\n(?P<body>.*?)\r?\n---[ \t]*(?:\r?\n|\Z)', re.DOTALL)
TITLE_FIELD = re.compile(r'^title:[ \t]*(?P<title>[^\r\n]+)', re.MULTILINE)
HEADING = re.compile(r'^(?P<level>#{1,3})[ \t]+(?P<text>[^\r\n]*?)[ \t]*$', re.MULTILINE)
ATTRIBUTE_LIST = re.compile(r'^(?P<text>.*?)[ \t]*\{(?P<attributes>[^}]*)\}$')
FENCE = re.compile(r'^[ \t]*(?P<fence>`{3,}|~{3,})')
MARKER = re.compile(
    r'^(?P<indent>[ \t]*)<!--[ \t]*agent-prompt\b(?P<arguments>.*?)-->[ \t]*$'
)
PROMPT_ARGUMENTS = re.compile(r'^[ \t]*(?P<step>\d+)[ \t]+(?P<kind>topics|source)="(?P<value>[^"]+)"[ \t]*$')
H1 = re.compile(r'<h1\b[^>]*>.*?</h1>', re.DOTALL)
H2 = re.compile(r'<h2\b[^>]*>.*?</h2>', re.DOTALL)
ICON_SPAN = re.compile(r'<span\b[^>]*\bclass="bdk-feature-icon"[^>]*>.*?</span>', re.DOTALL)
FEATURE_ALIASES = {
    "pipelines": ("Pipeline",),
    "orchestrations": ("Orchestration",),
    "repositories": ("Repository", "Domain repositories"),
    "active-entity": ("ActiveEntity",),
    "domain": ("Domain", "Domain modeling"),
    "change-history": ("Domain change history", "ChangeHistory"),
    "storage-files": ("FileStorage",),
    "storage-blobs": ("BlobStorage",),
    "storage-documents": ("DocumentStorage",),
    "job-scheduling": ("Job scheduling",),
    "commands-queries": ("Application commands and queries",),
    "results": ("Result",),
}
TUTORIAL_STEP_COUNT = 10
PROMPT_TEMPLATE = (
    'Do step {step}, "{heading}", of the guide "Build an app with DevKit" in this application, and nothing else.',
    "Use the bdk MCP connection from step 2. If it is unavailable, reconnect using step 2 before editing.",
    "Read step {step} with bdk_docs_get and source docs/site/getting-started-existing-app.md.",
    "{guidance}",
    "Use bdk_docs_search for additional feature guides. Verify DevKit signatures with bdk_api_search, then "
    "bdk_api_get using the returned uid. If documentation is truncated, read the full document from its returned URL.",
    "Read devkit-plan.md for this app's names and rules. If it does not exist, run the planning prompt "
    'from the section "Use an agent" in the guide, and wait for my approval.',
    "If an earlier step is unchecked in the plan, stop and tell me.",
    "Make the change with this app's names from the plan. Build the application or solution, run the step's check from the plan, "
    "and stop the app.",
    "Tick step {step} in the plan and report the result. Leave the changes uncommitted for my review.",
)
MINIMAL_APP_PROMPT_TEMPLATE = (
    'Do step {step}, "{heading}", of the guide "Build an app with DevKit".',
    "Read https://bridgingit-gmbh.github.io/bITdevKit/getting-started-existing-app/#step-1.",
    "Use the existing ASP.NET Core host if one is present. Otherwise create the plain .NET 10 web project "
    "shown in the guide. Keep existing working features. Do not add an entity, DbContext, repository, or database yet.",
    "Build the application, start it in Development, check its root route, and stop the process. "
    "Report the project, namespace, URL, and check result. Leave changes uncommitted for review.",
)
MCP_SETUP_PROMPT_TEMPLATE = (
    'Set up step {step}, "{heading}", of the guide "Build an app with DevKit" in this repository. Do not change application code.',
    "Read https://bridgingit-gmbh.github.io/bITdevKit/getting-started-existing-app/#step-{step}.",
    "Inspect the local tool manifest and MCP client configuration. Install or restore BridgingIT.DevKit.Cli "
    "and merge the bdk server into the client's configuration, preserving existing tools and servers. "
    "Run dotnet tool run bdk mcp from this repository root through the MCP client.",
    "If the client needs a reload, report how to reload it and resume verification after reconnecting. "
    "Do not claim the connection works until the tools respond.",
    "Call bdk_mcp_status and check the workspace. Call bdk_docs_search with query host builder, read a result "
    "with bdk_docs_get, and call bdk_guidance_list. These tools need no running DevKit host.",
    "{guidance}",
    "If documentation is truncated, read the full document from its returned URL.",
    "The starting application needs only its existing route from step 1. No database, entity, or customer "
    "endpoints are required. Keep application code unchanged.",
    "Report the configuration files and check results. If devkit-plan.md exists, tick step {step} only after "
    "all checks pass. Leave changes uncommitted for my review. Do not apply later steps.",
)
FEATURES_PAGE = "getting-started-existing-app-features.md"
log = get_plugin_logger("feature_icons")
catalog = []
drawings = {}
navigation = {}
source_urls = {}
decision_links = {}
recipe_links = {}
feature_aliases = {}


def attribute(attributes, name):
    """Read a double-quoted attribute from markup authored by this integration."""
    match = re.search(rf'\b{re.escape(name)}="([^"]*)"', attributes)
    return unescape(match[1]) if match else None


def icon_html(icon_id, size=24):
    """Return a decorative SVG; its adjacent text supplies the accessible name."""
    return (
        f'<span class="bdk-feature-icon" style="--bdk-feature-icon-size:{size}px" '
        f'data-feature-icon="{icon_id}" aria-hidden="true">{drawings[icon_id]}</span>'
    )


def feature_name_html(icon_id, name, size):
    """Keep the icon with the first word, allowing longer feature names to wrap."""
    first, space, rest = name.partition(" ")
    return (
        f'<span class="bdk-feature-name" data-feature-name="{icon_id}">'
        f'{icon_html(icon_id, size)}{escape(first)}</span>{space}{escape(rest)}'
    )


class FeatureNameDecorator(HTMLParser):
    """Prefix named features in text without touching code, attributes, or existing icons."""

    void_tags = {"area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"}
    literal_tags = {"code", "pre", "script", "style", "svg", "textarea"}

    def __init__(self, aliases, size=18, labels_only=False, skip_headings=False):
        super().__init__(convert_charrefs=False)
        self.aliases = aliases
        names = sorted(aliases, key=len, reverse=True)
        self.pattern = re.compile(r"(?<![\w-])(?:" + "|".join(re.escape(name) for name in names) + r")(?![\w-])", re.IGNORECASE) if names else None
        self.size = size
        self.labels_only = labels_only
        self.skip_headings = skip_headings
        self.frames = []
        self.parts = []
        self.previous_icon = None
        self.matches = 0

    def handle_starttag(self, tag, attrs):
        self.parts.append(self.get_starttag_text())
        attributes = dict(attrs)
        classes = attributes.get("class", "").split()
        blocked = bool(self.frames and self.frames[-1][1])
        blocked |= tag in self.literal_tags or bool({"bdk-feature-icon", "bdk-feature-name", "headerlink", "mermaid", "prompt"}.intersection(classes))
        blocked |= attributes.get("data-feature-icons") == "false"
        blocked |= self.skip_headings and re.fullmatch(r"h[1-6]", tag) is not None
        label = self.labels_only or tag in {"a", "th", "dt", "strong", "summary"} or bool(self.frames and self.frames[-1][2])
        if tag not in self.void_tags:
            self.frames.append((tag, blocked, label, attributes.get("data-feature-icon"), "bdk-feature-name" in classes))

    def handle_startendtag(self, tag, attrs):
        self.parts.append(self.get_starttag_text())

    def handle_endtag(self, tag):
        self.parts.append(f"</{tag}>")
        for index in range(len(self.frames) - 1, -1, -1):
            if self.frames[index][0] == tag:
                frame = self.frames[index]
                del self.frames[index:]
                if frame[3]:
                    self.previous_icon = frame[3]
                elif frame[4] or tag in {"p", "li", "td", "th", "a", "div", "section"}:
                    self.previous_icon = None
                break

    def handle_data(self, data):
        if not self.pattern or not data.strip() or self.frames and self.frames[-1][1]:
            self.parts.append(data)
            return

        def replace(match):
            if not (self.labels_only or self.frames and self.frames[-1][2] or match[0][0].isupper()):
                return match[0]
            icon_id = self.aliases[match[0].casefold()]
            if self.previous_icon == icon_id:
                self.previous_icon = None
                return match[0]
            self.matches += 1
            return feature_name_html(icon_id, match[0], self.size)

        self.parts.append(self.pattern.sub(replace, data))
        self.previous_icon = None

    def handle_entityref(self, name):
        self.parts.append(f"&{name};")

    def handle_charref(self, name):
        self.parts.append(f"&#{name};")

    def handle_comment(self, data):
        self.parts.append(f"<!--{data}-->")

    def render(self, markup):
        self.feed(markup)
        self.close()
        return "".join(self.parts)


def feature_label_html(label, ids=(), size=18):
    """Place each label icon next to its feature, retaining one icon for generic titles."""
    icons = list(ICON_SPAN.finditer(label))
    intended = list(ids) or [attribute(match[0], "data-feature-icon") for match in icons]
    aliases = {name: icon_id for name, icon_id in feature_aliases.items() if not intended or icon_id in intended}
    plain = ICON_SPAN.sub("", label)
    decorator = FeatureNameDecorator(aliases, size, labels_only=True)
    decorated = decorator.render(plain)
    if decorator.matches:
        return decorated
    fallback = intended[0] if intended else None
    return (icon_html(fallback, size) if fallback else "") + plain


def decorate_feature_names(html):
    """Apply the feature-name pattern to headings, links, tables, and prose."""
    def heading(match):
        if attribute(match["start"], "data-feature-icons") == "false":
            return match[0]
        level = int(match["level"])
        size = 32 if level == 1 else 24
        return match["start"] + feature_label_html(match["label"], size=size) + match["end"]

    html = re.sub(
        r'(?P<start><h(?P<level>[1-6])\b[^>]*>)(?P<label>.*?)(?P<end></h[1-6]>)',
        heading, html, flags=re.DOTALL,
    )
    return FeatureNameDecorator(feature_aliases, skip_headings=True).render(html)


def on_pre_build(config):
    """Refresh source data and stage only public SVG and metadata assets."""
    global catalog, drawings, feature_aliases
    catalog = json.loads((ASSETS / "catalog.json").read_text(encoding="utf-8"))["icons"]
    feature_aliases = {
        name.casefold(): icon["id"]
        for icon in catalog
        for name in (icon["title"], *FEATURE_ALIASES.get(icon["id"], ()))
    }
    drawings = {}
    target = Path(config.docs_dir) / "assets/features"
    target.mkdir(parents=True, exist_ok=True)
    names = {f'{icon["id"]}.svg' for icon in catalog} | {"features.svg", "catalog.json"}
    for name in names:
        data = (ASSETS / name).read_bytes()
        destination = target / name
        if not destination.exists() or destination.read_bytes() != data:
            destination.write_bytes(data)
    for obsolete in target.glob("*.svg"):
        if obsolete.name not in names:
            obsolete.unlink()
    for icon in catalog:
        svg = ET.fromstring((ASSETS / f'{icon["id"]}.svg').read_text(encoding="utf-8"))
        for title in list(svg.findall(f"{{{SVG_NS}}}title")):
            svg.remove(title)
        svg.attrib.pop("aria-labelledby", None)
        svg.attrib.pop("role", None)
        svg.set("aria-hidden", "true")
        svg.set("focusable", "false")
        # Markdown headings must remain one physical line after expansion.
        for node in svg.iter():
            if node.text and not node.text.strip():
                node.text = None
            if node.tail and not node.tail.strip():
                node.tail = None
        drawings[icon["id"]] = ET.tostring(svg, encoding="unicode")


def on_nav(nav, config, files):
    """Use each guide's actual title icons for its navigation entry and collect guide back-links."""
    global navigation, source_urls, decision_links, recipe_links
    navigation = {}
    source_urls = {}
    decision_links = {}
    recipe_links = {}
    sources = []
    for page in nav.pages:
        source = page.file.src_uri
        source_urls[source] = page.url
        content = Path(page.file.abs_src_path).read_text(encoding="utf-8-sig")
        sources.append((page, content))
        heading = TITLE.search(content)
        if heading:
            ids = []
            for match in IMAGE.finditer(heading["icons"]):
                icon_id = Path(attribute(match["attributes"], "src")).stem
                if icon_id not in drawings:
                    raise ValueError(f"Unknown title icon {icon_id} in {source}")
                ids.append(icon_id)
            if ids:
                navigation[page.url] = ids
                if source.startswith("reference/") and len(ids) == 1:
                    feature_aliases.setdefault(plain_text(heading["title"]).casefold(), ids[0])
    for page, content in sources:
        source = page.file.src_uri
        if re.fullmatch(r"decisions-[^/]*\.md", source):
            title = page_title(content) or page.title or source
            for guide in linked_guides(page, without_code(content)):
                links = decision_links.setdefault(guide, [])
                if all(url != page.url for url, _ in links):
                    links.append((page.url, title))
        elif source == FEATURES_PAGE:
            collect_recipes(page, without_code(content))
    return nav


def page_title(content):
    """Read the front matter title, or the first H1, as plain text."""
    front_matter = FRONT_MATTER.match(content)
    field = TITLE_FIELD.search(front_matter["body"]) if front_matter else None
    if field:
        return plain_text(field["title"].strip().strip("\"'"))
    for heading in HEADING.finditer(content):
        if heading["level"] == "#":
            return plain_text(heading["text"])
    return None


def plain_text(markup):
    """Reduce inline Markdown or HTML to the text a reader sees."""
    text = re.sub(r"`([^`]*)`", lambda match: escape(match[1]), markup)
    text = re.sub(r"<[^>]+>", "", text)
    return re.sub(r"\s+", " ", unescape(text)).strip()


def without_code(content):
    """Blank fenced code so links and headings in listings are ignored."""
    return "\n".join("" if fenced else line for line, fenced in fence_lines(content))


def fence_lines(content):
    """Yield each line with whether it belongs to a fenced code block."""
    fence = None
    for line in content.split("\n"):
        if fence:
            if re.fullmatch(rf"[ \t]*{re.escape(fence[0])}{{{len(fence)},}}[ \t]*\r?", line):
                fence = None
            yield line, True
            continue
        opening = FENCE.match(line)
        if opening:
            fence = opening["fence"]
            yield line, True
            continue
        yield line, False


def guide_url(page, target):
    """Resolve a Markdown source link or a built URL to a reference guide URL in the navigation."""
    target = re.split(r"[#?]", target, maxsplit=1)[0]
    if target.endswith(".md"):
        source = posixpath.normpath(posixpath.join(posixpath.dirname(page.file.src_uri), target))
        url = source_urls.get(source)
    else:
        resolved = urlsplit(urljoin("https://docs.invalid/" + page.url, target))
        url = resolved.path.lstrip("/") if resolved.netloc == "docs.invalid" else None
        if url not in source_urls.values():
            url = None
    return url if url and url.startswith("reference/") else None


def linked_guides(page, content):
    """Return the URLs of reference guides a page links, in link order."""
    found = []
    for match in LINK.finditer(content):
        url = guide_url(page, match["target"] or match["href"])
        if url and url not in found:
            found.append(url)
    return found


def collect_recipes(page, content):
    """Map each guide to the Add features recipe that links it, preferring its Guide link."""
    headings = list(HEADING.finditer(content))
    ranks = {}
    for index, heading in enumerate(headings):
        if heading["level"] != "###":
            continue
        parts = ATTRIBUTE_LIST.match(heading["text"])
        if not parts or ".feature-recipe" not in parts["attributes"].split():
            continue
        anchor = re.search(r"(?:^|\s)#(?P<id>[\w-]+)", parts["attributes"])
        if not anchor:
            continue
        end = headings[index + 1].start() if index + 1 < len(headings) else len(content)
        section = content[heading.end():end]
        label = plain_text(parts["text"])
        chip = GUIDE_CHIP.search(section)
        preferred = guide_url(page, chip["href"] or chip["target"]) if chip else None
        for position, guide in enumerate(linked_guides(page, section)):
            rank = (0 if guide == preferred else 1 if position == 0 else 2, index)
            if guide not in ranks or rank < ranks[guide]:
                ranks[guide] = rank
                recipe_links[guide] = (page.url, anchor["id"], label)


def decorate_navigation(markup, current_url):
    """Add inline icons to Material's existing primary navigation anchors."""
    def replace(match):
        attrs = match["attributes"]
        classes = (attribute(attrs, "class") or "").split()
        href = attribute(attrs, "href")
        if "md-nav__link" not in classes or not href or href.startswith("#"):
            return match[0]
        target = urlsplit(urljoin("https://docs.invalid/" + current_url, href))
        if target.netloc != "docs.invalid":
            return match[0]
        ids = navigation.get(target.path.lstrip("/"), [])
        if not ids:
            return match[0]
        return f'<a{attrs}>{feature_label_html(match["label"], ids)}</a>'

    return Markup(ANCHOR.sub(replace, str(markup)))


def on_env(env, config, files):
    """Expose the shared logo and decorate Material's existing nav partial."""
    env.filters["bdk_feature_navigation"] = decorate_navigation
    env.globals["bdk_brand_logo"] = lambda size=170: Markup(brand_logo_html(size))
    return env


def catalog_html(page):
    """Build the native, theme-aware catalog from the same icon inventory."""
    by_id = {icon["id"]: icon for icon in catalog}
    groups = dict.fromkeys(icon["group"] for icon in catalog)
    parts = []
    for group in groups:
        parts.append(f"\n\n## {escape(group)}\n\n<div class=\"bdk-icon-grid\">\n")
        for icon in (item for item in catalog if item["group"] == group):
            source = icon["sources"][0]
            guide = source_urls["reference/" + Path(source).name]
            href = relative_url(page.url, guide)
            download = relative_url(page.url, f'assets/features/{icon["id"]}.svg')
            parent = by_id[icon["parent"]]["title"] if icon["parent"] else "Feature"
            parts.append(
                f'<div class="bdk-icon-card" id="icon-{icon["id"]}">'
                + icon_html(icon["id"], 40)
                + f'<a href="{escape(href, quote=True)}">{escape(icon["title"])}</a>'
                + f'<span class="bdk-icon-parent">{escape(parent)}</span>'
                + f'<a class="bdk-icon-download" href="{escape(download, quote=True)}" '
                + f'download aria-label="Download {escape(icon["title"], quote=True)} SVG">SVG</a></div>\n'
            )
        parts.append("</div>\n")
    return "".join(parts)


def relative_url(current, target):
    """Resolve public URLs with MkDocs' own subdirectory-aware helper."""
    from mkdocs.utils import get_relative_url

    return get_relative_url(target, current)


def brand_logo_html(size=170):
    """Reuse the dashboard's decorative SVG geometry at the requested size."""
    source = (ROOT / "src/Presentation.Web/Pages/_DashboardLogo.cshtml").read_text(encoding="utf-8")
    match = re.search(r"<svg\b.*?</svg>", source, re.DOTALL)
    if not match:
        raise ValueError("The dashboard brand logo contains no SVG")
    svg = ET.fromstring(match[0])
    svg.set("width", str(size))
    svg.set("height", str(size))
    return ET.tostring(svg, encoding="unicode")


def on_page_markdown(markdown, page, config, files):
    """Inline marked SVG images before heading and TOC generation."""
    def replace(match):
        attrs = match["attributes"]
        icon_id = Path(attribute(attrs, "src")).stem
        if icon_id not in drawings:
            raise ValueError(f"Unknown documentation icon: {icon_id}")
        size = int(attribute(attrs, "width") or 24)
        return icon_html(icon_id, size)

    markdown = expand_markers(markdown, page)
    markdown = TITLE.sub(lambda match: f'# {match["icons"]} {match["title"]}', markdown)
    markdown = IMAGE.sub(replace, markdown)
    if "<!-- bdk:feature-icon-catalog -->" in markdown:
        markdown = markdown.replace("<!-- bdk:feature-icon-catalog -->", catalog_html(page))
    if "<!-- bdk:brand-logo -->" in markdown:
        markdown = markdown.replace("<!-- bdk:brand-logo -->", brand_logo_html())
    return markdown


def expand_markers(markdown, page):
    """Render agent-prompt markers outside fenced code."""
    if "<!--" not in markdown:
        return markdown
    lines = []
    for line, fenced in fence_lines(markdown):
        marker = None if fenced else MARKER.match(line.rstrip("\r"))
        rendered = render_marker(marker, markdown, page) if marker else None
        if rendered is None:
            lines.append(line)
        else:
            lines.extend(marker["indent"] + item if item else "" for item in ["", *rendered, ""])
    return "\n".join(lines)


def render_marker(marker, markdown, page):
    """Return the marker's Markdown lines, or None after warning about a malformed marker."""
    arguments = PROMPT_ARGUMENTS.match(marker["arguments"])
    if arguments and 1 <= int(arguments["step"]) <= TUTORIAL_STEP_COUNT:
        step = int(arguments["step"])
        heading = step_heading(markdown, step)
        if heading:
            return agent_prompt_lines(step, heading, arguments["kind"], arguments["value"].split())
        log.warning(
            "%s: no '## ... { #step-%d }' heading for marker '%s'; left as a comment",
            page.file.src_uri, step, marker[0].strip(),
        )
        return None
    log.warning("%s: malformed marker '%s'; left as a comment", page.file.src_uri, marker[0].strip())
    return None


def step_heading(markdown, step):
    """Read a step heading as plain text, without its attribute list, spans and code marks."""
    for line, fenced in fence_lines(markdown):
        heading = None if fenced else re.match(r"##[ \t]+(?P<text>.+?)[ \t]*\{(?P<attributes>[^}]*)\}[ \t]*\r?$", line)
        if heading and re.search(rf"(?:^|\s)#step-{step}(?:\s|$)", heading["attributes"]):
            return plain_text(re.sub(r"<span\b[^>]*>.*?</span>", "", heading["text"])) or None
    return None


def agent_prompt_lines(step, heading, kind, values):
    """Build the collapsed step prompt from the shared template."""
    if kind == "source":
        guidance = (
            "Read the linked reference guide with the bdk MCP tool "
            f"bdk_docs_get and source {' '.join(values)}."
        )
    elif len(values) == 1:
        guidance = f"Call the bdk MCP tool bdk_guidance_get with topic {values[0]}."
    else:
        topics = f"{', '.join(values[:-1])} and {values[-1]}"
        guidance = f"Call the bdk MCP tool bdk_guidance_get once for each of the topics {topics}."
    template = MINIMAL_APP_PROMPT_TEMPLATE if step == 1 else MCP_SETUP_PROMPT_TEMPLATE if step == 2 else PROMPT_TEMPLATE
    prompt = [line.format(step=step, heading=heading, guidance=guidance) for line in template]
    return ['??? prompt "AI prompt"', "", "    ```text", *(f"    {line}" for line in prompt), "    ```"]


def on_page_content(html, page, config, files):
    """Restyle feature guide headers and decision pages from their predictable Material markup."""
    source = page.file.src_uri
    decision = re.fullmatch(r"decisions-[^/]*\.md", source) is not None
    if decision or source.startswith("reference/"):
        html = add_lede_and_facts(html, page)
    if decision:
        html = add_choice_fork(html, page)
        html = wrap_comparison(html)
        html = add_related_strip(html, page)
    return decorate_feature_names(html)


def element_end(html, start, tag):
    """Return the index after the element whose start tag begins at start, or -1."""
    depth = 0
    for match in re.finditer(rf"<(/?){tag}\b[^>]*>", html[start:]):
        depth += -1 if match[1] else 1
        if depth == 0:
            return start + match.end()
    return -1


def add_lede_and_facts(html, page):
    """Mark the blockquote after the H1 as the lede and follow it with the facts strip."""
    heading = H1.search(html)
    lede = re.compile(r"\s*<blockquote>").match(html, heading.end()) if heading else None
    if not lede:
        return html
    start = lede.end() - len("<blockquote>")
    end = element_end(html, start, "blockquote")
    if end < 0:
        return html
    quote = '<blockquote class="bdk-lede">' + html[lede.end():end]
    return html[:start] + quote + facts_html(html, page) + html[end:]


def facts_html(html, page):
    """Build the facts strip from guide back-links; empty when no chip has data."""
    chips = []
    decisions = decision_links.get(page.url, [])
    if decisions:
        chips.append(("Decide", "".join(link_html(relative_url(page.url, url), title) for url, title in decisions)))
    recipe = recipe_links.get(page.url)
    if recipe:
        url, anchor, label = recipe
        chips.append(("Existing app", link_html(f"{relative_url(page.url, url)}#{anchor}", f"{label} recipe")))
    if re.search(r'<h2\b[^>]*\bid="dashboard-guide"', html):
        chips.append(("Dashboard", link_html("#dashboard-guide", "Dashboard guide")))
    if not chips:
        return ""
    items = "".join(f"<div><dt>{name}</dt><dd>{links}</dd></div>" for name, links in chips)
    return f'\n<dl class="bdk-facts">{items}</dl>'


def link_html(href, label):
    """Return an escaped text link."""
    return f'<a href="{escape(href, quote=True)}">{escape(label)}</a>'


def add_choice_fork(html, page):
    """Wrap the first two H2 sections in choice cards."""
    headings = list(H2.finditer(html))
    if len(headings) < 2:
        return html
    cards = []
    for index in range(2):
        end = headings[index + 1].start() if index + 1 < len(headings) else len(html)
        section = html[headings[index].start():end].rstrip()
        cards.append(f'<section class="choice-card">\n{section}\n</section>')
    start = headings[0].start()
    end = headings[2].start() if len(headings) > 2 else len(html)
    return html[:start] + '<div class="choice-fork">\n' + "\n".join(cards) + "\n</div>\n" + html[end:]


def wrap_comparison(html):
    """Wrap the first table of the Comparison section in the shared comparison style."""
    heading = re.search(r'<h2\b[^>]*\bid="comparison"[^>]*>.*?</h2>', html, re.DOTALL)
    if not heading:
        return html
    following = H2.search(html, heading.end())
    limit = following.start() if following else len(html)
    start = html.find("<table", heading.end(), limit)
    end = element_end(html, start, "table") if start >= 0 else -1
    if end < 0:
        return html
    return html[:start] + '<div class="cmp">' + html[start:end] + "</div>" + html[end:]


def add_related_strip(html, page):
    """Turn the Related features list into chip links with each target's title icons."""
    heading = re.search(r'<h2\b[^>]*\bid="related-features"[^>]*>(?:(?!</h2>).)*</h2>\s*<ul>', html, re.DOTALL)
    if not heading:
        return html
    start = heading.end() - len("<ul>")
    end = element_end(html, start, "ul")
    if end < 0:
        return html

    def decorate(match):
        href = attribute(match["attributes"], "href") or ""
        target = urlsplit(urljoin("https://docs.invalid/" + page.url, href))
        ids = navigation.get(target.path.lstrip("/"), []) if target.netloc == "docs.invalid" else []
        return f'<a{match["attributes"]}>{feature_label_html(match["label"], ids)}</a>'

    strip = '<ul class="related-strip">' + ANCHOR.sub(decorate, html[heading.end():end])
    return html[:start] + strip + html[end:]
