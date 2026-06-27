"use client";

import { HistoryActor } from "@/components/staff/HistoryActor";
import { StaffHistorySection } from "@/components/staff/StaffHistorySection";
import { StaffHistoryProgress } from "@/components/staff/StaffHistoryProgress";
import { StaffTimeline, StaffTimelineEvent } from "@/components/staff/StaffTimeline";
import { useStaffHistory } from "@/hooks/use-staff-history";
import { privateApiFetch } from "@/lib/api-browser";
import {
  appendTestDriveHistory,
  parseStaffTestDriveHistory,
  testDriveEventSummary,
  testDriveEventTitles,
  testDriveHistoryPath,
} from "@/lib/test-drives/staff-history";

// Every retained step of the booking, newest first: the booking, each ownership change, the start, the outcome
// and any cancellation. The Details tab keeps only the current facts.
export function StaffTestDriveTimeline({
  bookingId,
  disabled,
}: {
  bookingId: string;
  disabled: boolean;
}) {
  const read = useStaffHistory(
    null,
    {
      path: testDriveHistoryPath(bookingId),
      parse: parseStaffTestDriveHistory,
      matches: (value) => value.bookingId === bookingId,
      append: appendTestDriveHistory,
      read: privateApiFetch,
    },
    "The test drive history could not be loaded. Confirmed entries remain shown.",
    { progressive: true },
  );
  const { history, busy } = read;
  return (
    <StaffHistorySection
      label="Test drive history"
      busy={busy}
      progress={<StaffHistoryProgress read={read} label="Test drive history" disabled={disabled} />}
    >
      <StaffTimeline>
        {history?.items.map((item) => {
          const summary = testDriveEventSummary(item);
          return (
            <StaffTimelineEvent
              key={item.id}
              title={testDriveEventTitles[item.kind]}
              at={item.recordedAt}
              context={<HistoryActor id={item.actorId} name={item.actor} />}
            >
              {summary && <p>{summary}</p>}
              {item.reason && <p>{item.reason}</p>}
              {item.notes && <p>{item.notes}</p>}
            </StaffTimelineEvent>
          );
        })}
      </StaffTimeline>
    </StaffHistorySection>
  );
}
