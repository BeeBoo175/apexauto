import type { ReactNode } from "react";
import type { IconName } from "@/lib/icons";

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
  /** Optional content floor; the full header and controls always set the default minimum. */
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
  /**
   * One empty state per table, the same on every tab: nothing narrows the table, or a search or column filter does.
   * The table writes the wording; the caller names its rows and supplies the clear that keeps the selected tab.
   */
  emptyState: {
    /** The plural name of the rows, such as "appointments". */
    noun: string;
    icon: IconName;
    /** A search or column filter, not the selected tab, is narrowing the table. */
    narrowed: boolean;
    /** One short line about the whole page, never about a tab. */
    hint?: string;
    onClear?: () => void;
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
  /**
   * Compact is the dense record table that fills its pane. Comfortable suits a table that sits among other content on
   * a page that scrolls: taller rows, wrapping cells and the height its rows need.
   */
  density?: "compact" | "comfortable";
}
