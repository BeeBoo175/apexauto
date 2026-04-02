import { Input } from "@/components/ui/Input";
import { VehicleValuationFields } from "./VehicleValuationFields";
import type { Taxonomy } from "@/lib/intake/contracts";
import type { VehicleDraft } from "@/lib/intake/vehicle-draft";
import styles from "./Sell.module.css";

export function VehicleFields({
  taxonomy,
  value,
  onChange,
}: {
  taxonomy: Taxonomy;
  value: VehicleDraft;
  onChange: (value: VehicleDraft) => void;
}) {
  return (
    <div className={styles.fields}>
      <VehicleValuationFields
        taxonomy={taxonomy}
        value={value}
        yearBounds={{ min: 1900, max: 2100 }}
        onChange={(valuation) => onChange({ ...value, ...valuation })}
      />
      <Input
        label="Registration plate"
        maxLength={20}
        required
        value={value.registrationPlate}
        onChange={(event) =>
          onChange({ ...value, registrationPlate: event.target.value.toUpperCase() })
        }
      />
    </div>
  );
}
