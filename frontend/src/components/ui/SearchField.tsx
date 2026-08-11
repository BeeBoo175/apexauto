"use client";

import { useEffect, useRef } from "react";
import { Button } from "./Button";
import { Icon } from "./Icon";
import { Input } from "./Input";
import styles from "./SearchField.module.css";

// Typing pauses this long before the enclosing form applies the search.
const SEARCH_DEBOUNCE_MS = 400;

// Every search bar applies itself: a pause in typing submits the enclosing form, so no one has to press a Search
// button. A bar outside a form (a local filter) has nothing to submit and simply filters as the value changes.
export function SearchField({
  label,
  value,
  disabled,
  onChange,
  onClear,
  placeholder,
  busy = false,
  disabledReason,
  density = "compact",
}: {
  label: string;
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
  onClear?: () => void;
  placeholder: string;
  busy?: boolean;
  disabledReason?: string;
  density?: "default" | "compact";
}) {
  const field = useRef<HTMLDivElement>(null);
  const timer = useRef(0);
  // A change made while a read is running waits for it, so the newest text is never dropped.
  const queued = useRef(false);
  const busyNow = useRef(busy);

  function submit() {
    window.clearTimeout(timer.current);
    queued.current = false;
    field.current?.closest("form")?.requestSubmit();
  }

  function schedule() {
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => {
      if (busyNow.current) {
        queued.current = true;
        return;
      }
      submit();
    }, SEARCH_DEBOUNCE_MS);
  }

  useEffect(() => {
    busyNow.current = busy;
    if (!busy && queued.current) {
      submit();
    }
  }, [busy]);

  useEffect(() => () => window.clearTimeout(timer.current), []);

  return (
    <div ref={field} className={styles.field}>
      <Input
        label={label}
        labelHidden
        density={density}
        type="search"
        maxLength={160}
        placeholder={placeholder}
        value={value}
        disabled={disabled}
        title={disabledReason}
        onChange={(event) => {
          onChange(event.target.value);
          schedule();
        }}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            event.preventDefault();
            submit();
          }
        }}
      />
      {onClear && (
        <Button
          type="button"
          size="icon"
          variant="ghost"
          aria-label="Clear search"
          title={disabledReason ?? "Clear search"}
          disabled={disabled || busy}
          onClick={() => {
            window.clearTimeout(timer.current);
            queued.current = false;
            onClear();
          }}
        >
          <Icon name="close" />
        </Button>
      )}
      <span className={styles.searchIcon} aria-hidden="true">
        <Icon name="search" />
      </span>
    </div>
  );
}
