import { Input, Select } from "@/components/ui/Input";
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
  const make = taxonomy.makes.find((item) => item.name === value.make);
  const model = make?.models.find((item) => item.name === value.model);
  return (
    <div className={styles.fields}>
      <Select
        label="Make"
        id="vehicle-make"
        required
        value={value.make}
        onChange={(event) => onChange({ ...value, make: event.target.value, model: "", trim: "" })}
      >
        <option value="">Choose make</option>
        {taxonomy.makes.map((item) => (
          <option key={item.name}>{item.name}</option>
        ))}
      </Select>
      <Select
        label="Model"
        id="vehicle-model"
        required
        value={value.model}
        disabled={!make}
        onChange={(event) => onChange({ ...value, model: event.target.value, trim: "" })}
      >
        <option value="">Choose model</option>
        {make?.models.map((item) => (
          <option key={item.name}>{item.name}</option>
        ))}
      </Select>
      <Select
        label="Trim"
        id="vehicle-trim"
        required
        value={value.trim}
        disabled={!model}
        onChange={(event) => onChange({ ...value, trim: event.target.value })}
      >
        <option value="">Choose trim</option>
        {model?.trims.map((item) => (
          <option key={item.name}>{item.name}</option>
        ))}
      </Select>
      <Input
        label="Manufacture year"
        type="number"
        min="1900"
        max="2100"
        step="1"
        required
        value={value.manufactureYear}
        onChange={(event) => onChange({ ...value, manufactureYear: event.target.value })}
      />
      <Input
        label="Declared mileage (km)"
        type="number"
        min="0"
        step="1"
        required
        value={value.mileageKilometres}
        onChange={(event) => onChange({ ...value, mileageKilometres: event.target.value })}
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
