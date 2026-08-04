"use client";

import { useCallback, useRef, useState } from "react";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseEditor, type EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import type { ApiResult } from "@/lib/api";
import { useStaffSelection } from "./use-staff-selection";
import { useGuardedAction } from "./use-guarded-action";
import type { RepairSelection, WorkshopSection } from "@/lib/inspection/query";
import type { WorkshopSelectionPath } from "@/lib/inspection/workshop-location";
export type { RepairSelection } from "@/lib/inspection/query";

export type InspectionLocationHandler = (
  selection: WorkshopSelectionPath,
  mode: false | "push" | "replace",
) => void;

type Destination = Omit<WorkshopSelectionPath, "id">;

export function useInspectionDrawer(
  id: string,
  initial: ApiResult<EditorSnapshot> | null,
  onChanged: () => void,
  onLocation: InspectionLocationHandler,
  initialRequest: RepairSelection | null = null,
  initialSection: WorkshopSection = "inspection",
  initialCategory = "",
) {
  const interaction = useGuardedAction();
  const [locked, setLocked] = useState(false);
  const [request, setRequest] = useState(initialRequest);
  const [section, setSection] = useState(initialSection);
  const [category, setCategory] = useState(initialCategory);
  // The destination of the read in flight; it becomes the shown section only once the read is confirmed.
  const destination = useRef<Destination>({
    section: initialSection,
    request: initialRequest,
    category: initialCategory,
  });
  const changed = useRef(false);
  const lock = useCallback((value: boolean) => setLocked(value), []);
  const selection = useStaffSelection(id, initial, {
    parameter: "inspection",
    path: (id) => `drafts/${id}/workspace`,
    parse: parseEditor,
    matches: (value, id) => value.draft.id === id,
    read: inspectionFetch,
    commitAfterRead: true,
    address: (confirmedId, updateAddress = true) => {
      const target = confirmedId
        ? destination.current
        : { section: "inspection" as const, request: null, category: "" };
      onLocation({ id: confirmedId, ...target }, updateAddress && "push");
    },
  });

  function show(target: Destination) {
    destination.current = target;
    setSection(target.section);
    setRequest(target.request);
    setCategory(target.category);
  }

  function open(
    id: string,
    request: RepairSelection | null = null,
    section: WorkshopSection = request ? "repairs" : "inspection",
  ) {
    destination.current = { section, request, category: "" };
    void selection.choose(id, () => show(destination.current));
  }

  // Applies a browser Back/Forward or refresh destination without writing the address.
  function visit(target: WorkshopSelectionPath, updateAddress = false) {
    destination.current = {
      section: target.section,
      request: target.request,
      category: target.category,
    };
    if (target.id === selection.id && selection.result?.data) {
      show(destination.current);
      return Promise.resolve(selection.result.data);
    }
    return selection.load(target.id, () => show(destination.current), updateAddress);
  }

  function changeSection(next: WorkshopSection) {
    show({ section: next, request: null, category });
    onLocation({ id: selection.id, section: next, request: null, category }, "push");
  }

  // Moving between checklist categories stays inside the same open draft, so it never reaches the discard guard.
  function changeCategory(next: string, mode: "push" | "replace" = "push") {
    show({ section: "inspection", request: null, category: next });
    onLocation({ id: selection.id, section: "inspection", request: null, category: next }, mode);
  }

  function review(next: RepairSelection | null) {
    show({ section: "repairs", request: next, category });
    onLocation({ id: selection.id, section: "repairs", request: next, category }, "push");
  }

  function close() {
    interaction.request(() => closeConfirmed(), "Discard and close");
  }

  function closeConfirmed(updateAddress = true) {
    selection.close(updateAddress);
    show({ section: "inspection", request: null, category: "" });
    if (changed.current) {
      changed.current = false;
      onChanged();
    }
  }

  return {
    ...selection,
    open,
    visit,
    close,
    closeConfirmed,
    locked,
    lock,
    request,
    section,
    category,
    changeCategory,
    changeSection,
    review,
    markChanged: () => {
      changed.current = true;
    },
  };
}
