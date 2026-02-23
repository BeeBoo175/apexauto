import React from "react";
import styles from "./Badge.module.css";

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: "trust" | "warning" | "signal" | "neutral" | "info";
}

export const Badge: React.FC<BadgeProps> = ({
  variant = "neutral",
  className = "",
  children,
  ...props
}) => {
  return (
    <span className={`${styles.badge} ${styles[variant]} ${className}`.trim()} {...props}>
      {children}
    </span>
  );
};
