"use client";
import { useEffect, useRef } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { useSellerWorkflow } from "@/hooks/use-seller-workflow";
import { useSellerAppointment } from "@/hooks/use-seller-appointment";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { angles, type SellerSnapshot } from "@/lib/intake/contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { VehicleFields } from "./VehicleFields";
import { VehicleRecord } from "./VehicleRecord";
import { PhotoUpload } from "./PhotoUpload";
import { AppointmentFields } from "./AppointmentFields";
import styles from "./Sell.module.css";

const steps = ["Vehicle details", "Valuation", "Exterior photos", "Appointment"];
const stepNotes = [
  "Specs and plate",
  "Indicative MYR range",
  "Four exterior angles",
  "90-min branch bay",
] as const;

export function SellerJourney({ initial }: { initial: SellerSnapshot }) {
  const unavailable = !initial.taxonomy || !initial.branches || initial.access >= 500;
  useInitialToast(
    unavailable
      ? "Vehicle intake is temporarily unavailable. Your session has been kept. Retry when the service is available."
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
        <Button onClick={() => window.location.reload()}>Retry intake</Button>
      </section>
    );
  }
  return <SellerWorkflow initial={initial} ready={actor.ready} />;
}

function SellerWorkflow({ initial, ready }: { initial: SellerSnapshot; ready: boolean }) {
  const workflow = useSellerWorkflow(initial);
  const appointment = useSellerAppointment(initial.branches!);
  const heading = useRef<HTMLHeadingElement>(null);
  const { step, quote, locked, receipt } = workflow;
  const photoCount = Object.values(workflow.photos).filter(Boolean).length;
  useEffect(() => {
    if (ready) {
      heading.current?.focus();
    }
  }, [step, ready, receipt?.id]);

  if (receipt) {
    return (
      <section className={styles.unavailable}>
        <h1 ref={heading} tabIndex={-1}>
          Your appointment is confirmed
        </h1>
        <p className={styles.plate}>{receipt.registrationPlate}</p>
        <p>{formatMalaysianDateTime(receipt.slot.startsAt)}</p>
        <p className={styles.help}>Reference: {receipt.id}</p>
        <p>
          A confirmation email is delivered separately. Your booking remains confirmed if mail is
          delayed.
        </p>
        <a href="/account">View your appointments</a>
      </section>
    );
  }

  return (
    <section className={styles.page} aria-label="Sell your car">
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
          <h1 ref={heading} tabIndex={-1}>
            {steps[step]}
          </h1>
          {step === 0 && (
            <form
              method="post"
              className={`${styles.stack} ${styles.formFill}`}
              onSubmit={(event) => {
                event.preventDefault();
                void workflow.requestQuote();
              }}
            >
              <p className={styles.intro}>
                Tell us about your car for an instant guide valuation, then arrange a showroom
                inspection.
              </p>
              <fieldset disabled={!ready || locked} className={styles.stack}>
                <VehicleFields
                  taxonomy={initial.taxonomy!}
                  value={workflow.vehicle}
                  onChange={workflow.editDetails}
                />
              </fieldset>
              <div className={styles.actions}>
                <Button type="submit" disabled={!ready || workflow.valuation.busy}>
                  {workflow.valuation.uncertain
                    ? "Retry same valuation"
                    : "Get indicative valuation"}
                </Button>
              </div>
            </form>
          )}
          {step === 1 && (
            <div className={styles.stack}>
              <p className={styles.intro}>Your vehicle’s indicative range</p>
              <p className={styles.largeAmount}>
                {quote &&
                  `${formatCurrency(quote.minValuation, quote.currency)} – ${formatCurrency(quote.maxValuation, quote.currency)}`}
              </p>
              <p>
                This is an estimate, not an offer. We will inspect the car before confirming an
                offer.
              </p>
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
          )}
          <div hidden={step !== 2}>
            <p className={styles.intro}>
              Upload one clear image of each exterior angle. Each upload is confirmed separately.
            </p>
            {!workflow.verified && (
              <div className={styles.stack}>
                <p>
                  {initial.account
                    ? "Verify your account email before uploading photos or booking."
                    : "Sign in with a verified customer account to continue. Your vehicle details can be carried to sign-in."}
                </p>
                <a
                  href={initial.account ? "/account" : "/login?returnTo=%2Fsell"}
                  onClick={workflow.saveHandoff}
                >
                  {initial.account ? "Manage email verification" : "Sign in to continue"}
                </a>
              </div>
            )}
            {workflow.verified && (
              <div className={styles.uploadGrid}>
                {angles.map((angle) => (
                  <PhotoUpload
                    key={angle}
                    angle={angle}
                    disabled={!ready || workflow.booking.busy || workflow.booking.uncertain}
                    onReceipt={workflow.receivePhoto}
                    onLock={workflow.lockPhoto}
                  />
                ))}
              </div>
            )}
            <div className={styles.actions}>
              <Button variant="secondary" disabled={locked} onClick={() => workflow.setStep(1)}>
                Back to valuation
              </Button>
              <Button
                disabled={!workflow.verified || photoCount !== 4 || locked}
                onClick={() => workflow.setStep(3)}
              >
                Choose appointment
              </Button>
            </div>
          </div>
          {step === 3 && (
            <form
              method="post"
              className={styles.stack}
              onSubmit={(event) => {
                event.preventDefault();
                void workflow.book(appointment.branchId, appointment.startsAt);
              }}
            >
              <p className={styles.intro}>
                Choose a branch and inspection time. Slot availability is checked again when you
                book.
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
