import React from "react";

export interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  dark?: boolean;
}

export const Card: React.FC<CardProps> = ({ dark = false, className = "", children, ...props }) => {
  const bgStyles = dark
    ? "bg-graphite text-paper border-graphite-soft"
    : "bg-paper text-graphite border-line";

  return (
    <div className={`border rounded-sm p-6 ${bgStyles} ${className}`.trim()} {...props}>
      {children}
    </div>
  );
};
