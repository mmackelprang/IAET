// inject.ts — injected directly into the page context (not the extension context)
// Monkey-patches fetch and XMLHttpRequest to capture API traffic.
// Communicates back to the content script via window.postMessage.

(function () {
  const INJECT_MSG = "__iaet_request__";
  const INJECT_WS_MSG = "__iaet_ws__";

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
        } else if (event.data instanceof Blob) {
          binarySize = event.data.size;
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
      } else if (data instanceof Blob) {
        binarySize = data.size;
      } else if (ArrayBuffer.isView(data)) {
        binarySize = data.byteLength;
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
})();
