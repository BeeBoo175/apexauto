import { relayWorkCalendar } from "@/lib/staff/work-calendar-routes";

export function GET(request: Request) {
  return relayWorkCalendar(request, true);
}
