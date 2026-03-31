// inject.ts — injected directly into the page context (not the extension context)
// Monkey-patches fetch and XMLHttpRequest to capture API traffic.
// Communicates back to the content script via window.postMessage.

(function () {
  const INJECT_MSG = "__iaet_request__";
  const INJECT_WS_MSG = "__iaet_ws__";
  const INJECT_RTC_MSG = "__iaet_rtc__";

  interface IaetInjectMessage {
    type: typeof INJECT_MSG;
    payload: {
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
    };
  }

  interface IaetWsMessage {
    type: typeof INJECT_WS_MSG;
    action: "open" | "frame" | "close";
    payload: {
      id: string;
      url: string;
      timestamp: string;
      direction?: "Sent" | "Received";
      textPayload?: string | null;
      binarySize?: number;
      protocol?: string;
    };
  }

  interface IaetRtcMessage {
    type: typeof INJECT_RTC_MSG;
    action: "create" | "setLocalDesc" | "setRemoteDesc" | "addIceCandidate" | "localIceCandidate" | "stateChange";
    payload: {
      id: string;
      timestamp: string;
      sdp?: string;
      sdpType?: string;
      candidate?: string;
      state?: string;
      config?: string;
    };
  }

  function generateId(): string {
    return `inject-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }

  function isApiUrl(url: string): boolean {
    try {
      const parsed = new URL(url, location.href);
      // Skip chrome-extension, data, blob URLs
      if (!parsed.protocol.startsWith("http")) return false;
      return true;
    } catch {
      return false;
    }
  }

  function postCapture(payload: IaetInjectMessage["payload"]): void {
    window.postMessage({ type: INJECT_MSG, payload } satisfies IaetInjectMessage, "*");
  }

  // ---- Patch fetch ----

  const originalFetch = window.fetch.bind(window);

  window.fetch = async function (input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
    const startTime = Date.now();
    const id = generateId();

    const req = new Request(input, init);
    const url = req.url;

    if (!isApiUrl(url)) {
      return originalFetch(input, init);
    }

    const method = req.method ?? "GET";

    const reqHeaders: Record<string, string> = {};
    req.headers.forEach((value, key) => {
      reqHeaders[key] = value;
    });

    let reqBody: string | null = null;
    if (init?.body != null) {
      if (typeof init.body === "string") {
        reqBody = init.body;
      } else if (init.body instanceof URLSearchParams) {
        reqBody = init.body.toString();
      } else if (init.body instanceof FormData) {
        reqBody = "[FormData]";
      } else {
        reqBody = "[Binary]";
      }
    }

    let response: Response;
    try {
      response = await originalFetch(input, init);
    } catch (err) {
      // Network error — still send partial capture
      postCapture({
        id,
        timestamp: new Date(startTime).toISOString(),
        httpMethod: method,
        url,
        requestHeaders: reqHeaders,
        requestBody: reqBody,
        responseStatus: 0,
        responseHeaders: {},
        responseBody: null,
        durationMs: Date.now() - startTime,
      });
      throw err;
    }

    const durationMs = Date.now() - startTime;

    const resHeaders: Record<string, string> = {};
    response.headers.forEach((value, key) => {
      resHeaders[key] = value;
    });

    // Clone to read body without consuming the original
    let resBody: string | null = null;
    try {
      const clone = response.clone();
      const ct = response.headers.get("content-type") ?? "";
      if (ct.includes("text") || ct.includes("json") || ct.includes("javascript")) {
        resBody = await clone.text();
      }
    } catch {
      // Ignore body read errors
    }

    postCapture({
      id,
      timestamp: new Date(startTime).toISOString(),
      httpMethod: method,
      url,
      requestHeaders: reqHeaders,
      requestBody: reqBody,
      responseStatus: response.status,
      responseHeaders: resHeaders,
      responseBody: resBody,
      durationMs,
    });

    return response;
  };

  // ---- Patch XMLHttpRequest ----

  const OriginalXHR = window.XMLHttpRequest;

  class PatchedXHR extends OriginalXHR {
    private _iaetMethod = "GET";
    private _iaetUrl = "";
    private _iaetReqHeaders: Record<string, string> = {};
    private _iaetReqBody: string | null = null;
    private _iaetStartTime = 0;
    private _iaetId = "";

    override open(method: string, url: string | URL, ...rest: [boolean?, string?, string?]): void {
      this._iaetMethod = method;
      this._iaetUrl = typeof url === "string" ? url : url.toString();
      this._iaetId = generateId();
      // @ts-expect-error — rest spread for overloaded signature
      super.open(method, url, ...rest);
    }

    override setRequestHeader(name: string, value: string): void {
      this._iaetReqHeaders[name] = value;
      super.setRequestHeader(name, value);
    }

    override send(body?: Document | XMLHttpRequestBodyInit | null): void {
      this._iaetStartTime = Date.now();

      if (typeof body === "string") {
        this._iaetReqBody = body;
      } else if (body instanceof URLSearchParams) {
        this._iaetReqBody = body.toString();
      } else if (body != null) {
        this._iaetReqBody = "[Binary]";
      }

      this.addEventListener("loadend", () => {
        const url = this._iaetUrl;
        if (!isApiUrl(url)) return;

        const resHeaders: Record<string, string> = {};
        const rawHeaders = this.getAllResponseHeaders();
        for (const line of rawHeaders.trim().split(/[\r\n]+/)) {
          const idx = line.indexOf(": ");
          if (idx >= 0) {
            resHeaders[line.slice(0, idx).toLowerCase()] = line.slice(idx + 2);
          }
        }

        const ct = resHeaders["content-type"] ?? "";
        let resBody: string | null = null;
        if (ct.includes("text") || ct.includes("json") || ct.includes("javascript")) {
          resBody = this.responseText;
        }

        postCapture({
          id: this._iaetId,
          timestamp: new Date(this._iaetStartTime).toISOString(),
          httpMethod: this._iaetMethod,
          url,
          requestHeaders: this._iaetReqHeaders,
          requestBody: this._iaetReqBody,
          responseStatus: this.status,
          responseHeaders: resHeaders,
          responseBody: resBody,
          durationMs: Date.now() - this._iaetStartTime,
        });
      });

      super.send(body);
    }
  }

  window.XMLHttpRequest = PatchedXHR;

  // ---- Patch WebSocket ----

  const OriginalWebSocket = window.WebSocket;

  function postWs(msg: IaetWsMessage): void {
    window.postMessage(msg, "*");
  }

  class PatchedWebSocket extends OriginalWebSocket {
    private _iaetId: string;
    private _iaetUrl: string;

    constructor(url: string | URL, protocols?: string | string[]) {
      super(url, protocols);
      this._iaetId = generateId();
      this._iaetUrl = typeof url === "string" ? url : url.toString();

      const wsId = this._iaetId;
      const wsUrl = this._iaetUrl;

      postWs({
        type: INJECT_WS_MSG,
        action: "open",
        payload: {
          id: wsId,
          url: wsUrl,
          timestamp: new Date().toISOString(),
          protocol: typeof protocols === "string" ? protocols : protocols?.[0],
        },
      });

      this.addEventListener("message", (event: MessageEvent) => {
        let textPayload: string | null = null;
        let binarySize = 0;

        if (typeof event.data === "string") {
          textPayload = event.data.length > 8192 ? event.data.slice(0, 8192) : event.data;
        } else if (event.data instanceof ArrayBuffer) {
          binarySize = event.data.byteLength;
          // Try to decode binary as UTF-8 text (SIP messages are text even in binary frames)
          try {
            const decoded = new TextDecoder("utf-8", { fatal: true }).decode(event.data);
            textPayload = decoded.length > 8192 ? decoded.slice(0, 8192) : decoded;
          } catch {
            // Truly binary data — keep as size only
          }
        } else if (event.data instanceof Blob) {
          binarySize = event.data.size;
          // Read blob as text asynchronously
          event.data.text().then((text) => {
            postWs({
              type: INJECT_WS_MSG,
              action: "frame",
              payload: {
                id: wsId,
                url: wsUrl,
                timestamp: new Date().toISOString(),
                direction: "Received",
                textPayload: text.length > 8192 ? text.slice(0, 8192) : text,
                binarySize: text.length,
              },
            });
          }).catch(() => {});
          // Still post immediately with size for non-decodable blobs
          if (binarySize > 0) return; // skip double-post; blob handler above will post
        }

        postWs({
          type: INJECT_WS_MSG,
          action: "frame",
          payload: {
            id: wsId,
            url: wsUrl,
            timestamp: new Date().toISOString(),
            direction: "Received",
            textPayload,
            binarySize,
          },
        });
      });

      this.addEventListener("close", () => {
        postWs({
          type: INJECT_WS_MSG,
          action: "close",
          payload: {
            id: wsId,
            url: wsUrl,
            timestamp: new Date().toISOString(),
          },
        });
      });
    }

    // Intercept send() to capture outgoing frames
    override send(data: string | ArrayBufferLike | Blob | ArrayBufferView): void {
      let textPayload: string | null = null;
      let binarySize = 0;

      if (typeof data === "string") {
        textPayload = data.length > 8192 ? data.slice(0, 8192) : data;
      } else if (data instanceof ArrayBuffer) {
        binarySize = data.byteLength;
        try {
          const decoded = new TextDecoder("utf-8", { fatal: true }).decode(data);
          textPayload = decoded.length > 8192 ? decoded.slice(0, 8192) : decoded;
        } catch { /* truly binary */ }
      } else if (data instanceof Blob) {
        binarySize = data.size;
      } else if (ArrayBuffer.isView(data)) {
        binarySize = data.byteLength;
        try {
          const decoded = new TextDecoder("utf-8", { fatal: true }).decode(data);
          textPayload = decoded.length > 8192 ? decoded.slice(0, 8192) : decoded;
        } catch { /* truly binary */ }
      }

      postWs({
        type: INJECT_WS_MSG,
        action: "frame",
        payload: {
          id: this._iaetId,
          url: this._iaetUrl,
          timestamp: new Date().toISOString(),
          direction: "Sent",
          textPayload,
          binarySize,
        },
      });

      super.send(data);
    }
  }

  // Preserve static properties (CONNECTING, OPEN, CLOSING, CLOSED)
  Object.defineProperty(PatchedWebSocket, "CONNECTING", { value: 0 });
  Object.defineProperty(PatchedWebSocket, "OPEN", { value: 1 });
  Object.defineProperty(PatchedWebSocket, "CLOSING", { value: 2 });
  Object.defineProperty(PatchedWebSocket, "CLOSED", { value: 3 });

  window.WebSocket = PatchedWebSocket as unknown as typeof WebSocket;

  // ---- Patch RTCPeerConnection ----

  const OriginalRTCPeerConnection = window.RTCPeerConnection;

  function postRtc(msg: IaetRtcMessage): void {
    window.postMessage(msg, "*");
  }

  class PatchedRTCPeerConnection extends OriginalRTCPeerConnection {
    private _iaetId: string;

    constructor(config?: RTCConfiguration) {
      super(config);
      this._iaetId = generateId();

      postRtc({
        type: INJECT_RTC_MSG,
        action: "create",
        payload: {
          id: this._iaetId,
          timestamp: new Date().toISOString(),
          config: config ? JSON.stringify({
            iceServers: config.iceServers?.map(s => ({
              urls: s.urls,
              // Never capture credentials
            })),
            bundlePolicy: config.bundlePolicy,
            rtcpMuxPolicy: config.rtcpMuxPolicy,
          }) : undefined,
        },
      });

      // Track ICE candidates
      this.addEventListener("icecandidate", (event) => {
        if (event.candidate) {
          postRtc({
            type: INJECT_RTC_MSG,
            action: "localIceCandidate",
            payload: {
              id: this._iaetId,
              timestamp: new Date().toISOString(),
              candidate: event.candidate.candidate,
            },
          });
        }
      });

      // Track connection state changes
      this.addEventListener("connectionstatechange", () => {
        postRtc({
          type: INJECT_RTC_MSG,
          action: "stateChange",
          payload: {
            id: this._iaetId,
            timestamp: new Date().toISOString(),
            state: this.connectionState,
          },
        });
      });
    }

    override async setLocalDescription(desc?: RTCLocalSessionDescriptionInit): Promise<void> {
      postRtc({
        type: INJECT_RTC_MSG,
        action: "setLocalDesc",
        payload: {
          id: this._iaetId,
          timestamp: new Date().toISOString(),
          sdpType: desc?.type,
          sdp: desc?.sdp?.length && desc.sdp.length > 16384 ? desc.sdp.slice(0, 16384) : desc?.sdp,
        },
      });
      return super.setLocalDescription(desc);
    }

    override async setRemoteDescription(desc: RTCSessionDescriptionInit): Promise<void> {
      postRtc({
        type: INJECT_RTC_MSG,
        action: "setRemoteDesc",
        payload: {
          id: this._iaetId,
          timestamp: new Date().toISOString(),
          sdpType: desc.type,
          sdp: desc.sdp?.length && desc.sdp.length > 16384 ? desc.sdp.slice(0, 16384) : desc.sdp,
        },
      });
      return super.setRemoteDescription(desc);
    }

    override async addIceCandidate(candidate?: RTCIceCandidateInit | RTCIceCandidate): Promise<void> {
      if (candidate) {
        const c = candidate instanceof RTCIceCandidate ? candidate : candidate;
        postRtc({
          type: INJECT_RTC_MSG,
          action: "addIceCandidate",
          payload: {
            id: this._iaetId,
            timestamp: new Date().toISOString(),
            candidate: typeof c.candidate === "string" ? c.candidate : JSON.stringify(c),
          },
        });
      }
      return super.addIceCandidate(candidate);
    }
  }

  window.RTCPeerConnection = PatchedRTCPeerConnection as unknown as typeof RTCPeerConnection;

  // ---- Patch EventSource (SSE) ----

  const OriginalEventSource = window.EventSource;
  const INJECT_SSE_MSG = "__iaet_sse__";

  interface IaetSseMessage {
    type: typeof INJECT_SSE_MSG;
    action: "open" | "message" | "error" | "close";
    payload: {
      id: string;
      url: string;
      timestamp: string;
      eventType?: string;
      data?: string;
    };
  }

  function postSse(msg: IaetSseMessage): void {
    window.postMessage(msg, "*");
  }

  class PatchedEventSource extends OriginalEventSource {
    private _iaetId: string;
    private _iaetUrl: string;

    constructor(url: string | URL, eventSourceInitDict?: EventSourceInit) {
      super(url, eventSourceInitDict);
      this._iaetId = generateId();
      this._iaetUrl = typeof url === "string" ? url : url.toString();

      const sseId = this._iaetId;
      const sseUrl = this._iaetUrl;

      postSse({
        type: INJECT_SSE_MSG,
        action: "open",
        payload: { id: sseId, url: sseUrl, timestamp: new Date().toISOString() },
      });

      this.addEventListener("message", (event: MessageEvent) => {
        const data = typeof event.data === "string"
          ? (event.data.length > 8192 ? event.data.slice(0, 8192) : event.data)
          : null;
        postSse({
          type: INJECT_SSE_MSG,
          action: "message",
          payload: { id: sseId, url: sseUrl, timestamp: new Date().toISOString(), eventType: "message", data },
        });
      });

      this.addEventListener("error", () => {
        postSse({
          type: INJECT_SSE_MSG,
          action: "error",
          payload: { id: sseId, url: sseUrl, timestamp: new Date().toISOString() },
        });
      });
    }
  }

  Object.defineProperty(PatchedEventSource, "CONNECTING", { value: 0 });
  Object.defineProperty(PatchedEventSource, "OPEN", { value: 1 });
  Object.defineProperty(PatchedEventSource, "CLOSED", { value: 2 });

  window.EventSource = PatchedEventSource as unknown as typeof EventSource;
})();
