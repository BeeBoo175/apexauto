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
      className={`${styles.container} ${framed ? styles.framed : ""} ${containerClassName}`.trim()}
    >
      <table
        className={`${styles.table} ${density === "compact" ? styles.compact : ""} ${className}`.trim()}
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
    <td className={`${styles.cell} ${className}`.trim()} {...props}>
      {children}
    </td>
  );
};
