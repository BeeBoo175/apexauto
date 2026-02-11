import { relayIntake } from "@/lib/intake/routes";
import { parseCalendar } from "@/lib/staff/calendar";

export function GET(request: Request) {
  const input = new URL(request.url).searchParams;
  const query = new URLSearchParams();
  for (const key of ["from", "to", "status", "branchId"]) {
    if (input.has(key)) {
      query.set(key, input.get(key)!);
    }
  }
  return relayIntake(
    request,
    `/api/staff/intake/calendar?${query}`,
    parseCalendar,
    true,
    1024 * 1024,
  );
}
