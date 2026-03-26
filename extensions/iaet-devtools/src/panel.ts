// Panel logic: listens to chrome.devtools.network, groups by endpoint, renders UI

import {
  type IaetRequest,
  type EndpointGroup,
  normalizeEndpoint,
  isAnalyticUrl,
} from "./types";
import { exportToIaet, downloadIaetFile } from "./export";

// ---- HAR entry types (chrome-types only exposes getContent; we augment locally) ----

interface HarNameValue {
  name: string;
  value: string;
}

interface HarRequest {
  method: string;
  url: string;
  headers: HarNameValue[];
  postData?: { text?: string };
}

interface HarResponse {
  status: number;
  headers: HarNameValue[];
}

interface HarEntry extends chrome.devtools.network.Request {
  request: HarRequest;
  response: HarResponse;
  time: number;
  _resourceType?: string;
}

// ---- State ----

const allRequests: IaetRequest[] = [];
const endpointMap = new Map<string, EndpointGroup>();
let selectedRequestId: string | null = null;
let filterAnalytics = true;
let requestIdCounter = 0;

function generateId(): string {
  return `devtools-${Date.now()}-${++requestIdCounter}`;
}

// ---- Network listener ----

chrome.devtools.network.onRequestFinished.addListener((rawEntry) => {
  const harEntry = rawEntry as unknown as HarEntry;
  const req = harEntry.request;
  const res = harEntry.response;

  // Only capture XHR/Fetch (skip navigation, stylesheet, script, etc.)
  if (harEntry._resourceType && !["xhr", "fetch"].includes(harEntry._resourceType)) return;

  const url = req.url;
  if (!url.startsWith("http://") && !url.startsWith("https://")) return;

  // Get response body asynchronously
  harEntry.getContent((body) => {
    const reqHeaders: Record<string, string> = {};
    for (const h of req.headers) reqHeaders[h.name] = h.value;

    const resHeaders: Record<string, string> = {};
    for (const h of res.headers) resHeaders[h.name] = h.value;

    const iaetReq: IaetRequest = {
      id: generateId(),
      sessionId: "",
      timestamp: new Date().toISOString(),
      httpMethod: req.method,
      url,
      requestHeaders: reqHeaders,
      requestBody: req.postData?.text ?? null,
      responseStatus: res.status,
      responseHeaders: resHeaders,
      responseBody: body || null,
      durationMs: Math.round(harEntry.time),
      tag: null,
    };

    allRequests.push(iaetReq);
    addToEndpointMap(iaetReq);
    renderEndpointList();
    updateRequestCount();
  });
});

// ---- Endpoint grouping ----

function addToEndpointMap(req: IaetRequest): void {
  const sig = normalizeEndpoint(req.httpMethod, req.url);
  const existing = endpointMap.get(sig);
  if (existing) {
    existing.requests.push(req);
  } else {
    const parts = sig.split(" ", 2);
    endpointMap.set(sig, {
      signature: sig,
      method: parts[0],
      pathTemplate: parts[1] ?? sig,
      requests: [req],
      tags: [],
    });
  }
}

function getVisibleGroups(): EndpointGroup[] {
  const groups = Array.from(endpointMap.values());
  if (!filterAnalytics) return groups;
  return groups.filter((g) => !isAnalyticUrl(g.requests[0]?.url ?? ""));
}

// ---- Rendering ----

function renderEndpointList(): void {
  const container = document.getElementById("endpoint-list")!;
  const emptyEl = document.getElementById("empty-endpoints");
  const groups = getVisibleGroups();

  if (groups.length === 0) {
    container.innerHTML = "";
    container.appendChild(emptyEl!);
    if (emptyEl) emptyEl.style.display = "";
    return;
  }

  if (emptyEl) emptyEl.remove();
  container.innerHTML = "";

  for (const group of groups) {
    const groupEl = document.createElement("div");
    groupEl.className = "endpoint-group";
    groupEl.dataset["sig"] = group.signature;

    const header = document.createElement("div");
    header.className = "endpoint-header";

    const badge = document.createElement("span");
    badge.className = `method-badge method-${group.method.toLowerCase()}`;
    badge.textContent = group.method;
    if (!["GET", "POST", "PUT", "PATCH", "DELETE"].includes(group.method)) {
      badge.classList.remove(`method-${group.method.toLowerCase()}`);
      badge.classList.add("method-other");
    }

    const path = document.createElement("span");
    path.className = "endpoint-path";
    path.textContent = group.pathTemplate;
    path.title = group.pathTemplate;

    const count = document.createElement("span");
    count.className = "endpoint-count";
    count.textContent = String(group.requests.length);

    header.appendChild(badge);
    header.appendChild(path);
    header.appendChild(count);

    // Expand/collapse sub-list on header click
    const sublist = document.createElement("div");
    sublist.className = "request-sublist";

    header.addEventListener("click", () => {
      sublist.classList.toggle("open");
      header.classList.toggle("selected");
    });

    for (const r of group.requests) {
      sublist.appendChild(buildRequestItem(r));
    }

    groupEl.appendChild(header);
    groupEl.appendChild(sublist);
    container.appendChild(groupEl);
  }
}

function buildRequestItem(req: IaetRequest): HTMLElement {
  const item = document.createElement("div");
  item.className = "request-item";
  item.dataset["id"] = req.id;

  if (req.id === selectedRequestId) {
    item.classList.add("selected");
  }

  const status = document.createElement("span");
  status.className = "request-status " + statusClass(req.responseStatus);
  status.textContent = String(req.responseStatus);

  const ts = document.createElement("span");
  ts.textContent = new URL(req.url).pathname.slice(0, 50);
  ts.style.flex = "1";
  ts.style.overflow = "hidden";
  ts.style.textOverflow = "ellipsis";
  ts.style.whiteSpace = "nowrap";

  const dur = document.createElement("span");
  dur.className = "request-duration";
  dur.textContent = `${req.durationMs}ms`;

  item.appendChild(status);
  item.appendChild(ts);
  item.appendChild(dur);

  if (req.tag) {
    const tagBadge = document.createElement("span");
    tagBadge.className = "request-tag-badge";
    tagBadge.textContent = req.tag;
    item.appendChild(tagBadge);
  }

  item.addEventListener("click", () => {
    selectedRequestId = req.id;
    renderDetailPane(req);
    // Update selected class
    document.querySelectorAll(".request-item.selected").forEach((el) => el.classList.remove("selected"));
    item.classList.add("selected");
  });

  return item;
}

function statusClass(code: number): string {
  if (code >= 500) return "status-5xx";
  if (code >= 400) return "status-4xx";
  if (code >= 300) return "status-3xx";
  return "status-2xx";
}

function renderDetailPane(req: IaetRequest): void {
  const pane = document.getElementById("detail-pane")!;
  const emptyDetail = document.getElementById("empty-detail");
  if (emptyDetail) emptyDetail.remove();

  pane.innerHTML = "";

  // URL
  const urlSection = makeSection("Request");
  const urlEl = document.createElement("div");
  urlEl.className = "detail-url";
  urlEl.textContent = `${req.httpMethod} ${req.url}`;
  urlSection.appendChild(urlEl);

  const metaTable = document.createElement("table");
  metaTable.className = "kv-table";
  addKvRow(metaTable, "Status", `${req.responseStatus}`);
  addKvRow(metaTable, "Duration", `${req.durationMs} ms`);
  addKvRow(metaTable, "Time", req.timestamp);
  urlSection.appendChild(metaTable);
  pane.appendChild(urlSection);

  // Tag editor
  const tagSection = makeSection("Tag");
  const tagForm = document.createElement("div");
  tagForm.className = "tag-form";
  const tagInput = document.createElement("input");
  tagInput.type = "text";
  tagInput.placeholder = "Add tag…";
  tagInput.value = req.tag ?? "";
  const tagBtn = document.createElement("button");
  tagBtn.textContent = "Save";
  tagBtn.addEventListener("click", () => {
    req.tag = tagInput.value.trim() || null;
    renderEndpointList();
    renderDetailPane(req);
  });
  tagForm.appendChild(tagInput);
  tagForm.appendChild(tagBtn);
  tagSection.appendChild(tagForm);
  pane.appendChild(tagSection);

  // Request headers
  if (Object.keys(req.requestHeaders).length > 0) {
    const reqHdrSection = makeSection("Request Headers");
    reqHdrSection.appendChild(buildKvTable(req.requestHeaders));
    pane.appendChild(reqHdrSection);
  }

  // Request body
  if (req.requestBody) {
    const reqBodySection = makeSection("Request Body");
    reqBodySection.appendChild(buildBodyBlock(req.requestBody));
    pane.appendChild(reqBodySection);
  }

  // Response headers
  if (Object.keys(req.responseHeaders).length > 0) {
    const resHdrSection = makeSection("Response Headers");
    resHdrSection.appendChild(buildKvTable(req.responseHeaders));
    pane.appendChild(resHdrSection);
  }

  // Response body
  if (req.responseBody) {
    const resBodySection = makeSection("Response Body");
    resBodySection.appendChild(buildBodyBlock(req.responseBody, true));
    pane.appendChild(resBodySection);
  }
}

function makeSection(title: string): HTMLElement {
  const div = document.createElement("div");
  div.className = "detail-section";
  const h3 = document.createElement("h3");
  h3.textContent = title;
  div.appendChild(h3);
  return div;
}

function buildKvTable(obj: Record<string, string>): HTMLElement {
  const table = document.createElement("table");
  table.className = "kv-table";
  for (const [k, v] of Object.entries(obj)) {
    addKvRow(table, k, v);
  }
  return table;
}

function addKvRow(table: HTMLElement, key: string, value: string): void {
  const tr = document.createElement("tr");
  const tdKey = document.createElement("td");
  tdKey.textContent = key;
  const tdVal = document.createElement("td");
  tdVal.textContent = value;
  tr.appendChild(tdKey);
  tr.appendChild(tdVal);
  table.appendChild(tr);
}

function buildBodyBlock(body: string, tryPrettyJson = false): HTMLElement {
  const pre = document.createElement("pre");
  pre.className = "body-block";
  if (tryPrettyJson) {
    try {
      pre.textContent = JSON.stringify(JSON.parse(body), null, 2);
    } catch {
      pre.textContent = body;
    }
  } else {
    pre.textContent = body;
  }
  return pre;
}

function updateRequestCount(): void {
  const el = document.getElementById("request-count");
  if (el) el.textContent = `${allRequests.length} requests`;
}

// ---- Button handlers ----

document.getElementById("btn-clear")?.addEventListener("click", () => {
  allRequests.length = 0;
  endpointMap.clear();
  selectedRequestId = null;
  renderEndpointList();
  updateRequestCount();
  const pane = document.getElementById("detail-pane")!;
  pane.innerHTML = "";
  const emptyDetail = document.createElement("div");
  emptyDetail.className = "empty-state";
  emptyDetail.id = "empty-detail";
  emptyDetail.textContent = "Select a request to inspect";
  pane.appendChild(emptyDetail);
});

document.getElementById("chk-filter")?.addEventListener("change", (e) => {
  filterAnalytics = (e.target as HTMLInputElement).checked;
  renderEndpointList();
});

document.getElementById("btn-export")?.addEventListener("click", () => {
  const visible = getVisibleGroups().flatMap((g) => g.requests);
  if (visible.length === 0) {
    alert("No requests to export.");
    return;
  }
  const sessionNameEl = document.getElementById("session-name") as HTMLInputElement;
  const sessionName = sessionNameEl.value.trim() || "devtools-capture";
  const iaetFile = exportToIaet(visible, sessionName, window.location.hostname || "unknown");
  downloadIaetFile(iaetFile, `${sessionName}.iaet.json`);
});
