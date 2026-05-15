"use client";

import { useLayoutEffect } from "react";
import { useToast } from "@/components/ui/Toast";
import { parseSettings, type PurchaseSettings } from "@/lib/settings/settings";
import { draftFrom, readDraft, unchanged, type SettingsDraft } from "@/lib/settings/settings-draft";
import { useEditGuard } from "./use-edit-guard";
import { useSettingsSave } from "./use-settings-save";

export function usePurchaseSettingsForm({
  settings,
  draft,
  busy,
  onDraft,
  onSaved,
  onLocked,
}: {
  settings: PurchaseSettings;
  draft: SettingsDraft | null;
  busy: boolean;
  onDraft: (draft: SettingsDraft | null) => void;
  onSaved: (settings: PurchaseSettings) => void;
  onLocked: (locked: boolean) => void;
}) {
  const toast = useToast();
  const saver = useSettingsSave(parseSettings);
  const current = draft ?? draftFrom(settings);
  const { values, errors } = readDraft(current, settings.bounds);
  const changed = values !== null && !unchanged(values, settings);
  const uncertain = Boolean(saver.notice?.uncertain);
  const disabled = busy || saver.busy || uncertain;
  const dirty = Boolean(draft) && (!values || changed);
  const locked = dirty || saver.busy || uncertain;
  useEditGuard({
    dirty,
    pending: saver.busy,
    uncertain,
    label: "Purchase settings",
    discard,
  });
  useLayoutEffect(() => {
    onLocked(locked);
    return () => onLocked(false);
  }, [locked, onLocked]);

  function field(key: keyof SettingsDraft, value: string) {
    saver.clearNotice();
    onDraft({ ...current, [key]: value });
  }

  function discard() {
    saver.clearNotice();
    onDraft(null);
  }

  async function submit() {
    if (!values || !changed) {
      return;
    }
    const saved = await saver.save("/api/staff/settings", "PUT", {
      ...values,
      expectedVersion: settings.version,
    });
    if (saved) {
      onDraft(null);
      onSaved(saved);
      toast.success("Settings saved.");
    }
  }

  async function reload() {
    const latest = await saver.reload("/api/staff/settings");
    if (latest.status === "loaded") {
      onDraft(null);
      onSaved(latest.data);
    } else {
      toast.error("Settings could not be checked. Your entries and recovery state are kept.");
    }
  }

  return { saver, current, errors, changed, uncertain, disabled, field, discard, submit, reload };
}
