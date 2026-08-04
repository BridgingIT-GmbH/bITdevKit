(function () {
  const mkdocsShortcutKeys = new Set(["f", "s", "/"]);

  document.addEventListener("keydown", function (event) {
    const context7Active = document.activeElement?.id === "context7-widget";
    const mkdocsShortcut =
      !event.metaKey &&
      !event.ctrlKey &&
      mkdocsShortcutKeys.has(event.key);

    if (context7Active || mkdocsShortcut) {
      event.stopPropagation();
    }
  });
})();
