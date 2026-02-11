import type { ReactNode } from "react";

export interface TableSort {
  column: string;
  direction: "asc" | "desc";
}

export interface DataColumn<Row> {
  id: string;
  label: string;
  exportLabel?: string;
  exportOnly?: boolean;
  cell: (row: Row) => ReactNode;
  exportValue?: (row: Row) => string;
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

export interface DataTableProps<Row> {
  label: string;
  fillHeight?: boolean;
  rows: readonly Row[];
  columns: readonly DataColumn<Row>[];
  rowId: (row: Row) => string;
  page: TablePage;
  sort: TableSort | null;
  onSort: (sort: TableSort | null) => void;
  onPage: (page: number, pageSize: number) => void;
  disabled: boolean;
  busy: boolean;
  selectionScope?: string;
  toolbar?: ReactNode;
  filters?: ReactNode;
  filterCount?: number;
  filterSummary?: ReactNode;
  onFiltersOpenChange?: (open: boolean) => void;
  defaultFiltersExpanded?: boolean;
  selectedRowId?: string;
  onRowClick?: (row: Row) => void;
  exportFileName?: string;
  onExportAll?: () => Promise<void>;
}
