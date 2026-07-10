(function () {
  const widgetSource = "https://context7.com/widget.js";

  if (document.querySelector(`script[src="${widgetSource}"]`)) {
    return;
  }

  const script = document.createElement("script");
  script.src = widgetSource;
  script.async = true;
  script.dataset.library = "/bridgingit-gmbh/bitdevkit";

  document.head.appendChild(script);
})();
