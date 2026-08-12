import { useId, type RefObject } from "react";
import { Input } from "@/components/ui/Input";
import {
  REGISTRATION_PLATE_MAX_LENGTH,
  sanitizeRegistrationPlateInput,
} from "@/lib/intake/registration-plate";
import { VehicleValuationFields } from "./VehicleValuationFields";
import type { Taxonomy } from "@/lib/intake/contracts";
import type { VehicleDraft } from "@/lib/intake/vehicle-draft";
import styles from "./Sell.module.css";

export function VehicleFields({
  taxonomy,
  value,
  onChange,
  plateError,
  onPlateBlur,
  plateInputRef,
}: {
  taxonomy: Taxonomy;
  value: VehicleDraft;
  onChange: (value: VehicleDraft) => void;
  plateError?: string | null;
  onPlateBlur?: () => void;
  plateInputRef?: RefObject<HTMLInputElement | null>;
}) {
  const plateHelpId = useId();
  const plateErrorId = useId();

  return (
    <div className={styles.fields}>
      <VehicleValuationFields
        taxonomy={taxonomy}
        value={value}
        onChange={(valuation) => onChange({ ...value, ...valuation })}
      />
      <div className={styles.plateFieldGroup}>
        <Input
          ref={plateInputRef}
          label="Registration plate"
          maxLength={REGISTRATION_PLATE_MAX_LENGTH}
          placeholder="e.g. VCA 6242"
          autoCapitalize="characters"
          autoCorrect="off"
          spellCheck={false}
          required
          value={value.registrationPlate}
          aria-invalid={Boolean(plateError) || undefined}
          aria-describedby={plateError ? `${plateErrorId} ${plateHelpId}` : plateHelpId}
          onChange={(event) =>
            onChange({
              ...value,
              registrationPlate: sanitizeRegistrationPlateInput(event.target.value),
            })
          }
          onBlur={onPlateBlur}
        />
        {plateError && (
          <p id={plateErrorId} className={styles.fieldError} role="alert">
            {plateError}
          </p>
        )}
        <p id={plateHelpId} className={styles.help}>
          Malaysian JPJ plate (e.g. VCA 6242 or W 1234 A).
        </p>
      </div>
    </div>
  );
}
