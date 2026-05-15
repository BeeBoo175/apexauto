"use client";

import { useState } from "react";
import type { useStaffSelection } from "./use-staff-selection";
import { useSettingsSave } from "./use-settings-save";

// The draft stays based on the first record read for this selection until newer values are chosen.
export function useSettingsDrawer<T extends { id: string }>({
  selection,
  creating,
  parse,
  onCloseCreate,
}: {
  selection: ReturnType<typeof useStaffSelection<T>>;
  creating: boolean;
  parse: (value: unknown) => T | null;
  onCloseCreate: () => void;
}) {
  const saver = useSettingsSave(parse);
  const [opened, setOpened] = useState<T | null>(null);
  const [revision, setRevision] = useState(0);
  const latest = selection.result?.data ?? null;
  if (!creating && latest && opened?.id !== latest.id) {
    setOpened(latest);
  }
  const base = creating || opened?.id !== latest?.id ? null : opened;
  const loading = !creating && selection.busy && !latest;

  function dismiss() {
    setOpened(null);
    if (creating) {
      onCloseCreate();
    } else {
      selection.close();
    }
  }

  function close() {
    if (!saver.busy) {
      dismiss();
    }
  }

  function adoptLatest() {
    setOpened(latest);
    setRevision((value) => value + 1);
  }

  return { saver, latest, base, loading, revision, dismiss, close, adoptLatest };
}
