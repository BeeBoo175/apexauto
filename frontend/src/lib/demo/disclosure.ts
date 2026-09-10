export const DISCLOSURE_STORAGE_KEY = "apexauto.demoDisclosureAcknowledged";

export type DisclosureState = "unknown" | "required" | "acknowledged";

const listeners = new Set<() => void>();

// Holds the acknowledgement for this page session when browser storage is unavailable.
let acknowledgedInMemory = false;

export function subscribeToDisclosure(callback: () => void) {
  listeners.add(callback);
  window.addEventListener("storage", callback);
  return () => {
    listeners.delete(callback);
    window.removeEventListener("storage", callback);
  };
}

export function readDisclosureState(): DisclosureState {
  if (acknowledgedInMemory) {
    return "acknowledged";
  }
  try {
    return localStorage.getItem(DISCLOSURE_STORAGE_KEY) === "true" ? "acknowledged" : "required";
  } catch {
    return "required";
  }
}

// Before hydration the visit is unknown, so the server markup and the first client render show no dialog.
export function readServerDisclosureState(): DisclosureState {
  return "unknown";
}

export function acknowledgeDisclosure() {
  acknowledgedInMemory = true;
  try {
    localStorage.setItem(DISCLOSURE_STORAGE_KEY, "true");
  } catch {
    // Storage is blocked: the acknowledgement lasts for this page session and shows again on the next load.
  }
  listeners.forEach((listener) => listener());
}
