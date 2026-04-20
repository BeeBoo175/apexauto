export interface ResizeColumn {
  id: string;
  /** Preferred share of the available width; every data column stays responsive. */
  width?: number;
  minWidth?: number;
}
type Widths = Record<string, number>;

export function columnLayout(columns: readonly ResizeColumn[], widths: Widths, selection: boolean) {
  const tracks = columns.map((column) => {
    const minimum = column.minWidth ?? 100;
    const weight = Math.max(minimum, widths[column.id] ?? column.width ?? 160);
    return `minmax(${minimum}px, ${weight}fr)`;
  });
  const selectionWidth = selection ? 44 : 0;
  if (selection) {
    tracks.unshift(`${selectionWidth}px`);
  }
  return {
    gridTemplateColumns: tracks.join(" "),
    minWidth: columns.reduce((sum, column) => sum + (column.minWidth ?? 100), selectionWidth),
  };
}

function availableWidth(column: ResizeColumn, widths: Widths) {
  return Math.max(0, widths[column.id] - (column.minWidth ?? 100));
}

function shrinkColumns(columns: readonly ResizeColumn[], widths: Widths, amount: number) {
  let remaining = amount;
  for (const column of columns) {
    if (remaining <= 0) {
      return;
    }
    const change = Math.min(remaining, availableWidth(column, widths));
    widths[column.id] -= change;
    remaining -= change;
  }
}

export function resizeColumnWidths(
  columns: readonly ResizeColumn[],
  initial: Widths,
  divider: number,
  delta: number,
): Widths {
  if (divider < 0 || divider >= columns.length - 1 || !Number.isFinite(delta) || delta === 0) {
    return initial;
  }
  const left = columns.slice(0, divider + 1).reverse();
  const right = columns.slice(divider + 1);
  const growing = delta > 0 ? columns[divider] : columns[divider + 1];
  const shrinking = delta > 0 ? right : left;
  const shrinkage = shrinking.reduce((sum, column) => sum + availableWidth(column, initial), 0);
  const amount = Math.min(Math.abs(delta), shrinkage);
  if (amount === 0) {
    return initial;
  }
  const widths = { ...initial };
  // Only the adjacent column grows; reductions cascade outward to preserve the right edge.
  widths[growing.id] += amount;
  shrinkColumns(shrinking, widths, amount);
  return widths;
}
