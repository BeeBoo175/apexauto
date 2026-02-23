import React from "react";
import { Icon } from "./Icon";
import styles from "./Input.module.css";

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  labelHidden?: boolean;
  density?: "default" | "compact";
}

export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ label, labelHidden = false, density = "default", className = "", id, ...props }, ref) => {
    const inputId = id || (label ? label.toLowerCase().replace(/\s+/g, "-") : undefined);

    return (
      <div className={styles.field}>
        {label && (
          <label htmlFor={inputId} className={labelHidden ? "sr-only" : styles.label}>
            {label}
          </label>
        )}
        <input
          ref={ref}
          id={inputId}
          className={`${styles.control} ${styles.input} ${density === "compact" ? styles.compact : ""} ${className}`.trim()}
          {...props}
        />
      </div>
    );
  },
);

Input.displayName = "Input";

export interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label: string;
  labelHidden?: boolean;
  density?: InputProps["density"];
}

export function Select({
  label,
  labelHidden = false,
  id,
  density = "default",
  className = "",
  children,
  onClick,
  onChange,
  onBlur,
  onKeyDown,
  ...props
}: SelectProps) {
  const generatedId = React.useId();
  const inputId = id ?? generatedId;
  const [open, setOpen] = React.useState(false);
  return (
    <div className={styles.field}>
      <label htmlFor={inputId} className={labelHidden ? "sr-only" : styles.label}>
        {label}
      </label>
      <div className={styles.selectFrame}>
        <select
          id={inputId}
          className={`${styles.control} ${styles.select} ${density === "compact" ? styles.compact : ""} ${className}`.trim()}
          onClick={(event) => {
            setOpen((prev) => !prev);
            onClick?.(event);
          }}
          onChange={(event) => {
            setOpen(false);
            onChange?.(event);
          }}
          onBlur={(event) => {
            setOpen(false);
            onBlur?.(event);
          }}
          onKeyDown={(event) => {
            if (event.key === "Escape" || event.key === "Tab" || event.key === "Enter") {
              setOpen(false);
            }
            onKeyDown?.(event);
          }}
          {...props}
        >
          {children}
        </select>
        <span className={styles.selectArrow} aria-hidden="true">
          <span
            className={styles.arrow}
            data-open={open || undefined}
            data-disabled={props.disabled || undefined}
          >
            <Icon name="down" />
          </span>
        </span>
      </div>
    </div>
  );
}

export interface TextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string;
}

export function Textarea({ label, id, className = "", ...props }: TextareaProps) {
  const generatedId = React.useId();
  const inputId = id ?? generatedId;
  return (
    <div className={styles.field}>
      <label htmlFor={inputId} className={styles.label}>
        {label}
      </label>
      <textarea
        id={inputId}
        className={`${styles.control} ${styles.textarea} ${className}`.trim()}
        {...props}
      />
    </div>
  );
}
