import { downloadCsv } from "@/components/ui/data-table/csv";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { StaffFilters } from "./contracts";
import { staffQuery } from "./query";

export async function exportWorklist(
  filters: StaffFilters,
  actor: CurrentActor,
  signal: AbortSignal,
) {
  const response = await fetch(`/api/staff/intake/appointments/export?${staffQuery(filters)}`, {
    headers: { "X-Expected-Actor": actorIdentity(actor) },
    cache: "no-store",
    signal: AbortSignal.any([signal, AbortSignal.timeout(25000)]),
  });
  if (response.status === 401 || response.status === 409) {
    window.dispatchEvent(new Event("apexauto-session-invalid"));
  }
  if (!response.ok) {
    if (response.status === 422) {
      throw new Error("More than 10,000 appointments match. Narrow the filters before exporting.");
    }
    throw new Error("CSV export could not be completed. Existing rows remain shown.");
  }
  if (!response.headers.get("content-type")?.startsWith("text/csv")) {
    throw new Error("The CSV response could not be confirmed.");
  }
  const blob = await response.blob();
  signal.throwIfAborted();
  downloadCsv(blob, "intake-appointments-full.csv");
}
