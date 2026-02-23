import React from "react";
import styles from "./Card.module.css";

export interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  dark?: boolean;
}

export const Card: React.FC<CardProps> = ({ dark = false, className = "", children, ...props }) => {
  return (
    <div className={`${styles.card} ${dark ? styles.dark : ""} ${className}`.trim()} {...props}>
      {children}
    </div>
  );
};
