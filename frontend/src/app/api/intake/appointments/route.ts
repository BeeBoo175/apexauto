import { ACCOUNT_PAGE_SIZE } from "@/lib/pagination";
import { relayIntake } from "@/lib/intake/routes";
import { parseAppointment, parseBooking, parseQuote } from "@/lib/intake/parse";
import { parsePage } from "@/lib/api";
export function POST(request: Request) {
  return relayIntake(
    request,
    "/api/intake/appointments",
    (value) => parseBooking(value) ?? parseQuote(value),
    true,
  );
}
export function GET(request: Request) {
  const query = new URL(request.url).searchParams;
  const allowed = new URLSearchParams({ pageSize: String(ACCOUNT_PAGE_SIZE) });
  for (const name of ["from", "to", "page", "pageSize"]) {
    if (query.has(name)) {
      allowed.set(name, query.get(name)!);
    }
  }
  return relayIntake(
    request,
    `/api/intake/appointments?${allowed}`,
    (value) => parsePage(value, parseAppointment, ACCOUNT_PAGE_SIZE),
    true,
  );
}
