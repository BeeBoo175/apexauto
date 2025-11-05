import React from "react";

export type TableProps = React.TableHTMLAttributes<HTMLTableElement>;

export const Table: React.FC<TableProps> = ({ className = "", children, ...props }) => {
  return (
    <div className="w-full overflow-x-auto border border-line rounded-sm bg-paper">
      <table className={`w-full text-left border-collapse text-sm ${className}`.trim()} {...props}>
        {children}
      </table>
    </div>
  );
};

export interface TableHeaderProps extends React.ThHTMLAttributes<HTMLTableCellElement> {
  sortDirection?: "asc" | "desc" | "none";
  onSort?: () => void;
}

export const TableHeader: React.FC<TableHeaderProps> = ({
  sortDirection,
  onSort,
  className = "",
  children,
  ...props
}) => {
  const isSortable = !!onSort;

  return (
    <th
      className={`p-3 text-xs uppercase font-bold tracking-wider text-muted border-b border-line bg-canvas ${
        isSortable ? "cursor-pointer select-none" : ""
      } ${className}`.trim()}
      onClick={onSort}
      aria-sort={
        sortDirection === "asc" ? "ascending" : sortDirection === "desc" ? "descending" : undefined
      }
      {...props}
    >
      <div className="flex items-center gap-1.5">
        <span>{children}</span>
        {isSortable && (
          <span className="text-[10px] text-muted">
            {sortDirection === "asc" ? "▲" : sortDirection === "desc" ? "▼" : "▲"}
          </span>
        )}
      </div>
    </th>
  );
};

export const TableCell: React.FC<React.TdHTMLAttributes<HTMLTableCellElement>> = ({
  className = "",
  children,
  ...props
}) => {
  return (
    <td
      className={`p-3 border-b border-soft-line text-graphite font-mono tabular-nums ${className}`.trim()}
      {...props}
    >
      {children}
    </td>
  );
};
