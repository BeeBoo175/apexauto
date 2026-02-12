import { downloadCsv } from "@/components/ui/data-table/csv";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { StaffFilters } from "./contracts";
import { staffQuery } from "./query";

export async function exportWorklist(
  filters: StaffFilters,
  actor: CurrentActor,
  signal: AbortSignal,
  notify: (message: string) => void,
) {
  const url = `/api/staff/intake/appointments/export?${staffQuery(filters)}`;
  const headers = { "X-Expected-Actor": actorIdentity(actor) };
  let response = await fetch(url, {
    headers,
    cache: "no-store",
    signal: AbortSignal.any([signal, AbortSignal.timeout(25000)]),
  });
  let downloadSignal = signal;
  if (response.status === 422) {
    await response.body?.cancel();
    signal.throwIfAborted();
    notify("This large export is being prepared. The download will start automatically.");
    downloadSignal = AbortSignal.any([signal, AbortSignal.timeout(130000)]);
    response = await fetch(url, {
      method: "POST",
      headers,
      cache: "no-store",
      signal: downloadSignal,
    });
  }
  if (response.status === 401 || response.status === 409) {
    window.dispatchEvent(new Event("apexauto-session-invalid"));
  }
  if (!response.ok) {
    await response.body?.cancel();
    if (response.status === 429) {
      throw new Error(
        "Another export is running or the request limit was reached. Try again shortly.",
      );
    }
    if (response.status === 413) {
      throw new Error("The export exceeds the download size limit. Narrow the filters.");
    }
    throw new Error("CSV export could not be completed. Existing rows remain shown.");
  }
  if (!response.headers.get("content-type")?.startsWith("text/csv")) {
    throw new Error("The CSV response could not be confirmed.");
  }
  const blob = await response.blob();
  downloadSignal.throwIfAborted();
  downloadCsv(blob, "intake-appointments-full.csv");
}
