import { appointmentStatusLabel, appointmentStatusTone } from "./status";
import type { CalendarEvent } from "@/components/ui/calendar/types";
import type { WorklistItem } from "./contracts";
import type { CalendarSnapshot } from "./calendar";

export function calendarEvents(snapshot: CalendarSnapshot): CalendarEvent<WorklistItem>[] {
  return snapshot.items.map((item) => {
    return {
      id: item.id,
      title: item.registrationPlate,
      startsAt: item.startsAt,
      endsAt: item.endsAt,
      label: appointmentStatusLabel(item.status),
      tone: appointmentStatusTone(item.status),
      data: item,
    };
  });
}
