import { Input, Select } from "@/components/ui/Input";
import type { Taxonomy } from "@/lib/intake/contracts";
import type { ValuationDraft } from "@/lib/intake/vehicle-draft";
import { supportedManufactureYears } from "@/lib/intake/manufacture-years";

export function VehicleValuationFields({
  taxonomy,
  value,
  onChange,
  density = "default",
}: {
  taxonomy: Taxonomy;
  value: ValuationDraft;
  onChange: (value: ValuationDraft) => void;
  density?: "default" | "compact";
}) {
  const make = taxonomy.makes.find((item) => item.name === value.make);
  const model = make?.models.find((item) => item.name === value.model);
  const trim = model?.trims.find((item) => item.name === value.trim);

  return (
    <>
      <Select
        density={density}
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
        density={density}
        label="Model"
        required
        value={value.model}
        disabled={!make}
        onChange={(event) => onChange({ ...value, model: event.target.value, trim: "" })}
      >
        <option value="">{make ? "Choose model" : "Choose a make first"}</option>
        {make?.models.map((item) => (
          <option key={item.name}>{item.name}</option>
        ))}
      </Select>
      <Select
        density={density}
        label="Trim"
        required
        value={value.trim}
        disabled={!model}
        onChange={(event) => onChange({ ...value, trim: event.target.value, manufactureYear: "" })}
      >
        <option value="">{model ? "Choose trim" : "Choose a model first"}</option>
        {model?.trims.map((item) => (
          <option key={item.name}>{item.name}</option>
        ))}
      </Select>
      <Select
        density={density}
        label="Manufacture year"
        required
        value={value.manufactureYear}
        disabled={!trim}
        onChange={(event) => onChange({ ...value, manufactureYear: event.target.value })}
      >
        <option value="">{trim ? "Choose year" : "Choose a trim first"}</option>
        {supportedManufactureYears(trim, new Date()).map((year) => (
          <option key={year}>{year}</option>
        ))}
      </Select>
      <Input
        density={density}
        label="Declared mileage (km)"
        type="number"
        min="0"
        max={taxonomy.maximumMileageKilometres}
        step="1"
        required
        value={value.mileageKilometres}
        onChange={(event) => onChange({ ...value, mileageKilometres: event.target.value })}
      />
    </>
  );
}
