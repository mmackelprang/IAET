// Shared TypeScript types matching .iaet.json format (docs/capture-format.md)

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

// Internal type for grouping requests by normalized endpoint
export interface EndpointGroup {
  signature: string; // "GET /api/v1/users/{id}"
  method: string;
  pathTemplate: string;
  requests: IaetRequest[];
  tags: string[];
}

// Normalized endpoint: replaces path IDs with {id}, removes query strings
export function normalizeEndpoint(method: string, url: string): string {
  try {
    const parsed = new URL(url);
    // Replace numeric and UUID path segments with {id}
    const normalizedPath = parsed.pathname
      .replace(/\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, "/{id}")
      .replace(/\/\d+/g, "/{id}");
    return `${method.toUpperCase()} ${normalizedPath}`;
  } catch {
    return `${method.toUpperCase()} ${url}`;
  }
}

// Analytics/telemetry filter patterns to hide noise
export const FILTER_PATTERNS: RegExp[] = [
  /google-analytics\.com/i,
  /googletagmanager\.com/i,
  /analytics\.google\.com/i,
  /segment\.com\/v1/i,
  /api\.segment\.io/i,
  /api\.mixpanel\.com/i,
  /sentry\.io/i,
  /bugsnag\.com/i,
  /datadog\.com/i,
  /hotjar\.com/i,
  /fullstory\.com/i,
  /amplitude\.com/i,
  /heap\.io/i,
  /intercom\.io/i,
  /collect\?v=\d/i, // GA collect
];

export function isAnalyticUrl(url: string): boolean {
  return FILTER_PATTERNS.some((pattern) => pattern.test(url));
}
