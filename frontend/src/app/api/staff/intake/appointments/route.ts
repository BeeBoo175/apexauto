import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import { relayIntake } from "@/lib/intake/routes";
import { parseWorklistPage } from "@/lib/staff/parse";

export function GET(request: Request) {
  const query = new URL(request.url).searchParams;
  const allowed = new URLSearchParams({ pageSize: String(DEFAULT_PAGE_SIZE) });
  for (const name of [
    "from",
    "to",
    "status",
    "branchId",
    "page",
    "pageSize",
    "search",
    "registration",
    "vehicle",
    "customer",
    "sort",
    "direction",
  ]) {
    if (query.has(name)) {
      allowed.set(name, query.get(name)!);
    }
  }
  return relayIntake(request, `/api/staff/intake/appointments?${allowed}`, parseWorklistPage, true);
}
