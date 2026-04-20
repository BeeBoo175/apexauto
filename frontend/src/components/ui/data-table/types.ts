import type { ReactNode } from "react";

export interface TableSort {
  column: string;
  direction: "asc" | "desc";
}

export interface DataColumn<Row> {
  id: string;
  label: string;
  cell: (row: Row) => ReactNode;
  sortable?: boolean;
  /** Preferred width in pixels, used proportionally as the container changes. */
  width?: number;
  /** Smallest readable width; the table scrolls when these no longer fit. */
  minWidth?: number;
  align?: "left" | "right";
  filter?: TableColumnFilter;
}

export interface TableColumnFilter {
  active: boolean;
  content: ReactNode;
  onOpenChange?: (open: boolean) => void;
}

export interface TablePage {
  page: number;
  pageSize: number;
  total: number;
}

export interface TableSelection {
  ids: ReadonlySet<string>;
  onChange: (ids: Set<string>) => void;
}

export interface TableSorting {
  value: TableSort | null;
  onChange: (sort: TableSort | null) => void;
}

export interface DataTableProps<Row> {
  label: string;
  /** Feature-owned search, criteria, queue tabs and export actions. */
  toolbar?: ReactNode;
  rows: readonly Row[];
  emptyState: {
    title: string;
    description: string;
    action?: { label: string; onClick: () => void };
  };
  columns: readonly DataColumn<Row>[];
  rowId: (row: Row) => string;
  page: TablePage;
  sorting?: TableSorting;
  onPage: (page: number, pageSize: number) => void;
  disabled: boolean;
  busy: boolean;
  selection?: TableSelection;
  selectedRowId?: string;
  onRowClick?: (row: Row) => void;
}
