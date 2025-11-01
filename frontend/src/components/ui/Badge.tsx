import React from "react";

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: "trust" | "warning" | "signal" | "neutral";
}

export const Badge: React.FC<BadgeProps> = ({
  variant = "neutral",
  className = "",
  children,
  ...props
}) => {
  const variantStyles = {
    trust: "bg-paper text-trust border-line font-bold",
    warning: "bg-paper text-warning border-line font-bold",
    signal: "bg-paper text-signal border-signal font-bold",
    neutral: "bg-paper text-muted border-soft-line font-medium",
  }[variant];

  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 text-xs uppercase tracking-wider border rounded-xs ${variantStyles} ${className}`.trim()}
      {...props}
    >
      {children}
    </span>
  );
};
