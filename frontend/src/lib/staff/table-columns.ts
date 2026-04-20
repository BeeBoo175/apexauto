import type { DataColumn } from "@/components/ui/data-table/types";

type ColumnSizing = Pick<DataColumn<unknown>, "width" | "minWidth">;

// Preferred widths distribute space proportionally; minima keep narrow tables readable.
export const staffColumnSizing = {
  registration: {
    minWidth: 144,
    width: 160,
  },
  vehicle: {
    minWidth: 176,
    width: 240,
  },
  status: {
    minWidth: 152,
    width: 184,
  },
  shortStatus: {
    minWidth: 128,
    width: 152,
  },
  statusWithContext: {
    minWidth: 168,
    width: 216,
  },
  money: {
    minWidth: 152,
    width: 184,
  },
  person: {
    minWidth: 160,
    width: 208,
  },
  dateTime: {
    minWidth: 168,
    width: 192,
  },
  activity: {
    minWidth: 184,
    width: 288,
  },
  branch: {
    minWidth: 144,
    width: 160,
  },
} satisfies Record<string, ColumnSizing>;
