"use client";

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
import timeline from "@/components/staff/StaffHistory.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

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
    <section className={timeline.history} aria-label="Test drive history" aria-busy={busy}>
      <div className={workbench.sectionHeader}>
        <h2 className={workbench.sectionTitle}>Record history</h2>
      </div>
      <StaffTimeline>
        {history?.items.map((item) => {
          const summary = testDriveEventSummary(item);
          return (
            <StaffTimelineEvent
              key={item.id}
              title={testDriveEventTitles[item.kind]}
              at={item.recordedAt}
              context={item.actor}
            >
              {summary && <p>{summary}</p>}
              {item.reason && <p>{item.reason}</p>}
              {item.notes && <p>{item.notes}</p>}
            </StaffTimelineEvent>
          );
        })}
      </StaffTimeline>
      <StaffHistoryProgress read={read} label="Test drive history" disabled={disabled} />
    </section>
  );
}
