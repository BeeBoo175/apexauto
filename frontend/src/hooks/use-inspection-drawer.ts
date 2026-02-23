"use client";

import { useCallback, useRef, useState } from "react";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseEditor, type EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import type { ApiResult } from "@/lib/intake/contracts";
import { useStaffSelection } from "./use-staff-selection";

export type RepairSelection = { requestId: string; revision: number };

export function useInspectionDrawer(
  id: string,
  initial: ApiResult<EditorSnapshot> | null,
  onChanged: () => void,
  initialRequest: RepairSelection | null = null,
) {
  const selection = useStaffSelection(id, initial, {
    parameter: "inspection",
    path: (id) => `drafts/${id}/workspace`,
    parse: parseEditor,
    matches: (value, id) => value.draft.id === id,
    read: inspectionFetch,
  });
  const [locked, setLocked] = useState(false);
  const [request, setRequest] = useState(initialRequest);
  const changed = useRef(false);
  const lock = useCallback((value: boolean) => setLocked(value), []);

  function open(id: string, request: RepairSelection | null = null) {
    if (locked) {
      return;
    }
    setRequest(request);
    void selection.choose(id);
    const url = new URL(window.location.href);
    url.searchParams.delete("request");
    url.searchParams.delete("revision");
    if (request) {
      url.searchParams.set("request", request.requestId);
      url.searchParams.set("revision", String(request.revision));
    }
    window.history.replaceState(null, "", url);
  }

  function close() {
    if (locked) {
      return;
    }
    selection.close();
    const url = new URL(window.location.href);
    url.searchParams.delete("request");
    url.searchParams.delete("revision");
    window.history.replaceState(null, "", url);
    setRequest(null);
    if (changed.current) {
      changed.current = false;
      onChanged();
    }
  }

  return {
    ...selection,
    open,
    close,
    locked,
    lock,
    request,
    markChanged: () => {
      changed.current = true;
    },
  };
}
