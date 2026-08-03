(function () {
  "use strict";

  const labels = {
    dark: "Dark",
    catppuccin: "Catppuccin",
    darkforest: "Darkforest",
    carbon: "Carbon",
    tokionights: "Tokyo Night",
    matrix: "Matrix",
    light: "Light",
    system: "System"
  };

  function getSelectedTheme() {
    const palette = typeof __md_get === "function" ? __md_get("__palette") : null;
    const color = palette && palette.color;

    if (color && color.media === "(prefers-color-scheme)") {
      return "system";
    }

    if (color && labels[color.scheme]) {
      return color.scheme;
    }

    const scheme = document.body.getAttribute("data-md-color-scheme");
    return labels[scheme] ? scheme : "system";
  }

  function updatePicker(picker) {
    const theme = getSelectedTheme();
    const label = picker.querySelector("[data-bdk-theme-label]");

    if (label) {
      label.textContent = labels[theme];
    }

    picker.querySelectorAll("[data-bdk-theme-option]").forEach(function (option) {
      const active = option.dataset.bdkThemeOption === theme;
      option.classList.toggle("is-active", active);
      option.setAttribute("aria-checked", active ? "true" : "false");
    });
  }

  function initializePicker() {
    document.querySelectorAll("[data-bdk-theme-picker]").forEach(function (details) {
      if (details.dataset.bdkThemePickerReady === "true") {
        updatePicker(details);
        return;
      }

      details.dataset.bdkThemePickerReady = "true";
      const form = details.closest("[data-md-component='palette']");

      if (!form) {
        return;
      }

      form.querySelectorAll("input[name='__palette']").forEach(function (input) {
        input.addEventListener("change", function () {
          window.requestAnimationFrame(function () {
            updatePicker(details);
            details.removeAttribute("open");
          });
        });
      });

      const options = Array.from(details.querySelectorAll("[data-bdk-theme-option]"));
      options.forEach(function (option, index) {
        option.addEventListener("keydown", function (event) {
          if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            event.stopPropagation();
            const input = document.getElementById(option.htmlFor);
            if (input) {
              input.checked = true;
              input.dispatchEvent(new Event("change", { bubbles: true }));
            }
            return;
          }

          let targetIndex = null;
          if (event.key === "ArrowDown" || event.key === "ArrowRight") {
            targetIndex = (index + 1) % options.length;
          } else if (event.key === "ArrowUp" || event.key === "ArrowLeft") {
            targetIndex = (index - 1 + options.length) % options.length;
          } else if (event.key === "Home") {
            targetIndex = 0;
          } else if (event.key === "End") {
            targetIndex = options.length - 1;
          }

          if (targetIndex !== null) {
            event.preventDefault();
            event.stopPropagation();
            options[targetIndex].focus();
          }
        });
      });

      details.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
          details.removeAttribute("open");
          details.querySelector("summary").focus();
        }
      });

      document.addEventListener("click", function (event) {
        if (!details.contains(event.target)) {
          details.removeAttribute("open");
        }
      });

      updatePicker(details);
    });
  }

  if (typeof document$ !== "undefined") {
    document$.subscribe(initializePicker);
  } else if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initializePicker);
  } else {
    initializePicker();
  }
})();
