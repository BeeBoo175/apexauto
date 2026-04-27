"use client";

import { useState } from "react";
import { FilterChoices } from "@/components/ui/FilterChoices";
import { ColumnFilterForm } from "./ColumnFilterForm";

// Single-choice column filter; Clear applies the "empty" value that means no filter.
export function ChoiceColumnFilter({
  label,
  options,
  value,
  empty,
  busy,
  onApply,
}: {
  label: string;
  options: { value: string; label: string }[];
  value: string;
  empty: string;
  busy: boolean;
  onApply: (value: string) => Promise<boolean>;
}) {
  const [draft, setDraft] = useState(value);
  return (
    <ColumnFilterForm busy={busy} onApply={() => onApply(draft)} onClear={() => onApply(empty)}>
      <FilterChoices
        label={label}
        options={options}
        value={draft}
        disabled={busy}
        onChange={setDraft}
      />
    </ColumnFilterForm>
  );
}
