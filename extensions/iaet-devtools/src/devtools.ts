// DevTools entry point: registers the IAET panel in Chrome DevTools

chrome.devtools.panels.create(
  "IAET",
  "/icons/icon16.png",
  "/panel.html",
  (_panel) => {
    // Panel registered; panel.ts handles the rest
  }
);
