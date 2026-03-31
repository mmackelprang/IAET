// content.ts — content script running in the page's isolated world
// Injects inject.js into the main world, then relays captured requests
// to the background service worker.

import type { ContentToBackground, BackgroundToContent } from "./types";

const INJECT_MSG = "__iaet_request__";
const INJECT_WS_MSG = "__iaet_ws__";

// ---- Inject the interceptor into the main world ----

function injectScript(): void {
  const script = document.createElement("script");
  script.src = chrome.runtime.getURL("inject.js");
  script.type = "module";
  (document.head ?? document.documentElement).appendChild(script);
  script.addEventListener("load", () => script.remove());
}

injectScript();

// ---- Relay messages from the page to the background service worker ----

window.addEventListener("message", (event: MessageEvent) => {
  if (event.source !== window) return;
  if (!event.data) return;

  if (event.data.type === INJECT_MSG) {
    const msg: ContentToBackground = {
      type: "REQUEST_CAPTURED",
      payload: event.data.payload,
    };
    chrome.runtime.sendMessage(msg).catch(() => {});
  } else if (event.data.type === INJECT_WS_MSG) {
    const msg: ContentToBackground = {
      type: "WS_EVENT",
      action: event.data.action,
      payload: event.data.payload,
    };
    chrome.runtime.sendMessage(msg).catch(() => {});
  } else if (event.data.type === "__iaet_rtc__") {
    const msg: ContentToBackground = {
      type: "RTC_EVENT",
      action: event.data.action,
      payload: event.data.payload,
    };
    chrome.runtime.sendMessage(msg).catch(() => {});
  }
});

// ---- Listen for recording state changes from background ----

chrome.runtime.onMessage.addListener(
  (msg: BackgroundToContent): undefined => {
    if (msg.type === "RECORDING_STATE") {
      // Could enable/disable the inject script based on recording state
      // For simplicity, once injected we always capture but background filters
    }
    return undefined;
  }
);

// Notify background that this tab is ready
const ping: ContentToBackground = { type: "PING" };
chrome.runtime.sendMessage(ping).catch(() => {
  // Ignore
});
