"use client";

import { useEffect, useEffectEvent, type RefObject } from "react";
import { VehicleValuationFields } from "@/components/sell/VehicleValuationFields";
import { Input, Select } from "@/components/ui/Input";
import type { useSellerAppointment } from "@/hooks/use-seller-appointment";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { Taxonomy } from "@/lib/intake/contracts";
import {
  REGISTRATION_PLATE_MAX_LENGTH,
  sanitizeRegistrationPlateInput,
} from "@/lib/intake/registration-plate";
import type { VehicleDraft } from "@/lib/intake/vehicle-draft";
import forms from "./StaffForms.module.css";
import styles from "./AssistedIntakeForm.module.css";

// The car's fields in the compact vocabulary of the other staff drawers: the make to year choices, the declared
// mileage and the plate.
export function AssistedIntakeVehicleFields({
  taxonomy,
  value,
  plateError,
  plateInputRef,
  onPlateBlur,
  onChange,
}: {
  taxonomy: Taxonomy;
  value: VehicleDraft;
  plateError: string | null;
  plateInputRef: RefObject<HTMLInputElement | null>;
  onPlateBlur: () => void;
  onChange: (value: VehicleDraft) => void;
}) {
  return (
    <>
      <div className={forms.pair}>
        <VehicleValuationFields
          density="compact"
          taxonomy={taxonomy}
          value={value}
          onChange={(valuation) => onChange({ ...value, ...valuation })}
        />
        <div>
          <Input
            ref={plateInputRef}
            density="compact"
            label="Registration plate"
            maxLength={REGISTRATION_PLATE_MAX_LENGTH}
            placeholder="e.g. VCA 6242"
            autoCapitalize="characters"
            autoCorrect="off"
            spellCheck={false}
            required
            value={value.registrationPlate}
            aria-invalid={Boolean(plateError) || undefined}
            onChange={(event) =>
              onChange({
                ...value,
                registrationPlate: sanitizeRegistrationPlateInput(event.target.value),
              })
            }
            onBlur={onPlateBlur}
          />
          {plateError && (
            <p className={forms.error} role="alert">
              {plateError}
            </p>
          )}
        </div>
      </div>
    </>
  );
}

// Branch, date and one of the offered inspection times.
export function AssistedIntakeAppointmentFields({
  selection,
}: {
  selection: ReturnType<typeof useSellerAppointment>;
}) {
  const { branches, offer, dateWindow, dateError } = selection;
  const ready = Boolean(selection.branchId && selection.date) && !dateError;
  const loadTimes = useEffectEvent(() => void selection.loadSlots());
  // The times for a chosen branch and date load by themselves; there is no button to press.
  useEffect(() => {
    if (ready) {
      loadTimes();
    }
  }, [ready, selection.branchId, selection.date]);

  return (
    <>
      <div className={forms.pair}>
        <Select
          density="compact"
          label="Branch"
          required
          value={selection.branchId}
          onChange={(event) => selection.selectBranch(event.target.value)}
        >
          <option value="">Choose a branch</option>
          {branches.items.map((branch) => (
            <option key={branch.id} value={branch.id}>
              {branch.name} — {branch.city}
            </option>
          ))}
        </Select>
        <div>
          <Input
            density="compact"
            label="Date (Malaysia)"
            type="date"
            required
            value={selection.date}
            min={dateWindow.firstDate}
            max={dateWindow.lastDate}
            aria-invalid={Boolean(dateError) || undefined}
            onChange={(event) => selection.selectDate(event.target.value)}
          />
          {dateError && (
            <p className={forms.error} role="alert">
              {dateError}
            </p>
          )}
        </div>
      </div>
      {selection.busy && <p className={forms.hint}>Checking available times…</p>}
      {offer && offer.slots.length === 0 && (
        <p className={forms.hint}>No times are available on this date. Choose another date.</p>
      )}
      {offer && offer.slots.length > 0 && (
        <fieldset className={styles.slots}>
          <legend className="sr-only">Available inspection times</legend>
          {offer.slots.map((slot) => (
            <label key={slot.startsAt} className={styles.slot}>
              <input
                className="sr-only"
                type="radio"
                name="inspection-slot"
                value={slot.startsAt}
                checked={selection.startsAt === slot.startsAt}
                onChange={() => selection.setStartsAt(slot.startsAt)}
              />
              {formatMalaysianDateTime(slot.startsAt, {
                year: undefined,
                month: undefined,
                day: undefined,
              })}
            </label>
          ))}
        </fieldset>
      )}
    </>
  );
}
