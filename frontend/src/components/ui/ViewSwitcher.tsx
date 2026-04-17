"use client";

import styles from "./ViewSwitcher.module.css";

export function ViewSwitcher<T extends string>({
  label,
  value,
  options,
  onChange,
  className = "",
}: {
  label: string;
  value: T;
  options: readonly [{ value: T; label: string }, { value: T; label: string }];
  onChange: (value: T) => void;
  className?: string;
}) {
  return (
    <div
      role="group"
      aria-label={label}
      data-second={value === options[1].value}
      className={`${styles.switch} ${className}`.trim()}
    >
      <span className={styles.thumb} aria-hidden="true" />
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          className={styles.option}
          aria-pressed={value === option.value}
          onClick={() => {
            if (value !== option.value) {
              onChange(option.value);
            }
          }}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
