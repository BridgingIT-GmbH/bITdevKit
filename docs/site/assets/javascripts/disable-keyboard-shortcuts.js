(function () {
  if (typeof keyboard$ === "undefined") {
    return;
  }

  keyboard$.subscribe(function (key) {
    if (key.mode === "global") {
      key.claim();
    }
  });
})();
