// Exports captured requests to .iaet.json format

import type { IaetFile, IaetRequest, IaetSession } from "./types";

function generateUuid(): string {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export function exportToIaet(
  requests: IaetRequest[],
  sessionName: string,
  targetApplication: string
): IaetFile {
  const now = new Date().toISOString();
  const sessionId = generateUuid();

  const session: IaetSession = {
    id: sessionId,
    name: sessionName,
    targetApplication,
    profile: "default",
    startedAt: requests.length > 0 ? requests[0].timestamp : now,
    stoppedAt: requests.length > 0 ? requests[requests.length - 1].timestamp : now,
    capturedBy: "iaet-devtools/0.1.0",
  };

  // Stamp all requests with the session ID
  const stamped = requests.map((r) => ({ ...r, sessionId }));

  return {
    iaetVersion: "1.0",
    exportedAt: now,
    session,
    requests: stamped,
    streams: [],
  };
}

export function downloadIaetFile(iaetFile: IaetFile, filename?: string): void {
  const json = JSON.stringify(iaetFile, null, 2);
  const blob = new Blob([json], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename ?? `capture-${Date.now()}.iaet.json`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
