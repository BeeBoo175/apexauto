import React from "react";
import { Icon } from "./Icon";

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  labelHidden?: boolean;
  density?: "default" | "compact";
}

const controlHeight = {
  default: "h-11 text-sm",
  compact: "h-8 text-[13px] max-[820px]:h-11 max-[820px]:text-base",
};

const inputFocus = {
  default: "focus:outline-none focus:border-graphite",
  compact: "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-graphite",
};

export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ label, labelHidden = false, density = "default", className = "", id, ...props }, ref) => {
    const inputId = id || (label ? label.toLowerCase().replace(/\s+/g, "-") : undefined);

    return (
      <div className="flex flex-col gap-1 w-full">
        {label && (
          <label
            htmlFor={inputId}
            className={
              labelHidden ? "sr-only" : "text-xs uppercase font-bold tracking-wider text-muted"
            }
          >
            {label}
          </label>
        )}
        <input
          ref={ref}
          id={inputId}
          className={`${controlHeight[density]} ${inputFocus[density]} min-w-0 px-3 bg-paper border border-line aria-invalid:border-signal rounded-sm text-graphite placeholder:text-muted disabled:bg-canvas disabled:text-muted ${className}`.trim()}
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
    <div className="flex flex-col gap-1 w-full">
      <label
        htmlFor={inputId}
        className={
          labelHidden ? "sr-only" : "text-xs uppercase font-bold tracking-wider text-muted"
        }
      >
        {label}
      </label>
      <div className="relative w-full">
        <select
          id={inputId}
          className={`${density === "compact" ? controlHeight.compact : "h-11 text-base"} w-full min-w-0 pl-3 pr-7 appearance-none bg-paper border border-line rounded-sm text-graphite focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-graphite disabled:bg-canvas disabled:text-muted cursor-pointer disabled:cursor-not-allowed ${className}`.trim()}
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
        <span
          className="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-2"
          aria-hidden="true"
        >
          <span
            className={`inline-flex items-center justify-center origin-center transition-transform duration-200 ease-in-out ${
              open ? "rotate-180" : "rotate-0"
            } ${props.disabled ? "text-muted" : "text-graphite"}`}
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
    <div className="flex flex-col gap-1 w-full">
      <label htmlFor={inputId} className="text-xs uppercase font-bold tracking-wider text-muted">
        {label}
      </label>
      <textarea
        id={inputId}
        className={`w-full min-w-0 p-3 bg-paper border border-line rounded-sm text-sm text-graphite focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-graphite disabled:bg-canvas disabled:text-muted max-[820px]:text-base ${className}`.trim()}
        {...props}
      />
    </div>
  );
}
