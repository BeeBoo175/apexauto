"use client";

import { useCallback, useRef, useState } from "react";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseEditor, type EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import type { ApiResult } from "@/lib/api";
import { useStaffSelection } from "./use-staff-selection";
import { useGuardedAction } from "./use-guarded-action";
import type { RepairSelection, WorkshopSection } from "@/lib/inspection/query";
export type { RepairSelection } from "@/lib/inspection/query";

export function useInspectionDrawer(
  id: string,
  initial: ApiResult<EditorSnapshot> | null,
  onChanged: () => void,
  initialRequest: RepairSelection | null = null,
  initialSection: WorkshopSection = "inspection",
) {
  const interaction = useGuardedAction();
  const selection = useStaffSelection(id, initial, {
    parameter: "inspection",
    path: (id) => `drafts/${id}/workspace`,
    parse: parseEditor,
    matches: (value, id) => value.draft.id === id,
    read: inspectionFetch,
    commitAfterRead: true,
  });
  const [locked, setLocked] = useState(false);
  const [request, setRequest] = useState(initialRequest);
  const [section, setSection] = useState(initialSection);
  const changed = useRef(false);
  const lock = useCallback((value: boolean) => setLocked(value), []);

  function open(
    id: string,
    request: RepairSelection | null = null,
    section: WorkshopSection = request ? "repairs" : "inspection",
  ) {
    void selection.choose(id, (url) => {
      setRequest(request);
      setSection(section);
      url.searchParams.set("section", section);
      url.searchParams.delete("request");
      url.searchParams.delete("revision");
      if (request) {
        url.searchParams.set("request", request.requestId);
        url.searchParams.set("revision", String(request.revision));
      }
    });
  }

  function changeSection(next: WorkshopSection) {
    setSection(next);
    const url = new URL(window.location.href);
    url.searchParams.set("section", next);
    window.history.replaceState(null, "", url);
  }

  function review(request: RepairSelection | null) {
    setRequest(request);
    changeSection("repairs");
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
    interaction.request(closeConfirmed, "Discard and close");
  }

  function closeConfirmed() {
    selection.close();
    const url = new URL(window.location.href);
    url.searchParams.delete("request");
    url.searchParams.delete("revision");
    url.searchParams.delete("section");
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
    section,
    changeSection,
    review,
    markChanged: () => {
      changed.current = true;
    },
  };
}
