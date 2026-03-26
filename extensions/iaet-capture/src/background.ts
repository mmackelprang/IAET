// background.ts — Manifest V3 service worker
// Manages capture state, stores captured requests, updates badge, handles export/POST.

import type {
  IaetRequest,
  IaetFile,
  ContentToBackground,
  PopupState,
  IaetSession,
} from "./types";
import { normalizeEndpoint, generateUuid } from "./types";

// ---- Capture state ----

interface CaptureState {
  recording: boolean;
  sessionId: string;
  sessionName: string;
  startedAt: string;
  requests: IaetRequest[];
  endpointSet: Set<string>;
  targetApplication: string;
}

const state: CaptureState = {
  recording: false,
  sessionId: generateUuid(),
  sessionName: "capture",
  startedAt: new Date().toISOString(),
  requests: [],
  endpointSet: new Set(),
  targetApplication: "unknown",
};

// ---- Message handler ----

chrome.runtime.onMessage.addListener(
  (
    msg: ContentToBackground | PopupMessage,
    _sender,
    sendResponse: (response: unknown) => void
  ) => {
    switch (msg.type) {
      case "REQUEST_CAPTURED": {
        if (!state.recording) break;
        const payload = (msg as ContentToBackground & { type: "REQUEST_CAPTURED" }).payload;
        const req: IaetRequest = {
          id: payload.id,
          sessionId: state.sessionId,
          timestamp: payload.timestamp,
          httpMethod: payload.httpMethod,
          url: payload.url,
          requestHeaders: payload.requestHeaders,
          requestBody: payload.requestBody,
          responseStatus: payload.responseStatus,
          responseHeaders: payload.responseHeaders,
          responseBody: payload.responseBody,
          durationMs: payload.durationMs,
          tag: null,
        };
        state.requests.push(req);
        const sig = normalizeEndpoint(req.httpMethod, req.url);
        state.endpointSet.add(sig);
        updateBadge();
        break;
      }

      case "PING":
        sendResponse({ type: "PONG" });
        break;

      case "POPUP_START":
        startRecording((msg as PopupMessage & { type: "POPUP_START" }).sessionName);
        sendResponse(getPopupState());
        break;

      case "POPUP_STOP":
        stopRecording();
        sendResponse(getPopupState());
        break;

      case "POPUP_GET_STATE":
        sendResponse(getPopupState());
        break;

      case "POPUP_EXPORT":
        sendResponse({ iaetFile: buildIaetFile() });
        break;

      case "POPUP_CLEAR":
        clearCapture();
        sendResponse(getPopupState());
        break;

      case "POPUP_POST": {
        const { serverUrl } = msg as PopupMessage & { type: "POPUP_POST"; serverUrl: string };
        void postToServer(serverUrl, buildIaetFile()).then((ok) => {
          sendResponse({ ok });
        });
        return true; // keep channel open for async
      }

      default:
        break;
    }
    return false;
  }
);

// ---- Capture lifecycle ----

function startRecording(sessionName: string): void {
  state.recording = true;
  state.sessionId = generateUuid();
  state.sessionName = sessionName;
  state.startedAt = new Date().toISOString();
  state.requests = [];
  state.endpointSet = new Set();
  updateBadge();
}

function stopRecording(): void {
  state.recording = false;
  updateBadge();
}

function clearCapture(): void {
  state.recording = false;
  state.requests = [];
  state.endpointSet = new Set();
  updateBadge();
}

function updateBadge(): void {
  const count = state.endpointSet.size;
  const text = count > 0 ? (count > 99 ? "99+" : String(count)) : "";
  chrome.action.setBadgeText({ text });
  chrome.action.setBadgeBackgroundColor({ color: state.recording ? "#0e639c" : "#267f3e" });
}

function getPopupState(): PopupState {
  return {
    recording: state.recording,
    requestCount: state.requests.length,
    sessionName: state.sessionName,
    endpointCount: state.endpointSet.size,
  };
}

function buildIaetFile(): IaetFile {
  const session: IaetSession = {
    id: state.sessionId,
    name: state.sessionName,
    targetApplication: state.targetApplication,
    profile: "default",
    startedAt: state.startedAt,
    stoppedAt: state.recording ? null : new Date().toISOString(),
    capturedBy: "iaet-capture/0.1.0",
  };

  return {
    iaetVersion: "1.0",
    exportedAt: new Date().toISOString(),
    session,
    requests: [...state.requests],
    streams: [],
  };
}

async function postToServer(serverUrl: string, iaetFile: IaetFile): Promise<boolean> {
  try {
    const res = await fetch(serverUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(iaetFile),
    });
    return res.ok;
  } catch {
    return false;
  }
}

// ---- Popup message type ----

type PopupMessage =
  | { type: "POPUP_START"; sessionName: string }
  | { type: "POPUP_STOP" }
  | { type: "POPUP_GET_STATE" }
  | { type: "POPUP_EXPORT" }
  | { type: "POPUP_CLEAR" }
  | { type: "POPUP_POST"; serverUrl: string };
