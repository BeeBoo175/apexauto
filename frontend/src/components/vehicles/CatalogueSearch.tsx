"use client";

import { useState } from "react";
import { SearchField } from "@/components/ui/SearchField";
import styles from "./Catalogue.module.css";

interface CatalogueSearchProps {
  // The applied search text, which also changes when a chip or filter pill clears it.
  search: string;
  busy: boolean;
  onSearch: (search: string) => void;
}

export function CatalogueSearch({ search, busy, onSearch }: CatalogueSearchProps) {
  const [text, setText] = useState(search);
  const [applied, setApplied] = useState(search);
  const [seen, setSeen] = useState(search);

  // An applied value this box did not submit itself (a cleared pill, a quick filter) replaces the typed text,
  // while a late answer to older typing leaves what is being typed alone.
  if (search !== seen) {
    setSeen(search);
    if (search !== applied) {
      setText(search);
      setApplied(search);
    }
  }

  function submit(value: string) {
    setApplied(value);
    onSearch(value);
  }

  return (
    <form
      className={styles.catalogueSearch}
      role="search"
      onSubmit={(event) => {
        event.preventDefault();
        submit(text.trim());
      }}
    >
      <SearchField
        label="Search vehicles"
        placeholder="Search by make, model, trim or year"
        density="default"
        value={text}
        disabled={false}
        busy={busy}
        onChange={setText}
        onClear={
          text
            ? () => {
                setText("");
                submit("");
              }
            : undefined
        }
      />
    </form>
  );
}
