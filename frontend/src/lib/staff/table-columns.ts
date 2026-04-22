import type { DataColumn } from "@/components/ui/data-table/types";

type ColumnSizing = Pick<DataColumn<unknown>, "width" | "minWidth">;

// Preferred widths distribute spare space; DataTable measures each full header minimum.
export const staffColumnSizing = {
  registration: {
    width: 160,
  },
  vehicle: {
    width: 240,
  },
  status: {
    width: 184,
  },
  shortStatus: {
    width: 152,
  },
  statusWithContext: {
    width: 216,
  },
  money: {
    width: 184,
  },
  person: {
    width: 208,
  },
  dateTime: {
    width: 192,
  },
  activity: {
    width: 288,
  },
  branch: {
    width: 160,
  },
} satisfies Record<string, ColumnSizing>;
