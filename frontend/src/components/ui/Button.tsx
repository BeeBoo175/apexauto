import React from "react";

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "danger" | "ghost";
  size?: "sm" | "md" | "lg";
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ variant = "primary", size = "md", className = "", children, ...props }, ref) => {
    const baseStyles =
      "inline-flex items-center justify-center font-bold tracking-wider uppercase transition-colors focus:outline-none disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer";

    const variantStyles = {
      primary: "bg-signal hover:bg-signal-dark text-paper border border-signal-dark",
      secondary: "bg-paper hover:bg-canvas text-graphite border border-line",
      danger: "bg-signal-dark hover:bg-signal text-paper border border-signal-dark",
      ghost: "bg-transparent hover:bg-canvas text-graphite border border-transparent",
    }[variant];

    const sizeStyles = {
      sm: "h-9 px-3 text-xs rounded-sm",
      md: "h-11 px-5 text-xs rounded-sm",
      lg: "h-13 px-7 text-sm rounded-sm",
    }[size];

    return (
      <button
        ref={ref}
        className={`${baseStyles} ${variantStyles} ${sizeStyles} ${className}`.trim()}
        {...props}
      >
        {children}
      </button>
    );
  },
);

Button.displayName = "Button";
