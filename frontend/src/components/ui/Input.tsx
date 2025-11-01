import React from "react";

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
}

export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ label, error, className = "", id, ...props }, ref) => {
    const inputId = id || (label ? label.toLowerCase().replace(/\s+/g, "-") : undefined);

    return (
      <div className="flex flex-col gap-1 w-full">
        {label && (
          <label htmlFor={inputId} className="text-xs uppercase font-bold tracking-wider text-muted">
            {label}
          </label>
        )}
        <input
          ref={ref}
          id={inputId}
          className={`h-11 px-3 bg-paper border rounded-sm text-sm text-graphite placeholder:text-muted focus:outline-none focus:border-graphite disabled:bg-canvas disabled:text-muted ${
            error ? "border-signal" : "border-line"
          } ${className}`.trim()}
          {...props}
        />
        {error && <span className="text-xs text-signal font-medium">{error}</span>}
      </div>
    );
  }
);

Input.displayName = "Input";
