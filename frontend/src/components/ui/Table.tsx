import React from "react";
import styles from "./Table.module.css";

export interface TableProps extends React.TableHTMLAttributes<HTMLTableElement> {
  ref?: React.Ref<HTMLTableElement>;
  density?: "default" | "compact";
  containerClassName?: string;
  framed?: boolean;
}

export const Table: React.FC<TableProps> = ({
  density = "default",
  framed = true,
  containerClassName = "",
  className = "",
  children,
  ...props
}) => {
  return (
    <div
      className={`w-full overflow-x-auto bg-paper ${framed ? "border border-line rounded-sm" : ""} ${containerClassName}`.trim()}
    >
      <table
        className={`w-full text-left border-collapse text-sm ${density === "compact" ? styles.compact : ""} ${className}`.trim()}
        {...props}
      >
        {children}
      </table>
    </div>
  );
};

export const TableCell: React.FC<React.TdHTMLAttributes<HTMLTableCellElement>> = ({
  className = "",
  children,
  ...props
}) => {
  return (
    <td
      className={`p-3 border-b border-line-soft text-graphite font-mono tabular-nums ${className}`.trim()}
      {...props}
    >
      {children}
    </td>
  );
};
