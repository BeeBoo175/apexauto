"use client";

import { useState } from "react";
import { PersonLabel } from "@/components/staff/PersonLabel";
import { StaffContact } from "@/components/staff/StaffContact";
import { StaffRecordLoadingShell } from "@/components/staff/StaffRecordLoadingShell";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { Button } from "@/components/ui/Button";
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

const loadingDetails = [
  "Reference",
  "Booked start",
  "Booked end",
  "Created",
  "Handling staff",
] as const;

type TestDriveSection = "details" | "history";

const sections = [
  { value: "details", label: "Details", available: true },
  { value: "history", label: "History", available: true },
] as const;

export function StaffTestDriveDetail({
  actor,
  record,
  refresh,
}: {
  actor: CurrentActor;
  record: ReturnType<typeof useStaffTestDriveDetail>;
  refresh: (kind?: TestDriveStaffCommand) => Promise<boolean>;
}) {
  const detail = record.snapshot;
  if (!detail) {
    // A drawer that is opening or reading its record holds the shape of the loaded one, never an empty prompt.
    if (record.pendingId) {
      return <StaffRecordLoadingShell label="Loading test drive…" details={loadingDetails} />;
    }
    return (
      <div className={styles.empty}>
        {record.id ? (
          <Button variant="secondary" size="compact" onClick={() => void record.load()}>
            Retry selected test drive
          </Button>
        ) : (
          <p>Select a test drive.</p>
        )}
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
  const [requested, setRequested] = useState<TestDriveSection>("details");
  // History is read only once its section has been opened.
  const [historyOpened, setHistoryOpened] = useState(false);
  const { tabs, section } = useWorkspaceTabs<TestDriveSection>(
    requested,
    sections,
    "details",
    setRequested,
  );
  const finished = isTerminalTestDriveStatus(booking.status);

  return (
    <section className={shared.workspace} aria-label="Selected test drive" inert={inert}>
      <div className={shared.summary}>
        <div className={shared.summaryMain}>
          <div>
            <strong className={shared.summaryValue}>{booking.registrationPlate}</strong>
            <p className={shared.help}>
              {booking.manufactureYear} {booking.make} {booking.model} {booking.trim}
            </p>
          </div>
          <StaffTestDriveStatus status={booking.status} />
        </div>
        <p className={shared.help}>
          {formatMalaysianDateTime(booking.startsAt)} · {booking.branchName}
        </p>
      </div>
      <StaffWorkspaceTabs
        label="Test drive sections"
        tabs={tabs}
        value={section}
        onChange={(next) => {
          setRequested(next);
          setHistoryOpened((opened) => opened || next === "history");
        }}
      />
      <div hidden={section !== "details"}>
        <StaffTestDriveActions actor={actor} detail={detail} refresh={refresh} reading={reading}>
          <section className={facts.group}>
            <h3 className={shared.sectionTitle}>Customer</h3>
            <p>{booking.customerName}</p>
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
              <div>
                <dt>{finished ? "Handled by" : "Handling staff"}</dt>
                <dd>
                  {booking.handlerId && booking.handlerEmail ? (
                    <PersonLabel
                      id={booking.handlerId}
                      name={booking.handlerName ?? booking.handlerEmail}
                      identity={booking.handlerEmail}
                      pictureId={booking.handlerPictureId}
                    />
                  ) : (
                    "Unassigned"
                  )}
                  {!finished && booking.handlerId && !booking.handlerEligible
                    ? " (currently ineligible)"
                    : ""}
                </dd>
              </div>
            </dl>
          </section>
        </StaffTestDriveActions>
      </div>
      {historyOpened && (
        <div hidden={section !== "history"}>
          <StaffTestDriveTimeline
            key={`${booking.status}:${booking.ownershipVersion}`}
            bookingId={booking.id}
            disabled={reading}
          />
        </div>
      )}
    </section>
  );
}
