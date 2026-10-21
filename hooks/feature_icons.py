"""Render the shared dashboard logo and canonical feature SVGs in documentation."""

from html import escape, unescape
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET
from urllib.parse import urljoin, urlsplit

from markupsafe import Markup


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
catalog = []
drawings = {}
navigation = {}
source_urls = {}


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


def on_pre_build(config):
    """Refresh source data and stage only public SVG and metadata assets."""
    global catalog, drawings
    catalog = json.loads((ASSETS / "catalog.json").read_text(encoding="utf-8"))["icons"]
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
    """Use each guide's actual title icons for its navigation entry."""
    global navigation, source_urls
    navigation = {}
    source_urls = {}
    for page in nav.pages:
        source = page.file.src_uri
        source_urls[source] = page.url
        content = Path(page.file.abs_src_path).read_text(encoding="utf-8-sig")
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
    return nav


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
        icons = "".join(icon_html(icon_id, 18) for icon_id in ids)
        return f'<a{attrs}>{icons}{match["label"]}</a>'

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

    markdown = TITLE.sub(lambda match: f'# {match["icons"]} {match["title"]}', markdown)
    markdown = IMAGE.sub(replace, markdown)
    if "<!-- bdk:feature-icon-catalog -->" in markdown:
        markdown = markdown.replace("<!-- bdk:feature-icon-catalog -->", catalog_html(page))
    if "<!-- bdk:brand-logo -->" in markdown:
        markdown = markdown.replace("<!-- bdk:brand-logo -->", brand_logo_html())
    return markdown
