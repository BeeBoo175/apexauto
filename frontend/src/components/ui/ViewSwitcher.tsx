"use client";

import type { IconName } from "@/lib/icons";
import { Icon } from "./Icon";
import styles from "./ViewSwitcher.module.css";

interface ViewOption<T extends string> {
  value: T;
  label: string;
  icon?: IconName;
}

export function ViewSwitcher<T extends string>({
  label,
  value,
  options,
  onChange,
  className = "",
  labelled = false,
}: {
  label: string;
  value: T;
  // An option with an icon shows it in place of the text, which stays as its accessible name and tooltip.
  options: readonly [ViewOption<T>, ViewOption<T>];
  onChange: (value: T) => void;
  className?: string;
  // Shows the text beside the icon, in wider options, when the icons alone would not be obvious.
  labelled?: boolean;
}) {
  return (
    <div
      role="group"
      aria-label={label}
      data-second={value === options[1].value}
      className={`${styles.switch} ${labelled ? styles.labelled : ""} ${className}`.trim()}
    >
      <span className={styles.thumb} aria-hidden="true" />
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          className={styles.option}
          aria-pressed={value === option.value}
          aria-label={option.icon && !labelled ? option.label : undefined}
          title={option.icon && !labelled ? option.label : undefined}
          onClick={() => {
            if (value !== option.value) {
              onChange(option.value);
            }
          }}
        >
          {option.icon && <Icon name={option.icon} />}
          {(!option.icon || labelled) && option.label}
        </button>
      ))}
    </div>
  );
}
