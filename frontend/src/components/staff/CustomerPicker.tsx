"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { SearchField } from "@/components/ui/SearchField";
import { useCustomerLookup } from "@/hooks/use-customer-lookup";
import type { CustomerLookupPurpose, CustomerLookupRow } from "@/lib/staff/assisted-customer";
import dialogs from "./AssistedDialogs.module.css";
import styles from "./CustomerPicker.module.css";

// Staff who book for a customer choose an existing verified account here. Contact stays masked, and staff cannot
// create accounts: a customer without one registers first.
export function CustomerPicker({
  purpose,
  selected,
  disabled = false,
  onSelect,
  onClear,
}: {
  purpose: CustomerLookupPurpose;
  selected: CustomerLookupRow | null;
  disabled?: boolean;
  onSelect: (customer: CustomerLookupRow) => void;
  onClear: () => void;
}) {
  const lookup = useCustomerLookup(purpose);
  const [text, setText] = useState("");

  if (selected) {
    return (
      <div className={styles.selected}>
        <p className={styles.summary}>
          <span className={styles.label}>Acting for</span>
          <strong className={styles.name}>{selected.name}</strong>
          <span className={styles.contact}>
            {selected.email} · {selected.mobile}
          </span>
        </p>
        <Button
          variant="secondary"
          size="compact"
          className={dialogs.change}
          disabled={disabled}
          onClick={onClear}
        >
          Change customer
        </Button>
      </div>
    );
  }

  return (
    <form
      className={styles.picker}
      role="search"
      onSubmit={(event) => {
        event.preventDefault();
        void lookup.search(text);
      }}
    >
      <SearchField
        label="Customer account"
        placeholder="Search by name, email or mobile"
        density="default"
        value={text}
        disabled={disabled}
        busy={lookup.busy}
        onChange={setText}
        onClear={
          text
            ? () => {
                setText("");
                void lookup.search("");
              }
            : undefined
        }
      />
      <p className={styles.help}>
        Choose an existing verified customer. A customer without an account registers first.
      </p>
      {lookup.message && (
        <p className={styles.error} role="alert">
          {lookup.message}
        </p>
      )}
      {lookup.searched && !lookup.message && lookup.items.length === 0 && (
        <p className={styles.help}>No verified customer matches this search.</p>
      )}
      {lookup.items.length > 0 && (
        <ul className={styles.results}>
          {lookup.items.map((row) => (
            <li key={row.id}>
              <button
                type="button"
                className={styles.result}
                disabled={disabled}
                onClick={() => onSelect(row)}
              >
                <span className={styles.name}>{row.name}</span>
                <span className={styles.contact}>
                  {row.email} · {row.mobile}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </form>
  );
}
