// background.ts — Manifest V3 service worker
// Manages capture state, stores captured requests, updates badge, handles export/POST.

import type {
  IaetRequest,
  IaetStream,
  IaetStreamFrame,
  IaetFile,
  ContentToBackground,
  PopupState,
  IaetSession,
  WsEventPayload,
  RtcEventPayload,
  SseEventPayload,
} from "./types";
import { normalizeEndpoint, generateUuid } from "./types";

// ---- Capture state ----

const MAX_REQUESTS = 10000;

interface WsConnectionState {
  id: string;
  url: string;
  startedAt: string;
  endedAt: string | null;
  protocol: string | undefined;
  frames: IaetStreamFrame[];
}

interface RtcConnectionState {
  id: string;
  startedAt: string;
  config: string | undefined;
  events: IaetStreamFrame[];
}

interface SseConnectionState {
  id: string;
  url: string;
  startedAt: string;
  endedAt: string | null;
  frames: IaetStreamFrame[];
}

interface CaptureState {
  recording: boolean;
  sessionId: string;
  sessionName: string;
  startedAt: string;
  requests: IaetRequest[];
  endpointSet: Set<string>;
  targetApplication: string;
  wsConnections: Map<string, WsConnectionState>;
  rtcConnections: Map<string, RtcConnectionState>;
  sseConnections: Map<string, SseConnectionState>;
}

const MAX_WS_FRAMES = 1000;

const state: CaptureState = {
  recording: false,
  sessionId: generateUuid(),
  sessionName: "capture",
  startedAt: new Date().toISOString(),
  requests: [],
  endpointSet: new Set(),
  targetApplication: "unknown",
  wsConnections: new Map(),
  rtcConnections: new Map(),
  sseConnections: new Map(),
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
        // Enforce request cap: if at limit, drop the oldest request
        if (state.requests.length >= MAX_REQUESTS) {
          const dropped = state.requests.shift();
          if (dropped) {
            const droppedSig = normalizeEndpoint(dropped.httpMethod, dropped.url);
            // Rebuild endpointSet as we may have removed the only request for this endpoint
            state.endpointSet.delete(droppedSig);
          }
        }
        state.requests.push(req);
        const sig = normalizeEndpoint(req.httpMethod, req.url);
        state.endpointSet.add(sig);
        updateBadge();
        break;
      }

      case "WS_EVENT": {
        if (!state.recording) break;
        const wsMsg = msg as ContentToBackground & { type: "WS_EVENT" };
        handleWsEvent(wsMsg.action, wsMsg.payload);
        updateBadge();
        break;
      }

      case "RTC_EVENT": {
        if (!state.recording) break;
        const rtcMsg = msg as ContentToBackground & { type: "RTC_EVENT" };
        handleRtcEvent(rtcMsg.action, rtcMsg.payload);
        break;
      }

      case "SSE_EVENT": {
        if (!state.recording) break;
        const sseMsg = msg as ContentToBackground & { type: "SSE_EVENT" };
        handleSseEvent(sseMsg.action, sseMsg.payload);
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

function handleWsEvent(action: "open" | "frame" | "close", payload: WsEventPayload): void {
  if (action === "open") {
    state.wsConnections.set(payload.id, {
      id: payload.id,
      url: payload.url,
      startedAt: payload.timestamp,
      endedAt: null,
      protocol: payload.protocol,
      frames: [],
    });
  } else if (action === "frame") {
    const conn = state.wsConnections.get(payload.id);
    if (conn && conn.frames.length < MAX_WS_FRAMES) {
      conn.frames.push({
        timestamp: payload.timestamp,
        direction: payload.direction ?? "Received",
        textPayload: payload.textPayload ?? null,
        binaryPayload: null,
        sizeBytes: payload.textPayload?.length ?? payload.binarySize ?? 0,
      });
    }
  } else if (action === "close") {
    const conn = state.wsConnections.get(payload.id);
    if (conn) {
      conn.endedAt = payload.timestamp;
    }
  }
}

function handleRtcEvent(action: string, payload: RtcEventPayload): void {
  if (action === "create") {
    state.rtcConnections.set(payload.id, {
      id: payload.id,
      startedAt: payload.timestamp,
      config: payload.config,
      events: [],
    });
  } else {
    const conn = state.rtcConnections.get(payload.id);
    if (conn && conn.events.length < MAX_WS_FRAMES) {
      // Store RTC events as frames with action + data in textPayload
      const eventData: Record<string, unknown> = { action };
      if (payload.sdpType) eventData.sdpType = payload.sdpType;
      if (payload.sdp) eventData.sdp = payload.sdp;
      if (payload.candidate) eventData.candidate = payload.candidate;
      if (payload.state) eventData.state = payload.state;

      conn.events.push({
        timestamp: payload.timestamp,
        direction: (action === "setRemoteDesc" || action === "addIceCandidate") ? "Received" : "Sent",
        textPayload: JSON.stringify(eventData),
        binaryPayload: null,
        sizeBytes: 0,
      });
    }
  }
}

function handleSseEvent(action: "open" | "message" | "error" | "close", payload: SseEventPayload): void {
  if (action === "open") {
    state.sseConnections.set(payload.id, {
      id: payload.id,
      url: payload.url,
      startedAt: payload.timestamp,
      endedAt: null,
      frames: [],
    });
  } else if (action === "message") {
    const conn = state.sseConnections.get(payload.id);
    if (conn && conn.frames.length < MAX_WS_FRAMES) {
      conn.frames.push({
        timestamp: payload.timestamp,
        direction: "Received",
        textPayload: payload.data ?? null,
        binaryPayload: null,
        sizeBytes: payload.data?.length ?? 0,
      });
    }
  } else if (action === "error" || action === "close") {
    const conn = state.sseConnections.get(payload.id);
    if (conn) {
      conn.endedAt = payload.timestamp;
    }
  }
}

function startRecording(sessionName: string): void {
  state.recording = true;
  state.sessionId = generateUuid();
  state.sessionName = sessionName;
  state.startedAt = new Date().toISOString();
  state.requests = [];
  state.endpointSet = new Set();
  state.wsConnections = new Map();
  state.rtcConnections = new Map();
  state.sseConnections = new Map();
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
  state.wsConnections = new Map();
  state.rtcConnections = new Map();
  state.sseConnections = new Map();
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
    capturedBy: "iaet-capture/0.2.0",
  };

  const streams: IaetStream[] = [];
  for (const conn of state.wsConnections.values()) {
    streams.push({
      id: conn.id,
      sessionId: state.sessionId,
      protocol: "WebSocket",
      url: conn.url,
      startedAt: conn.startedAt,
      endedAt: conn.endedAt,
      metadata: conn.protocol ? { subprotocol: conn.protocol } : {},
      frames: conn.frames.length > 0 ? conn.frames : null,
      samplePayloadPath: null,
      tag: null,
    });
  }
  for (const conn of state.rtcConnections.values()) {
    streams.push({
      id: conn.id,
      sessionId: state.sessionId,
      protocol: "WebRtc",
      url: "rtc://" + conn.id,
      startedAt: conn.startedAt,
      endedAt: null,
      metadata: conn.config ? { config: conn.config } : {},
      frames: conn.events.length > 0 ? conn.events : null,
      samplePayloadPath: null,
      tag: null,
    });
  }
  for (const conn of state.sseConnections.values()) {
    streams.push({
      id: conn.id,
      sessionId: state.sessionId,
      protocol: "ServerSentEvents",
      url: conn.url,
      startedAt: conn.startedAt,
      endedAt: conn.endedAt,
      metadata: {},
      frames: conn.frames.length > 0 ? conn.frames : null,
      samplePayloadPath: null,
      tag: null,
    });
  }

  return {
    iaetVersion: "1.0",
    exportedAt: new Date().toISOString(),
    session,
    requests: [...state.requests],
    streams,
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
