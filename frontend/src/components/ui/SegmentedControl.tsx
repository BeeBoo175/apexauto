import type { ReactNode } from "react";
import styles from "./SegmentedControl.module.css";

export interface SegmentedOption {
  key: string;
  label: ReactNode;
  // Spoken name when the visible label is shortened.
  ariaLabel?: string;
  selected: boolean;
  onSelect: () => void;
}

// One joined row of toggle buttons. Each option reports its own pressed state, so the same control serves a
// single choice, a set of independent toggles and a preset that clears when pressed again.
export function SegmentedControl({
  label,
  options,
  fill = false,
}: {
  label: string;
  options: readonly SegmentedOption[];
  fill?: boolean;
}) {
  return (
    <div className={styles.group} role="group" aria-label={label} data-fill={fill || undefined}>
      {options.map((option) => (
        <button
          key={option.key}
          type="button"
          className={styles.segment}
          aria-label={option.ariaLabel}
          aria-pressed={option.selected}
          onClick={option.onSelect}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
