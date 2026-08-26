"use client";

import { useMemo, useRef, useState } from "react";
import { PhotoUpload } from "@/components/sell/PhotoUpload";
import { Button } from "@/components/ui/Button";
import { useEditGuard } from "@/hooks/use-edit-guard";
import { useSellerAppointment } from "@/hooks/use-seller-appointment";
import { useSellerPhotos } from "@/hooks/use-seller-photos";
import { useSellerWorkflow } from "@/hooks/use-seller-workflow";
import type { Branch } from "@/lib/branches";
import type { SellerSnapshot, Taxonomy } from "@/lib/intake/contracts";
import type { Page } from "@/lib/api";
import type { StaffSellNotice } from "@/lib/intake/staff-sell-notice";

import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { CustomerLookupRow } from "@/lib/staff/assisted-customer";
import {
  AssistedIntakeAppointmentFields,
  AssistedIntakeVehicleFields,
} from "./AssistedIntakeFields";
import { CustomerPicker } from "./CustomerPicker";
import { StaffWorkspaceFooter } from "./StaffWorkspaceFooter";
import sell from "@/components/sell/Sell.module.css";
import forms from "./StaffForms.module.css";
import styles from "./AssistedIntakeForm.module.css";
import workbench from "./StaffWorkspace.module.css";

// Staff fill in the trade-in booking for a customer on one form, in any order: the customer, the car, its
// valuation, four photos and the bay. Nothing is stepped; the single Book action unlocks once everything is in.
export function AssistedIntakeForm({
  taxonomy,
  branches,
  staffNotice,
  onBooked,
  onClose,
}: {
  taxonomy: Taxonomy;
  branches: Page<Branch>;
  staffNotice: StaffSellNotice;
  onBooked: () => void;
  onClose: () => void;
}) {
  // The seller workflow starts from a public-page snapshot; staff have no account of their own here, so it is
  // built once from the loaded options and the customer is named separately.
  const initial = useMemo<SellerSnapshot>(
    () => ({ taxonomy, branches, account: null, access: 200, staffNotice }),
    [taxonomy, branches, staffNotice],
  );
  const [customer, setCustomer] = useState<CustomerLookupRow | null>(null);
  const workflow = useSellerWorkflow(initial, customer?.id, false);
  const appointment = useSellerAppointment(branches);
  const photos = useSellerPhotos(workflow.receivePhoto, workflow.lockPhoto, customer?.id);
  const plateInput = useRef<HTMLInputElement>(null);
  const { quote, receipt, locked } = workflow;
  // The chosen customer is part of the draft, so leaving with one chosen asks to discard first.
  const hasWork =
    customer !== null ||
    Object.values(workflow.vehicle).some(Boolean) ||
    quote !== null ||
    photos.readyCount > 0;
  const vehicle = workflow.vehicle;
  const vehicleComplete = [
    vehicle.make,
    vehicle.model,
    vehicle.trim,
    vehicle.manufactureYear,
    vehicle.mileageKilometres,
    vehicle.registrationPlate,
  ].every(Boolean);
  const canBook =
    customer !== null &&
    vehicleComplete &&
    photos.readyCount === 4 &&
    Boolean(appointment.startsAt) &&
    !workflow.freshQuote &&
    !locked &&
    !photos.uploading;

  useEditGuard({
    dirty: hasWork && !receipt,
    pending: locked,
    uncertain: workflow.booking.uncertain,
    // Short, because the footer shows "<label>: ..." on one line while an action is running.
    label: "Booking",
    // Leaving with entries asks to discard them in the shared dialog, like the other drawers; the form is
    // unmounted with the drawer, which clears everything else.
    discard: () => setCustomer(null),
  });

  async function book() {
    if (!canBook) {
      return;
    }
    // The booking needs an accepted valuation, which staff never have to handle: it is requested here.
    const accepted = quote ?? (await workflow.requestQuote());
    if (!accepted) {
      return;
    }
    // Chosen photos upload only now, so nothing is stored for a booking that is never made.
    if (!(await photos.uploadPending())) {
      return;
    }
    await workflow.book(appointment.branchId, appointment.startsAt, accepted);
  }

  if (receipt) {
    return (
      <section className={styles.confirmed} aria-label="Appointment confirmed">
        <h3>Appointment confirmed</h3>
        <p>
          {receipt.registrationPlate} · {formatMalaysianDateTime(receipt.slot.startsAt)}
        </p>
        <p>
          Indicative valuation {formatCurrency(receipt.quote.minValuation, receipt.quote.currency)}{" "}
          – {formatCurrency(receipt.quote.maxValuation, receipt.quote.currency)}
        </p>
        <p className={styles.note}>
          Booked for {customer?.name}. The customer is emailed the confirmation, and the booking
          remains confirmed if mail is delayed. Reference: {receipt.id}
        </p>
        <div>
          <Button
            onClick={() => {
              onBooked();
              onClose();
            }}
          >
            Done
          </Button>
        </div>
      </section>
    );
  }

  return (
    <div className={workbench.workspace}>
      <div className={workbench.body}>
        <div className={forms.fields}>
          <section className={`${forms.section} ${styles.section}`} aria-label="Customer">
            <h3>Customer</h3>
            <CustomerPicker
              purpose="intake"
              selected={customer}
              disabled={locked || photos.readyCount > 0}
              onSelect={setCustomer}
              onClear={() => setCustomer(null)}
            />
          </section>

          <section className={`${forms.section} ${styles.section}`} aria-label="Vehicle">
            <h3>Vehicle</h3>
            <fieldset disabled={locked} className={styles.fieldset}>
              <AssistedIntakeVehicleFields
                taxonomy={taxonomy}
                value={workflow.vehicle}
                plateError={workflow.plateError}
                plateInputRef={plateInput}
                onPlateBlur={() => workflow.commitPlate()}
                onChange={workflow.editDetails}
              />
            </fieldset>
          </section>

          <section className={`${forms.section} ${styles.section}`} aria-label="Exterior photos">
            <h3>Exterior photos</h3>
            <p className={forms.hint}>
              {customer
                ? "Four angles, up to 10 MiB each. They upload when you book."
                : "Choose the customer before adding photos."}
            </p>
            <div className={`${sell.uploadGrid} ${sell.photoField}`}>
              {photos.items.map((item) => (
                <PhotoUpload
                  key={item.angle}
                  photo={item}
                  disabled={!customer || workflow.booking.busy || workflow.booking.uncertain}
                />
              ))}
            </div>
          </section>

          <section className={`${forms.section} ${styles.section}`} aria-label="Appointment">
            <h3>Appointment</h3>
            <fieldset disabled={locked} className={styles.fieldset}>
              <AssistedIntakeAppointmentFields selection={appointment} />
            </fieldset>
            {workflow.freshQuote && (
              <div className={forms.notice} role="status">
                <p>
                  The valuation changed to {formatCurrency(workflow.freshQuote.minValuation)} –{" "}
                  {formatCurrency(workflow.freshQuote.maxValuation)}. Accept it to book.
                </p>
                <Button
                  type="button"
                  size="compact"
                  variant="secondary"
                  onClick={workflow.acceptFreshQuote}
                >
                  Accept valuation
                </Button>
              </div>
            )}
          </section>
        </div>
      </div>
      <StaffWorkspaceFooter
        title="New intake"
        description="The customer is emailed the confirmation."
      >
        <Button size="compact" disabled={!canBook} onClick={() => void book()}>
          {workflow.booking.uncertain || workflow.valuation.uncertain
            ? "Retry same booking"
            : "Book appointment"}
        </Button>
      </StaffWorkspaceFooter>
    </div>
  );
}
