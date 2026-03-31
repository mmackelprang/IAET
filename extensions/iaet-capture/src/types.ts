// Shared TypeScript types matching .iaet.json format (docs/capture-format.md)
// Duplicated from iaet-devtools/src/types.ts so each extension is self-contained.

export interface IaetSession {
  id: string;
  name: string;
  targetApplication: string;
  profile: string;
  startedAt: string;
  stoppedAt: string | null;
  capturedBy: string;
}

export interface IaetRequest {
  id: string;
  sessionId: string;
  timestamp: string;
  httpMethod: string;
  url: string;
  requestHeaders: Record<string, string>;
  requestBody: string | null;
  responseStatus: number;
  responseHeaders: Record<string, string>;
  responseBody: string | null;
  durationMs: number;
  tag: string | null;
}

export interface IaetStreamFrame {
  timestamp: string;
  direction: "Sent" | "Received";
  textPayload: string | null;
  binaryPayload: string | null;
  sizeBytes: number;
}

export interface IaetStream {
  id: string;
  sessionId: string;
  protocol:
    | "WebSocket"
    | "ServerSentEvents"
    | "WebRtc"
    | "HlsStream"
    | "DashStream"
    | "GrpcWeb"
    | "WebAudio"
    | "Unknown";
  url: string;
  startedAt: string;
  endedAt: string | null;
  metadata: Record<string, string>;
  frames: IaetStreamFrame[] | null;
  samplePayloadPath: string | null;
  tag: string | null;
}

export interface IaetFile {
  iaetVersion: "1.0";
  exportedAt: string;
  session: IaetSession;
  requests: IaetRequest[];
  streams: IaetStream[];
}

// Messages between content script / inject and the background service worker
export interface WsEventPayload {
  id: string;
  url: string;
  timestamp: string;
  direction?: "Sent" | "Received";
  textPayload?: string | null;
  binarySize?: number;
  protocol?: string;
}

export type ContentToBackground =
  | { type: "REQUEST_CAPTURED"; payload: CapturedRequestPayload }
  | { type: "WS_EVENT"; action: "open" | "frame" | "close"; payload: WsEventPayload }
  | { type: "PING" };

export type BackgroundToContent =
  | { type: "RECORDING_STATE"; recording: boolean }
  | { type: "PONG" };

export interface CapturedRequestPayload {
  id: string;
  timestamp: string;
  httpMethod: string;
  url: string;
  requestHeaders: Record<string, string>;
  requestBody: string | null;
  responseStatus: number;
  responseHeaders: Record<string, string>;
  responseBody: string | null;
  durationMs: number;
}

// Message from background to popup
export interface PopupState {
  recording: boolean;
  requestCount: number;
  sessionName: string;
  endpointCount: number;
}

// Normalize endpoint: replaces path numeric/UUID segments with {id}
export function normalizeEndpoint(method: string, url: string): string {
  try {
    const parsed = new URL(url);
    const normalizedPath = parsed.pathname
      .replace(/\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, "/{id}")
      .replace(/\/\d+/g, "/{id}");
    return `${method.toUpperCase()} ${normalizedPath}`;
  } catch {
    return `${method.toUpperCase()} ${url}`;
  }
}

export function generateUuid(): string {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}
