// popup.ts — popup logic: communicates with background service worker

import type { PopupState, IaetFile } from "./types";

type PopupMessage =
  | { type: "POPUP_START"; sessionName: string }
  | { type: "POPUP_STOP" }
  | { type: "POPUP_GET_STATE" }
  | { type: "POPUP_EXPORT" }
  | { type: "POPUP_CLEAR" }
  | { type: "POPUP_POST"; serverUrl: string };

function sendMessage<T>(msg: PopupMessage): Promise<T> {
  return chrome.runtime.sendMessage(msg) as Promise<T>;
}

// ---- DOM refs ----

const btnStart = document.getElementById("btn-start") as HTMLButtonElement;
const btnStop = document.getElementById("btn-stop") as HTMLButtonElement;
const btnExport = document.getElementById("btn-export") as HTMLButtonElement;
const btnClear = document.getElementById("btn-clear") as HTMLButtonElement;
const btnPost = document.getElementById("btn-post") as HTMLButtonElement;
const sessionNameInput = document.getElementById("session-name") as HTMLInputElement;
const serverUrlInput = document.getElementById("server-url") as HTMLInputElement;
const statRequests = document.getElementById("stat-requests")!;
const statEndpoints = document.getElementById("stat-endpoints")!;
const recordingDot = document.getElementById("recording-dot")!;
const statusMsg = document.getElementById("status-msg")!;

// ---- Apply state to UI ----

function applyState(state: PopupState): void {
  const { recording, requestCount, endpointCount, sessionName } = state;

  statRequests.textContent = String(requestCount);
  statEndpoints.textContent = String(endpointCount);

  if (sessionNameInput.value === "" || !recording) {
    sessionNameInput.value = sessionName;
  }

  sessionNameInput.disabled = recording;

  btnStart.disabled = recording;
  btnStop.disabled = !recording;

  const hasData = requestCount > 0;
  btnExport.disabled = !hasData;
  btnPost.disabled = !hasData;

  if (recording) {
    recordingDot.classList.add("recording");
  } else {
    recordingDot.classList.remove("recording");
  }
}

function showStatus(msg: string, kind: "ok" | "err" | "info" = "info"): void {
  statusMsg.textContent = msg;
  statusMsg.className = kind;
  if (kind !== "err") {
    setTimeout(() => { statusMsg.textContent = ""; statusMsg.className = ""; }, 3000);
  }
}

function downloadIaetFile(iaetFile: IaetFile, filename: string): void {
  const json = JSON.stringify(iaetFile, null, 2);
  const blob = new Blob([json], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

// ---- Button handlers ----

btnStart.addEventListener("click", async () => {
  const name = sessionNameInput.value.trim() || "capture";
  const state = await sendMessage<PopupState>({ type: "POPUP_START", sessionName: name });
  applyState(state);
  showStatus("Recording started", "ok");
});

btnStop.addEventListener("click", async () => {
  const state = await sendMessage<PopupState>({ type: "POPUP_STOP" });
  applyState(state);
  showStatus("Recording stopped", "ok");
});

btnExport.addEventListener("click", async () => {
  const result = await sendMessage<{ iaetFile: IaetFile }>({ type: "POPUP_EXPORT" });
  const name = sessionNameInput.value.trim() || "capture";
  downloadIaetFile(result.iaetFile, `${name}.iaet.json`);
  showStatus("File downloaded", "ok");
});

btnClear.addEventListener("click", async () => {
  const state = await sendMessage<PopupState>({ type: "POPUP_CLEAR" });
  applyState(state);
  showStatus("Cleared", "info");
});

btnPost.addEventListener("click", async () => {
  const serverUrl = serverUrlInput.value.trim();
  if (!serverUrl) {
    showStatus("Enter a server URL", "err");
    return;
  }

  btnPost.disabled = true;
  showStatus("Posting…");

  const result = await sendMessage<{ ok: boolean }>({ type: "POPUP_POST", serverUrl });

  if (result.ok) {
    showStatus("Posted successfully", "ok");
  } else {
    showStatus("POST failed — is iaet import --listen running?", "err");
  }

  btnPost.disabled = false;
});

// ---- Init ----

void (async () => {
  const state = await sendMessage<PopupState>({ type: "POPUP_GET_STATE" });
  applyState(state);
})();
