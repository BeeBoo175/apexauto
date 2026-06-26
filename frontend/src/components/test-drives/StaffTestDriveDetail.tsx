"use client";

import { StaffContact } from "@/components/staff/StaffContact";
import { Button } from "@/components/ui/Button";
import type { CurrentActor } from "@/lib/auth/actor";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { useStaffTestDriveDetail } from "@/hooks/use-staff-test-drive-detail";
import type { TestDriveStaffCommand } from "@/lib/test-drives/staff-commands";
import { StaffTestDriveStatus } from "./StaffTestDriveStatus";
import { StaffTestDriveActions } from "./StaffTestDriveActions";
import shared from "@/components/staff/StaffWorkspace.module.css";
import facts from "@/components/staff/StaffRecordContent.module.css";
import styles from "./StaffTestDrive.module.css";
import { PersonLabel } from "@/components/staff/PersonLabel";

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
    return (
      <div className={styles.empty}>
        {record.id ? (
          <Button
            variant="secondary"
            size="compact"
            disabled={Boolean(record.pendingId)}
            onClick={() => void record.load()}
          >
            Retry selected test drive
          </Button>
        ) : (
          <p>Select a test drive.</p>
        )}
      </div>
    );
  }
  const { booking, start, outcome, cancellation, ownership } = detail;
  return (
    <section
      className={shared.workspace}
      aria-label="Selected test drive"
      inert={Boolean(record.pendingId && record.pendingId !== record.id)}
    >
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
      <StaffTestDriveActions
        key={booking.id}
        actor={actor}
        detail={detail}
        refresh={refresh}
        reading={Boolean(record.pendingId)}
      >
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
              <dt>Handling staff</dt>
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
                {booking.handlerId && !booking.handlerEligible ? " (currently ineligible)" : ""}
              </dd>
            </div>
            {ownership && (
              <div>
                <dt>Last ownership change</dt>
                <dd>
                  {formatMalaysianDateTime(ownership.recordedAt)}
                  {ownership.reason ? ` · ${ownership.reason}` : ""}
                </dd>
              </div>
            )}
            {start && (
              <div>
                <dt>Actual start</dt>
                <dd>
                  {formatMalaysianDateTime(start.startedAt)} · {detail.startedByEmail}
                  <br />
                  Customer present · Driving licence checked
                </dd>
              </div>
            )}
            {outcome && (
              <div>
                <dt>Actual outcome</dt>
                <dd>
                  {outcome.kind === "Completed" ? "Completed" : "Not completed"} ·{" "}
                  {formatMalaysianDateTime(outcome.endedAt)} · {detail.completedByEmail}
                </dd>
              </div>
            )}
            {outcome?.reason && (
              <div>
                <dt>Outcome reason</dt>
                <dd>{outcome.reason}</dd>
              </div>
            )}
            {outcome?.notes && (
              <div>
                <dt>Outcome notes</dt>
                <dd>{outcome.notes}</dd>
              </div>
            )}
            {cancellation && (
              <div>
                <dt>Cancelled</dt>
                <dd>
                  {formatMalaysianDateTime(cancellation.recordedAt)} ·{" "}
                  {cancellation.reason ?? "Cancelled by customer"}
                </dd>
              </div>
            )}
          </dl>
        </section>
      </StaffTestDriveActions>
    </section>
  );
}
