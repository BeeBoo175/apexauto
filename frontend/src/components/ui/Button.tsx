import React from "react";

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "danger" | "ghost" | "shell";
  size?: "sm" | "md" | "lg" | "compact" | "row" | "icon";
}

function buttonStyles(variant: ButtonProps["variant"], size: ButtonProps["size"]) {
  const base =
    "inline-flex items-center justify-center transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer";
  const typography =
    size === "compact" || size === "row" || size === "icon"
      ? "font-semibold tracking-normal"
      : "font-bold tracking-wider uppercase";
  const variants = {
    primary:
      "bg-signal hover:bg-signal-dark text-paper border border-signal-dark focus-visible:outline-graphite",
    secondary:
      "bg-paper hover:bg-canvas text-graphite border border-line focus-visible:outline-graphite",
    danger:
      "bg-signal-dark hover:bg-signal text-paper border border-signal-dark focus-visible:outline-graphite",
    ghost:
      "bg-transparent hover:bg-canvas text-graphite border border-transparent focus-visible:outline-graphite",
    shell:
      "bg-transparent hover:bg-white/10 text-white border border-white/25 focus-visible:outline-white",
  };
  const sizes = {
    sm: "h-9 px-3 text-xs rounded-sm",
    md: "h-11 px-5 text-xs rounded-sm",
    lg: "h-13 px-7 text-sm rounded-sm",
    compact: "h-8 px-3 text-[13px] rounded-sm max-[820px]:h-11",
    row: "min-h-7 px-2.5 py-1 text-xs rounded-sm max-[820px]:min-h-11",
    icon: "h-8 w-8 shrink-0 rounded-sm max-[820px]:h-11 max-[820px]:w-11",
  };
  return `${base} ${typography} ${variants[variant ?? "primary"]} ${sizes[size ?? "md"]}`;
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ variant = "primary", size = "md", className = "", children, ...props }, ref) => {
    return (
      <button ref={ref} className={`${buttonStyles(variant, size)} ${className}`.trim()} {...props}>
        {children}
      </button>
    );
  },
);

Button.displayName = "Button";
