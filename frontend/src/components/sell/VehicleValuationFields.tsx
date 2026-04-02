import { Input, Select } from "@/components/ui/Input";
import type { Taxonomy } from "@/lib/intake/contracts";
import type { ValuationDraft } from "@/lib/intake/vehicle-draft";

export function VehicleValuationFields({
  taxonomy,
  value,
  onChange,
  yearBounds,
}: {
  taxonomy: Taxonomy;
  value: ValuationDraft;
  onChange: (value: ValuationDraft) => void;
  yearBounds?: { min: number; max: number };
}) {
  const make = taxonomy.makes.find((item) => item.name === value.make);
  const model = make?.models.find((item) => item.name === value.model);

  return (
    <>
      <Select
        label="Make"
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
        min={yearBounds?.min ?? 1}
        max={yearBounds?.max}
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
    </>
  );
}
