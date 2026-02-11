import { useId } from "react";
import styles from "./FilterChoices.module.css";

export interface FilterChoice {
  value: string;
  label: string;
}

export function FilterChoices({
  label,
  options,
  value,
  disabled,
  onChange,
}: {
  label: string;
  options: readonly FilterChoice[];
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  const name = useId();

  return (
    <div className={styles.choices} role="radiogroup" aria-label={label}>
      {options.map((option) => (
        <label key={option.value} className={styles.choice}>
          <input
            type="radio"
            name={name}
            value={option.value}
            checked={value === option.value}
            disabled={disabled}
            onChange={() => onChange(option.value)}
          />
          <span>{option.label}</span>
        </label>
      ))}
    </div>
  );
}
