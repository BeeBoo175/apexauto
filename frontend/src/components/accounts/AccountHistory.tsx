"use client";

import { HistoryActor } from "@/components/staff/HistoryActor";
import { StaffHistoryProgress } from "@/components/staff/StaffHistoryProgress";
import { StaffHistorySection } from "@/components/staff/StaffHistorySection";
import { StaffTimeline, StaffTimelineEvent } from "@/components/staff/StaffTimeline";
import { useStaffHistory } from "@/hooks/use-staff-history";
import { privateApiFetch } from "@/lib/api-browser";
import {
  accountChangeText,
  accountEventTitles,
  accountHistoryPath,
  appendAccountHistory,
  parseAccountHistory,
} from "@/lib/accounts/account-history";

// Every retained administrative or self-service change to the account, newest first.
export function AccountHistory({ accountId }: { accountId: string }) {
  const read = useStaffHistory(
    null,
    {
      path: accountHistoryPath(accountId),
      parse: parseAccountHistory,
      matches: (value) => value.accountId === accountId,
      append: appendAccountHistory,
      read: privateApiFetch,
    },
    "The account history could not be loaded. Confirmed entries remain shown.",
    { progressive: true },
  );
  const { history, busy } = read;
  return (
    <StaffHistorySection
      label="Account history"
      busy={busy}
      empty={history?.totalCount === 0 && <p>No changes have been recorded yet.</p>}
      progress={<StaffHistoryProgress read={read} label="Account history" />}
    >
      <StaffTimeline>
        {history?.items.map((item) => (
          <StaffTimelineEvent
            key={item.id}
            title={accountEventTitles[item.kind]}
            at={item.recordedAt}
            context={<HistoryActor id={item.actorId} name={item.actor} />}
          >
            {item.reason && <p>{item.reason}</p>}
            {item.changes.map((change) => (
              <p key={change.field}>{accountChangeText(change)}</p>
            ))}
          </StaffTimelineEvent>
        ))}
      </StaffTimeline>
    </StaffHistorySection>
  );
}
