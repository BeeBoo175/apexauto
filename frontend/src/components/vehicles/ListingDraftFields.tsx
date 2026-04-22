"use client";

import { useId } from "react";
import { Input, Select, Textarea } from "@/components/ui/Input";
import type { ListingDraft } from "@/lib/vehicles/listing-draft";
import { fuels, transmissions } from "@/lib/vehicles/contracts";
import { parseMyr } from "@/lib/currency-input";
import styles from "./ListingEditor.module.css";

export function ListingDraftFields({
  value,
  disabled = false,
  readOnly = false,
  validationAttempted = false,
  onChange,
}: {
  value: ListingDraft;
  disabled?: boolean;
  readOnly?: boolean;
  validationAttempted?: boolean;
  onChange?: (value: ListingDraft) => void;
}) {
  const fieldsId = useId();
  const price = parseMyr(value.vehiclePrice);
  const roadTax = parseMyr(value.roadTax);

  return (
    <div className={styles.fields}>
      <section className={styles.fieldSection} aria-label="Listing price">
        <h3>Pricing</h3>
        <Input
          id={`${fieldsId}-price`}
          label="Vehicle price (MYR)"
          placeholder="e.g. 68500.00"
          inputMode="decimal"
          density="compact"
          value={value.vehiclePrice}
          required
          aria-invalid={validationAttempted && price === null}
          disabled={disabled}
          readOnly={readOnly}
          onChange={(event) => onChange?.({ ...value, vehiclePrice: event.target.value })}
        />
        <Input
          id={`${fieldsId}-road-tax`}
          label="Road tax (MYR)"
          placeholder="e.g. 90.00"
          density="compact"
          inputMode="decimal"
          value={value.roadTax}
          required
          aria-invalid={validationAttempted && roadTax === null}
          disabled={disabled}
          readOnly={readOnly}
          onChange={(event) => onChange?.({ ...value, roadTax: event.target.value })}
        />
      </section>
      <section className={styles.fieldSection} aria-label="Listing specifications">
        <h3>Specifications</h3>
        <div className={styles.fieldPair}>
          <Select
            label="Transmission"
            density="compact"
            value={value.transmission}
            required
            aria-invalid={
              validationAttempted && !transmissions.some((name) => name === value.transmission)
            }
            disabled={disabled || readOnly}
            onChange={(event) => onChange?.({ ...value, transmission: event.target.value })}
          >
            <option value="">Choose transmission</option>
            {transmissions.map((name) => (
              <option key={name} value={name}>
                {name}
              </option>
            ))}
          </Select>
          <Select
            label="Fuel"
            density="compact"
            value={value.fuel}
            required
            aria-invalid={validationAttempted && !fuels.some((name) => name === value.fuel)}
            disabled={disabled || readOnly}
            onChange={(event) => onChange?.({ ...value, fuel: event.target.value })}
          >
            <option value="">Choose fuel</option>
            {fuels.map((name) => (
              <option key={name} value={name}>
                {name === "PlugInHybrid" ? "Plug-in hybrid" : name}
              </option>
            ))}
          </Select>
        </div>
      </section>
      <section className={styles.fieldSection} aria-label="Listing description">
        <Textarea
          label="Public description"
          placeholder="Describe the vehicle's condition, features and highlights for buyers"
          rows={7}
          maxLength={2000}
          value={value.description}
          required
          aria-invalid={
            validationAttempted &&
            (!value.description.trim() ||
              value.description.trim().length > 2000 ||
              value.description.includes("\0"))
          }
          disabled={disabled}
          readOnly={readOnly}
          onChange={(event) => onChange?.({ ...value, description: event.target.value })}
        />
        <p className={styles.help}>
          {value.description.length.toLocaleString("en-MY")} / 2,000 characters
        </p>
      </section>
    </div>
  );
}
