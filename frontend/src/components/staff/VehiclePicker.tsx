"use client";

import { useState } from "react";
import { SearchField } from "@/components/ui/SearchField";
import type { useVehiclePicker } from "@/hooks/use-vehicle-picker";
import { formatCurrency } from "@/lib/currency-format";
import styles from "./AssistedDialogs.module.css";

// A search over the published cars with the results to choose from. The car page is the customer view; here
// staff only need to find the car by make, model, trim or year.
export function VehiclePicker({ picker }: { picker: ReturnType<typeof useVehiclePicker> }) {
  const [text, setText] = useState("");

  return (
    <form
      className={styles.stack}
      role="search"
      onSubmit={(event) => {
        event.preventDefault();
        void picker.search(text);
      }}
    >
      <SearchField
        label="Car"
        placeholder="Search by make, model, trim or year"
        density="default"
        value={text}
        disabled={false}
        busy={picker.busy}
        onChange={setText}
        onClear={
          text
            ? () => {
                setText("");
                void picker.search("");
              }
            : undefined
        }
      />
      {picker.message && (
        <p className={styles.error} role="alert">
          {picker.message}
        </p>
      )}
      {picker.searched && !picker.message && picker.items.length === 0 && (
        <p className={styles.vehicleMeta}>No published car matches this search.</p>
      )}
      {picker.items.length > 0 && (
        <ul className={styles.vehicles}>
          {picker.items.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                className={styles.vehicle}
                onClick={() => void picker.choose(item.id)}
              >
                <span className={styles.vehicleName}>
                  {item.manufactureYear} {item.make} {item.model} {item.trim}
                </span>
                <span className={styles.vehicleMeta}>
                  {item.branchName} · {formatCurrency(item.vehiclePrice)}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </form>
  );
}
