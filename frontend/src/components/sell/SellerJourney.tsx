"use client";
import { useEffect, useRef, useState } from "react";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import Link from "next/link";
import { Button, buttonClassName } from "@/components/ui/Button";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Icon } from "@/components/ui/Icon";
import { useEditGuard } from "@/hooks/use-edit-guard";
import { useSellerLeaveGuard } from "@/hooks/use-seller-leave-guard";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { useSellerWorkflow } from "@/hooks/use-seller-workflow";
import { useSellerAppointment } from "@/hooks/use-seller-appointment";
import { useSellerPhotos } from "@/hooks/use-seller-photos";
import { useInitialToast } from "@/hooks/use-initial-toast";
import type { SellerSnapshot } from "@/lib/intake/contracts";
import type { CustomerLookupRow } from "@/lib/staff/assisted-customer";
import { replaceUrlParams } from "@/lib/browser-url";
import { SELLER_STEP_KEY } from "@/lib/intake/seller-steps";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { isValidMalaysianPlate } from "@/lib/intake/registration-plate";
import { VehicleFields } from "./VehicleFields";
import { VehicleRecord } from "./VehicleRecord";
import { PhotoUpload } from "./PhotoUpload";
import { AppointmentFields } from "./AppointmentFields";
import { AssistedSellerGate } from "./AssistedSellerGate";
import { SellerAuthGate } from "./SellerAuthGate";
import styles from "./Sell.module.css";

const steps = ["Vehicle details", "Valuation", "Exterior photos", "Appointment"];
const stepNotes = [
  "Specs and plate",
  "Indicative RM range",
  "Four exterior angles",
  "90-min branch bay",
] as const;

export function SellerJourney({
  initial,
  embedded = false,
  onDone,
}: {
  initial: SellerSnapshot;
  // Inside a staff drawer the journey keeps its own stage, because the address belongs to the workspace.
  embedded?: boolean;
  onDone?: () => void;
}) {
  const [generation, setGeneration] = useState(0);
  const unavailable = !initial.taxonomy || !initial.branches || initial.access >= 500;
  useInitialToast(
    unavailable
      ? "Vehicle intake is temporarily unavailable. Your session has been kept. Reload the page to try again."
      : null,
  );
  const actor = useIntakeActor(
    initial.account
      ? { userId: initial.account.userId, role: "Customer", branchId: null }
      : undefined,
  );
  if (!actor.active) {
    return null;
  }
  if (unavailable) {
    return (
      <section className={styles.unavailable}>
        <h1>Sell your car</h1>
        <ReloadNotice subject="Intake" />
      </section>
    );
  }
  // Starting over remounts the workflow, which clears every entered value, photo and booking attempt at once.
  return (
    <SellerWorkflow
      key={generation}
      initial={initial}
      ready={actor.ready}
      embedded={embedded}
      onDone={onDone}
      onStartOver={() => {
        if (!embedded) {
          replaceUrlParams({ [SELLER_STEP_KEY]: "" });
        }
        setGeneration((current) => current + 1);
      }}
    />
  );
}

function SellerWorkflow({
  initial,
  ready,
  embedded,
  onDone,
  onStartOver,
}: {
  initial: SellerSnapshot;
  ready: boolean;
  embedded: boolean;
  onDone?: () => void;
  onStartOver: () => void;
}) {
  // Sales staff choose the customer they book for; everything below then belongs to that customer.
  const [customer, setCustomer] = useState<CustomerLookupRow | null>(null);
  const workflow = useSellerWorkflow(initial, customer?.id, !embedded);
  const appointment = useSellerAppointment(initial.branches!);
  const sellerPhotos = useSellerPhotos(workflow.receivePhoto, workflow.lockPhoto, customer?.id);
  const heading = useRef<HTMLHeadingElement>(null);
  const plateInput = useRef<HTMLInputElement>(null);
  const { step, quote, locked, receipt } = workflow;
  const photoCount = Object.values(workflow.photos).filter(Boolean).length;
  const [confirmingStartOver, setConfirmingStartOver] = useState(false);
  // Anything entered is lost by leaving or starting over, so both ask first.
  const hasWork =
    Object.values(workflow.vehicle).some(Boolean) || quote !== null || sellerPhotos.readyCount > 0;

  // In a drawer the drawer's own guard asks before closing; on the page the browser asks before leaving.
  useEditGuard({
    dirty: embedded && hasWork && !receipt,
    pending: embedded && locked,
    label: "Trade-in appointment",
  });

  const leave = useSellerLeaveGuard(hasWork && !receipt && !embedded);

  useEffect(() => {
    if (ready) {
      heading.current?.focus();
    }
  }, [step, ready, receipt?.id]);

  async function handleContinueToAppointment() {
    if (!workflow.verified || sellerPhotos.readyCount !== 4 || locked) {
      return;
    }
    const allConfirmed = await sellerPhotos.uploadPending();
    if (allConfirmed) {
      workflow.setStep(3);
    }
  }

  if (receipt) {
    return (
      <section className={styles.unavailable}>
        <h1 ref={heading} tabIndex={-1}>
          {customer ? "Appointment confirmed" : "Your appointment is confirmed"}
        </h1>
        <p className={styles.plate}>{receipt.registrationPlate}</p>
        <p>{formatMalaysianDateTime(receipt.slot.startsAt)}</p>
        <p className={styles.help}>Reference: {receipt.id}</p>
        <p>
          {customer
            ? `Booked for ${customer.name}. The confirmation email is delivered to the customer separately, and the booking remains confirmed if mail is delayed.`
            : "A confirmation email is delivered separately. Your booking remains confirmed if mail is delayed."}
        </p>
        {embedded ? (
          <Button onClick={onDone}>Done</Button>
        ) : (
          <Link
            href={customer ? "/dashboard/intake" : "/account/appointments"}
            prefetch={false}
            className={buttonClassName("primary", "md")}
          >
            {customer ? "Open intake workspace" : "View your appointments"}
          </Link>
        )}
      </section>
    );
  }

  return (
    <section
      className={styles.page}
      data-embedded={embedded || undefined}
      aria-label="Sell your car"
    >
      <div className={styles.progressBand}>
        <div className={styles.progressWrap}>
          <ol className={styles.progress}>
            {steps.map((label, index) => {
              const done = index < step;
              const current = index === step;
              return (
                <li
                  key={label}
                  aria-current={current ? "step" : undefined}
                  data-done={done || undefined}
                >
                  <span className={styles.stepMarker} aria-hidden="true">
                    {done ? <Icon name="check" /> : `0${index + 1}`}
                  </span>
                  <div className={styles.stepText}>
                    <span className={styles.stepTitle}>{label}</span>
                    <span className={styles.stepNote}>{stepNotes[index]}</span>
                  </div>
                </li>
              );
            })}
          </ol>
        </div>
      </div>

      <div className={styles.workspace}>
        <div className={styles.formPanel}>
          <div className={styles.panelHeading}>
            <h1 ref={heading} tabIndex={-1}>
              {steps[step]}
            </h1>
            {hasWork && (
              <Button
                variant="ghost"
                size="compact"
                disabled={locked}
                onClick={() => setConfirmingStartOver(true)}
              >
                Start over
              </Button>
            )}
          </div>
          <ConfirmationDialog
            open={leave.leaving}
            title="Leave this page?"
            confirmLabel="Discard and leave"
            cancelLabel="Keep my details"
            danger
            onClose={leave.stayHere}
            onConfirm={leave.confirmLeave}
          >
            <p>
              The vehicle details, valuation and photos entered so far are lost if you leave.
              Nothing has been booked yet.
            </p>
          </ConfirmationDialog>
          <ConfirmationDialog
            open={confirmingStartOver}
            title="Start over?"
            confirmLabel="Discard and start over"
            cancelLabel="Keep my details"
            danger
            onClose={() => setConfirmingStartOver(false)}
            onConfirm={onStartOver}
          >
            <p>
              This clears the vehicle details, valuation and photos entered so far. Nothing has been
              booked yet.
            </p>
          </ConfirmationDialog>

          {step === 0 && (
            <form
              method="post"
              className={`${styles.stack} ${styles.formFill}`}
              onSubmit={(event) => {
                event.preventDefault();
                void workflow.requestQuote().then((submitted) => {
                  if (!submitted && !isValidMalaysianPlate(workflow.vehicle.registrationPlate)) {
                    plateInput.current?.focus();
                  }
                });
              }}
            >
              <p className={styles.intro}>
                Enter your car specifications for an instant guide valuation, then schedule an
                inspection bay.
              </p>
              <fieldset disabled={!ready || locked} className={styles.stack}>
                <VehicleFields
                  taxonomy={initial.taxonomy!}
                  value={workflow.vehicle}
                  plateError={workflow.plateError}
                  onPlateBlur={() => workflow.commitPlate()}
                  plateInputRef={plateInput}
                  onChange={workflow.editDetails}
                />
              </fieldset>
              <div className={styles.formFooter}>
                <p className={styles.help}>Free valuation range with zero booking obligation.</p>
                <div className={styles.actions}>
                  <Button type="submit" disabled={!ready || workflow.valuation.busy}>
                    {workflow.valuation.uncertain
                      ? "Retry same valuation"
                      : "Get indicative valuation"}
                  </Button>
                </div>
              </div>
            </form>
          )}

          {step === 1 && (
            <div className={`${styles.stack} ${styles.formFill}`}>
              <p className={styles.intro}>
                Review your online valuation range before adding exterior photos.
              </p>
              <div className={styles.valuationCard}>
                <div className={styles.valuationHeader}>
                  <span>Indicative valuation</span>
                  <span className={styles.valuationBadge}>Non-binding</span>
                </div>
                <p className={styles.largeAmount}>
                  {quote &&
                    `${formatCurrency(quote.minValuation, quote.currency)} – ${formatCurrency(quote.maxValuation, quote.currency)}`}
                </p>
                <p className={styles.valuationNote}>
                  Final offer is confirmed on site after a 90-minute physical bay inspection.
                </p>
              </div>
              <ul className={styles.valuationChecks} aria-label="What is included">
                <li>
                  <Icon name="check" />
                  <span>
                    Calculated from verified make, model, trim, year, and declared mileage
                  </span>
                </li>
                <li>
                  <Icon name="shield-check" />
                  <span>No obligation to accept at inspection across all three showrooms</span>
                </li>
                <li>
                  <Icon name="car" />
                  <span>Add four exterior photos next to unlock appointment bay booking</span>
                </li>
              </ul>
              <div className={styles.formFooter}>
                <div className={styles.actions}>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={locked}
                    onClick={() => workflow.setStep(0)}
                  >
                    Edit vehicle details
                  </Button>
                  <Button type="button" onClick={() => workflow.setStep(2)}>
                    Continue to photos
                  </Button>
                </div>
              </div>
            </div>
          )}

          <div hidden={step !== 2} className={styles.stepPane}>
            <div className={`${styles.stack} ${styles.formFill}`}>
              <p className={styles.intro}>
                Add 4 clear exterior angles (JPEG, PNG or WebP, max 10 MiB each). Photos upload
                automatically when you choose an appointment.
              </p>
              {initial.staffNotice?.assisted && (
                <AssistedSellerGate
                  notice={initial.staffNotice}
                  showLink={!embedded}
                  customer={customer}
                  locked={locked || sellerPhotos.readyCount > 0}
                  onSelect={setCustomer}
                  onClear={() => setCustomer(null)}
                />
              )}
              {!workflow.verified && !initial.staffNotice?.assisted && (
                <SellerAuthGate initial={initial} onSignIn={workflow.saveHandoff} />
              )}
              {workflow.verified && (
                <div className={styles.uploadGrid}>
                  {sellerPhotos.items.map((item) => (
                    <PhotoUpload
                      key={item.angle}
                      photo={item}
                      disabled={!ready || workflow.booking.busy || workflow.booking.uncertain}
                    />
                  ))}
                </div>
              )}
              <div className={styles.formFooter}>
                <div className={styles.actions}>
                  <Button variant="secondary" disabled={locked} onClick={() => workflow.setStep(1)}>
                    Back to valuation
                  </Button>
                  <Button
                    type="button"
                    disabled={
                      !workflow.verified ||
                      sellerPhotos.readyCount !== 4 ||
                      locked ||
                      sellerPhotos.uploading
                    }
                    onClick={() => void handleContinueToAppointment()}
                  >
                    {sellerPhotos.uploading ? "Uploading photos…" : "Choose appointment"}
                  </Button>
                </div>
              </div>
            </div>
          </div>

          {step === 3 && (
            <form
              method="post"
              className={`${styles.stack} ${styles.formFill}`}
              onSubmit={(event) => {
                event.preventDefault();
                void workflow.book(appointment.branchId, appointment.startsAt);
              }}
            >
              <p className={styles.intro}>
                Choose a showroom and 90-minute inspection bay. Availability is confirmed on
                booking.
              </p>
              <AppointmentFields selection={appointment} disabled={!ready || locked} />
              {workflow.freshQuote && (
                <div className={styles.stack}>
                  <h3>Updated valuation</h3>
                  <p>
                    {formatCurrency(workflow.freshQuote.minValuation, workflow.freshQuote.currency)}{" "}
                    –{" "}
                    {formatCurrency(workflow.freshQuote.maxValuation, workflow.freshQuote.currency)}
                  </p>
                  <Button type="button" variant="secondary" onClick={workflow.acceptFreshQuote}>
                    Accept updated valuation
                  </Button>
                </div>
              )}
              <div className={styles.formFooter}>
                <div className={styles.actions}>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={locked}
                    onClick={() => workflow.setStep(2)}
                  >
                    Back to photos
                  </Button>
                  <Button
                    type="submit"
                    disabled={
                      !ready ||
                      workflow.booking.busy ||
                      !appointment.startsAt ||
                      Boolean(workflow.freshQuote)
                    }
                  >
                    {workflow.booking.uncertain ? "Retry same booking" : "Confirm appointment"}
                  </Button>
                </div>
              </div>
            </form>
          )}

          <noscript>
            <p>Enable JavaScript to use the intake form.</p>
          </noscript>
        </div>
        <VehicleRecord vehicle={workflow.vehicle} quote={quote} photos={photoCount} />
      </div>
    </section>
  );
}
