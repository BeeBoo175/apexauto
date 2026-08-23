"use client";

import { StaffQueuePlaceholder } from "@/components/staff/StaffQueuePlaceholder";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { useState } from "react";
import { PersonLabel } from "@/components/staff/PersonLabel";
import { SuspensionBadge } from "@/components/staff/SuspensionBadge";
import { StaffContact } from "@/components/staff/StaffContact";
import { StaffRecordSummary, StaffSummaryFact } from "@/components/staff/StaffRecordSummary";
import { StaffStatusGroup } from "@/components/staff/StaffStatusGroup";
import { StaffDrawerLoadingShell } from "@/components/staff/StaffDrawerLoadingShell";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { useRecordSection } from "@/hooks/use-record-section";
import { useWorkspaceTabs } from "@/hooks/use-workspace-tabs";
import type { useStaffTestDriveDetail } from "@/hooks/use-staff-test-drive-detail";
import type { CurrentActor } from "@/lib/auth/actor";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { TestDriveStaffCommand } from "@/lib/test-drives/staff-commands";
import {
  isTerminalTestDriveStatus,
  type StaffTestDriveDetail,
} from "@/lib/test-drives/staff-query";
import { StaffTestDriveActions } from "./StaffTestDriveActions";
import { StaffTestDriveStatus } from "./StaffTestDriveStatus";
import { StaffTestDriveTimeline } from "./StaffTestDriveTimeline";
import shared from "@/components/staff/StaffWorkspace.module.css";
import facts from "@/components/staff/StaffRecordContent.module.css";
import styles from "./StaffTestDrive.module.css";
import { CustomerAvatar } from "@/components/staff/CustomerAvatar";

type TestDriveSection = "details" | "history";

const sections = [
  { value: "details", label: "Details", available: true },
  { value: "history", label: "History", available: true },
] as const;

export function StaffTestDriveDetail({
  actor,
  record,
  refresh,
  onGoToClaim,
}: {
  actor: CurrentActor;
  record: ReturnType<typeof useStaffTestDriveDetail>;
  refresh: (kind?: TestDriveStaffCommand) => Promise<boolean>;
  // Opens the To claim queue, which the empty content pane points to.
  onGoToClaim: () => void;
}) {
  const detail = record.snapshot;
  if (!detail) {
    // A drawer that is opening or reading its record holds the shape of the loaded one, never an empty prompt.
    if (record.pendingId) {
      return <StaffDrawerLoadingShell label="Loading test drive…" fact single />;
    }
    if (!record.id) {
      return (
        <StaffQueuePlaceholder
          label="Test drive content"
          title="Select a test drive"
          link={{ text: "To claim", onClick: onGoToClaim }}
        >
          Open one from your queue, or claim one in To claim.
        </StaffQueuePlaceholder>
      );
    }
    return (
      <div className={styles.empty}>
        <ReloadNotice subject="Test drive" />
      </div>
    );
  }
  return (
    <LoadedTestDrive
      key={detail.booking.id}
      actor={actor}
      detail={detail}
      reading={Boolean(record.pendingId)}
      inert={Boolean(record.pendingId && record.pendingId !== record.id)}
      refresh={refresh}
    />
  );
}

// The selected booking: current facts and actions on Details, every retained step on History. The tab state
// belongs to the booking, so choosing another one starts on Details again.
function LoadedTestDrive({
  actor,
  detail,
  reading,
  inert,
  refresh,
}: {
  actor: CurrentActor;
  detail: StaffTestDriveDetail;
  reading: boolean;
  inert: boolean;
  refresh: (kind?: TestDriveStaffCommand) => Promise<boolean>;
}) {
  const { booking } = detail;
  const [requested, setRequested] = useRecordSection<TestDriveSection>(
    "/dashboard/test-drives",
    booking.id,
    ["details", "history"],
  );
  // History is read only once its section has been opened.
  const [historyOpened, setHistoryOpened] = useState(requested === "history");
  const { tabs, section } = useWorkspaceTabs<TestDriveSection>(
    requested,
    sections,
    "details",
    setRequested,
  );
  const finished = isTerminalTestDriveStatus(booking.status);

  return (
    <section className={shared.workspace} aria-label="Selected test drive" inert={inert}>
      <StaffRecordSummary
        ariaLabel="Test drive vehicle and status"
        label={`${booking.manufactureYear} ${booking.make} ${booking.model} ${booking.trim}`}
        value={booking.registrationPlate}
        status={
          <StaffStatusGroup label="Test drive status">
            <StaffTestDriveStatus status={booking.status} />
          </StaffStatusGroup>
        }
        context={<span>{booking.branchName}</span>}
        facts={
          <StaffSummaryFact label={finished ? "Handled by" : "Handling staff"}>
            {booking.handlerId && booking.handlerEmail ? (
              <PersonLabel
                id={booking.handlerId}
                name={booking.handlerName ?? booking.handlerEmail}
                identity={booking.handlerEmail}
                pictureId={booking.handlerPictureId}
              />
            ) : (
              <span>Unassigned</span>
            )}
            {!finished && booking.handlerSuspended && <SuspensionBadge person="Handler" />}
            {!finished &&
              booking.handlerId &&
              !booking.handlerEligible &&
              !booking.handlerSuspended && <span> (currently ineligible)</span>}
          </StaffSummaryFact>
        }
      />
      <StaffWorkspaceTabs
        label="Test drive sections"
        tabs={tabs}
        value={section}
        onChange={(next) => {
          setRequested(next);
          setHistoryOpened((opened) => opened || next === "history");
        }}
      />
      <div className={styles.panel} hidden={section !== "details"}>
        <StaffTestDriveActions actor={actor} detail={detail} refresh={refresh} reading={reading}>
          <section className={facts.group}>
            <h3 className={shared.sectionTitle}>Customer</h3>
            <p>
              <CustomerAvatar id={booking.customerId} name={booking.customerName} />
            </p>
            <dl className={styles.contacts}>
              <StaffContact method="phone" value={detail.customerMobile} />
              <StaffContact method="email" value={detail.customerEmail} />
            </dl>
          </section>
          <section className={facts.group}>
            <h3 className={shared.sectionTitle}>Test drive details</h3>
            <dl className={facts.facts}>
              <div>
                <dt>Reference</dt>
                <dd>{booking.id}</dd>
              </div>
              <div>
                <dt>Booked start</dt>
                <dd>{formatMalaysianDateTime(booking.startsAt)}</dd>
              </div>
              <div>
                <dt>Booked end</dt>
                <dd>{formatMalaysianDateTime(booking.endsAt)}</dd>
              </div>
              <div>
                <dt>Created</dt>
                <dd>{formatMalaysianDateTime(detail.createdAt)}</dd>
              </div>
            </dl>
          </section>
        </StaffTestDriveActions>
      </div>
      {(historyOpened || section === "history") && (
        <div className={styles.panel} hidden={section !== "history"}>
          <div className={shared.body}>
            <StaffTestDriveTimeline
              key={`${booking.status}:${booking.ownershipVersion}`}
              bookingId={booking.id}
              disabled={reading}
            />
          </div>
        </div>
      )}
    </section>
  );
}
