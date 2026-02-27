"use client";

import { useState } from "react";

export function useRowSelection<Row>(
  rows: readonly Row[],
  rowId: (row: Row) => string,
  scope: string,
) {
  const [selection, setSelection] = useState({ scope, ids: new Set<string>() });
  // Selection belongs to exactly the confirmed actor/query/page snapshot supplied by the caller.
  const ids = selection.scope === scope ? selection.ids : new Set<string>();

  function onChange(nextIds: Set<string>) {
    setSelection({ scope, ids: nextIds });
  }

  function clear() {
    onChange(new Set());
  }

  return {
    selection: { ids, onChange },
    selectedRows: rows.filter((row) => ids.has(rowId(row))),
    clear,
  };
}
