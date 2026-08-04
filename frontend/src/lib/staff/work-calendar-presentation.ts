import type { CalendarEvent } from "@/components/ui/calendar/types";
import { appointmentStatusLabel } from "./status";
import { testDriveStatusLabels } from "@/lib/test-drives/account";
import { saleStatusLabels } from "@/lib/sales/contracts";
import { workshopStatusLabel, workshopStatuses } from "@/lib/inspection/workshop-contracts";
import type { WorkCalendarSnapshot, WorkItem, ScheduledWork } from "./work-calendar";

export function workItemLabel(item: WorkItem) {
  if (item.kind === "Intake") {
    return appointmentStatusLabel(item.status);
  }
  if (item.kind === "Workshop") {
    const status = workshopStatuses.find((value) => value === item.status);
    return status ? workshopStatusLabel(status) : item.status;
  }
  const labels: Record<string, string> =
    item.kind === "Sale" ? saleStatusLabels : testDriveStatusLabels;
  return labels[item.status] ?? item.status;
}

export function workItemKind(item: WorkItem) {
  return item.kind === "TestDrive" ? "Test drive" : item.kind;
}

export function workItemHref(item: WorkItem) {
  if (item.kind === "Workshop") {
    return `/dashboard/inspection/${item.inspectionId}`;
  }
  if (item.kind === "Sale") {
    return `/dashboard/sales/${item.id}`;
  }
  if (item.kind === "TestDrive") {
    return `/dashboard/test-drives?booking=${item.id}`;
  }
  return `/dashboard/intake?appointment=${item.id}`;
}

export function workCalendarEvents(snapshot: WorkCalendarSnapshot): CalendarEvent<ScheduledWork>[] {
  return snapshot.items.map((item) => ({
    id: `${item.kind}:${item.id}`,
    title: `${workItemKind(item)} Â· ${item.registrationPlate}${snapshot.branchId === null ? ` Â· ${item.branchName}` : ""}`,
    startsAt: item.startsAt,
    endsAt: item.endsAt,
    label: workItemLabel(item),
    tone: scheduledTone(item.status),
    data: item,
  }));
}

function scheduledTone(status: string): "info" | "success" | "neutral" {
  if (status === "Completed" || status === "CheckedIn") {
    return "success";
  }
  return status === "Booked" || status === "InProgress" ? "info" : "neutral";
}
