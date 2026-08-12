"use client";

import Link from "next/link";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Select } from "@/components/ui/Input";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { useVehicleTestDrive } from "@/hooks/use-vehicle-test-drive";
import { authHref, type CustomerAccount } from "@/lib/auth/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import type { CustomerVehicleAccess } from "@/lib/vehicles/customer-access";
import styles from "./VehicleTestDrive.module.css";

export function VehicleTestDrive({
  detail,
  access,
  actingFor,
}: {
  detail: PublicVehicleDetail;
  access: CustomerVehicleAccess;
  // The customer's id when sales staff book on their behalf.
  actingFor?: string;
}) {
  const actor = useIntakeActor(access.actor ?? undefined);
  if (!actor.active || access.status === 403) {
    return null;
  }
  if (access.status === 401) {
    return (
      <Link
        href={authHref("/login", `/vehicles/${detail.listing.id}`)}
        prefetch={false}
        className={buttonClassName("secondary", "md")}
      >
        Sign in to book a test drive
      </Link>
    );
  }
  if (!access.account || !access.actor || access.status !== 200) {
    return null;
  }
  if (!access.account.emailVerified) {
    return (
      <Link href="/account" prefetch={false} className={buttonClassName("secondary", "md")}>
        Verify email to book a test drive
      </Link>
    );
  }
  return (
    <CustomerTestDrive
      key={`${access.actor.userId}:${detail.publicationId}`}
      detail={detail}
      account={access.account}
      actingFor={actingFor}
      ready={actor.ready}
    />
  );
}

function CustomerTestDrive({
  detail,
  account,
  actingFor,
  ready,
}: {
  detail: PublicVehicleDetail;
  account: CustomerAccount;
  actingFor?: string;
  ready: boolean;
}) {
  const drive = useVehicleTestDrive(detail, account, actingFor);
  const receipt = drive.command.receipt;
  const confirmedDate = drive.offer?.date === drive.date;
  const vehicle = `${detail.listing.manufactureYear} ${detail.listing.make} ${detail.listing.model} ${detail.listing.trim}`;
  let title = "Book a test drive";
  if (receipt) {
    title = "Test drive confirmed";
  } else if (drive.confirmation) {
    title = "Confirm this test drive?";
  }
  return (
    <>
      <Button variant="secondary" disabled={!ready || drive.locked} onClick={drive.show}>
        {receipt ? "View test-drive booking" : "Book a test drive"}
      </Button>
      <Dialog
        open={drive.open}
        title={title}
        description={`${vehicle} · ${detail.listing.branchName}`}
        closeDisabled={drive.locked}
        onClose={drive.close}
      >
        <div className={styles.content}>
          {receipt && (
            <>
              <p>Your 30-minute test drive is booked at {detail.listing.branchName}.</p>
              <dl className={styles.facts}>
                <div>
                  <dt>Time (Malaysia)</dt>
                  <dd>{formatMalaysianDateTime(receipt.startsAt)}</dd>
                </div>
                <div>
                  <dt>Reference</dt>
                  <dd className={styles.reference}>{receipt.id}</dd>
                </div>
              </dl>
              <p>
                Bring your valid driving licence. You can view or cancel your booking in your
                account.
              </p>
              <Link href="/account/test-drives" prefetch={false}>
                Your test drives
              </Link>
              <Button variant="secondary" onClick={drive.close}>
                Done
              </Button>
            </>
          )}
          {!receipt && drive.confirmation && (
            <>
              <p>Review your appointment before confirming. No deposit is required.</p>
              <dl className={styles.facts}>
                <div>
                  <dt>Branch</dt>
                  <dd>{detail.listing.branchName}</dd>
                </div>
                <div>
                  <dt>Time (Malaysia)</dt>
                  <dd>{formatMalaysianDateTime(drive.confirmation.startsAt)}</dd>
                </div>
              </dl>
              <p>This books a 30-minute test drive. It does not reserve the car for purchase.</p>
              <div className={styles.actions}>
                <Button
                  variant="secondary"
                  disabled={drive.locked}
                  onClick={() => drive.setConfirmation(null)}
                >
                  Choose another time
                </Button>
                <Button disabled={drive.command.busy} onClick={() => void drive.book()}>
                  {drive.command.uncertain ? "Reconcile booking" : "Confirm test drive"}
                </Button>
              </div>
            </>
          )}
          {!receipt && !drive.confirmation && (
            <>
              <p>
                Choose a 30-minute appointment in Malaysia time. No deposit is required. Bring your
                valid driving licence.
              </p>
              <Input
                label="Date (Malaysia)"
                type="date"
                value={drive.date}
                min={drive.offer?.dateWindow.firstDate}
                max={drive.offer?.dateWindow.lastDate}
                disabled={drive.locked}
                onChange={(event) => void drive.load(event.target.value)}
              />
              <Select
                label="Available time (Malaysia)"
                value={drive.startsAt}
                disabled={drive.busy || drive.locked || !confirmedDate}
                onChange={(event) => drive.setStartsAt(event.target.value)}
              >
                <option value="">Choose a time</option>
                {drive.offer?.slots.map((slot) => (
                  <option key={slot.startsAt} value={slot.startsAt}>
                    {formatMalaysianDateTime(slot.startsAt)}
                  </option>
                ))}
              </Select>
              {drive.busy && <p role="status">Loading available times…</p>}
              {confirmedDate && drive.offer?.slots.length === 0 && (
                <p>No available times on this date. Choose another date.</p>
              )}
              <div className={styles.actions}>
                <Button
                  variant="secondary"
                  disabled={drive.busy || drive.locked}
                  onClick={() => void drive.load()}
                >
                  Refresh times
                </Button>
                <Button
                  disabled={drive.busy || drive.locked || !confirmedDate || !drive.startsAt}
                  onClick={drive.confirm}
                >
                  Continue
                </Button>
              </div>
              <p className={styles.help}>
                Times are confirmed when you book. You may have one outstanding test drive.
              </p>
            </>
          )}
        </div>
      </Dialog>
    </>
  );
}
