import { relayIntake } from "@/lib/intake/routes";
import { parseAppointment, parseBooking, parsePage, parseQuote } from "@/lib/intake/parse";
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
  const allowed = new URLSearchParams();
  for (const name of ["from", "to", "page"]) {
    if (query.has(name)) {
      allowed.set(name, query.get(name)!);
    }
  }
  return relayIntake(
    request,
    `/api/intake/appointments?${allowed}&pageSize=20`,
    (value) => parsePage(value, parseAppointment),
    true,
  );
}
