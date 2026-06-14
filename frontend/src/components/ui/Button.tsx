import React from "react";
import styles from "./Button.module.css";

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?:
    | "primary"
    | "secondary"
    | "muted"
    | "danger"
    | "ghost"
    | "shell"
    | "photo"
    | "choice"
    | "warning";
  size?: "sm" | "md" | "lg" | "compact" | "row" | "icon" | "tile" | "filter";
}

// Links and anchors that must look like buttons share the same class contract.
export function buttonClassName(
  variant: NonNullable<ButtonProps["variant"]> = "primary",
  size: NonNullable<ButtonProps["size"]> = "md",
  className = "",
) {
  return `${styles.button} ${styles[variant]} ${styles[size]} ${className}`.trim();
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ variant = "primary", size = "md", className = "", children, ...props }, ref) => {
    return (
      <button ref={ref} className={buttonClassName(variant, size, className)} {...props}>
        {children}
      </button>
    );
  },
);

Button.displayName = "Button";
