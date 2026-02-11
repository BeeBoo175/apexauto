import { appointmentStatusLabel } from "@/lib/staff/status";
import type { CalendarEvent } from "@/components/ui/calendar/types";
import type { WorklistItem } from "./contracts";
import type { CalendarSnapshot } from "./calendar";

export function calendarEvents(snapshot: CalendarSnapshot): CalendarEvent<WorklistItem>[] {
  return snapshot.items.map((item) => {
    let tone: CalendarEvent["tone"] = "info";
    if (item.status === "CheckedIn") {
      tone = "success";
    }
    if (item.status === "Cancelled" || item.status === "NoShow") {
      tone = "neutral";
    }
    return {
      id: item.id,
      title: item.registrationPlate,
      startsAt: item.startsAt,
      endsAt: item.endsAt,
      label: appointmentStatusLabel(item.status),
      tone,
      data: item,
    };
  });
}
