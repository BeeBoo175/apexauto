import { Input, Textarea } from "@/components/ui/Input";
import type { CheckInDraft } from "@/lib/staff/contracts";
import styles from "./AppointmentWorkbench.module.css";
import workspace from "./StaffWorkspace.module.css";

const confirmations = [
  ["customerPresent", "The customer is personally present"],
  ["identityConfirmed", "I have confirmed the customer's identity"],
  ["ownershipConfirmed", "I have confirmed vehicle ownership"],
  ["vehicleConfirmed", "The physical registration, make, model, trim and year match the booking"],
] as const;

export function CheckInFields({
  draft,
  onChange,
  disabled,
  retrospective = false,
}: {
  draft: CheckInDraft;
  onChange: (draft: CheckInDraft) => void;
  disabled: boolean;
  retrospective?: boolean;
}) {
  return (
    <fieldset disabled={disabled} className={`${workspace.stack} ${styles.fields}`}>
      <legend className="sr-only">Physical vehicle and customer verification</legend>
      <div className={styles.vehicleFields}>
        <Input
          density="compact"
          label="VIN"
          value={draft.vin}
          required
          minLength={17}
          maxLength={17}
          pattern="[A-HJ-NPR-Za-hj-npr-z0-9]{17}"
          onChange={(event) => onChange({ ...draft, vin: event.target.value.toUpperCase() })}
        />
        <Input
          density="compact"
          label="Actual odometer (km)"
          type="number"
          min="0"
          step="1"
          required
          value={draft.mileageKilometres}
          onChange={(event) => onChange({ ...draft, mileageKilometres: event.target.value })}
        />
      </div>
      <div className={styles.confirmations}>
        {confirmations.map(([key, label], index) => (
          <label key={key} className={styles.confirmation}>
            <input
              type="checkbox"
              required
              checked={draft[key]}
              onChange={(event) => onChange({ ...draft, [key]: event.target.checked })}
            />
            <span className={styles.step} aria-hidden="true">
              {String(index + 1).padStart(2, "0")}
            </span>
            <span>{retrospective ? retrospectiveConfirmations[index] : label}</span>
          </label>
        ))}
      </div>
      <Textarea
        label="Notes (optional)"
        maxLength={1000}
        rows={3}
        value={draft.notes}
        onChange={(event) => onChange({ ...draft, notes: event.target.value })}
      />
    </fieldset>
  );
}

const retrospectiveConfirmations = [
  "The customer attended personally at the recorded time",
  "The customer's identity was confirmed at attendance",
  "Vehicle ownership was confirmed at attendance",
  "The physical registration, make, model, trim and year matched the booking",
];
