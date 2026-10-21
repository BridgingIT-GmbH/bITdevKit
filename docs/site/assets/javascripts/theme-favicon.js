(function () {
  "use strict";

  let updateScheduled = false;

  function updateFavicon() {
    const source = document.getElementById("bdk-favicon-source");
    const favicon = document.querySelector("link[rel~='icon']");
    const drawing = source && source.content.querySelector("svg");
    if (!drawing || !favicon) return;

    const colors = window.getComputedStyle(document.body);
    const body = colors.getPropertyValue("--bdk-info").trim();
    const accent = colors.getPropertyValue("--bdk-danger").trim();
    if (!body || !accent) return;

    const svg = drawing.cloneNode(true);
    svg.setAttribute("fill", body);
    svg.querySelectorAll(".dashboard-brand-logo-accent").forEach(function (part) {
      part.setAttribute("fill", accent);
    });
    const markup = new XMLSerializer().serializeToString(svg);
    const href = "data:image/svg+xml," + encodeURIComponent(markup);
    if (favicon.getAttribute("href") !== href) {
      favicon.setAttribute("type", "image/svg+xml");
      favicon.setAttribute("href", href);
    }
  }

  function scheduleUpdate() {
    if (updateScheduled) return;
    updateScheduled = true;
    window.requestAnimationFrame(function () {
      updateScheduled = false;
      updateFavicon();
    });
  }

  const observer = new MutationObserver(scheduleUpdate);
  function initializeFavicon() {
    observer.disconnect();
    observer.observe(document.body, {
      attributes: true,
      attributeFilter: ["data-md-color-scheme"]
    });
    scheduleUpdate();
  }

  if (typeof document$ !== "undefined") {
    document$.subscribe(initializeFavicon);
  } else if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initializeFavicon);
  } else {
    initializeFavicon();
  }
})();
