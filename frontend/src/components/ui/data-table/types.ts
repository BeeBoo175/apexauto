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
  width?: number;
  minWidth?: number;
  align?: "left" | "right";
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
