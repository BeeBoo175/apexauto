"use client";

import { useLayoutEffect, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import {
  parseRetentionSettings,
  readRetentionDraft,
  retentionDraftFrom,
  retentionShortened,
  retentionUnchanged,
  type RetentionDraft,
  type RetentionSettings,
} from "@/lib/settings/retention";
import { useEditGuard } from "./use-edit-guard";
import { useSettingsSave } from "./use-settings-save";

const retentionPath = "/api/staff/settings/retention";

export function useRetentionSettingsForm({
  settings,
  draft,
  busy,
  onDraft,
  onSaved,
  onLocked,
}: {
  settings: RetentionSettings;
  draft: RetentionDraft | null;
  busy: boolean;
  onDraft: (draft: RetentionDraft | null) => void;
  onSaved: (settings: RetentionSettings) => void;
  onLocked: (locked: boolean) => void;
}) {
  const toast = useToast();
  const saver = useSettingsSave(parseRetentionSettings);
  const [shorteningConfirmed, setShorteningConfirmed] = useState(false);
  const current = draft ?? retentionDraftFrom(settings);
  const { values, errors } = readRetentionDraft(current, settings.bounds);
  const changed = values !== null && !retentionUnchanged(values, settings);
  const shortened = values !== null && retentionShortened(values, settings);
  const uncertain = Boolean(saver.notice?.uncertain);
  const disabled = busy || saver.busy || uncertain;
  const dirty = Boolean(draft) && (!values || changed);
  const locked = dirty || saver.busy || uncertain;
  useEditGuard({
    dirty,
    pending: saver.busy,
    uncertain,
    label: "Retention settings",
    discard,
  });
  useLayoutEffect(() => {
    onLocked(locked);
    return () => onLocked(false);
  }, [locked, onLocked]);

  function field(key: keyof RetentionDraft, value: string) {
    saver.clearNotice();
    // A confirmation covers one reviewed set of values, never the next edit.
    setShorteningConfirmed(false);
    onDraft({ ...current, [key]: value });
  }

  function discard() {
    saver.clearNotice();
    setShorteningConfirmed(false);
    onDraft(null);
  }

  async function submit() {
    if (!values || !changed || (shortened && !shorteningConfirmed)) {
      return;
    }
    const saved = await saver.save(retentionPath, "PUT", {
      ...values,
      expectedVersion: settings.version,
      shorteningConfirmed: shortened,
    });
    if (saved) {
      setShorteningConfirmed(false);
      onDraft(null);
      onSaved(saved);
      toast.success("Retention settings saved.");
    }
  }

  async function reload() {
    const latest = await saver.reload(retentionPath);
    if (latest.status === "loaded") {
      setShorteningConfirmed(false);
      onDraft(null);
      onSaved(latest.data);
    } else {
      toast.error(
        "Retention settings could not be checked. Your entries and recovery state are kept.",
      );
    }
  }

  return {
    saver,
    current,
    errors,
    changed,
    shortened,
    shorteningConfirmed,
    confirmShortening: setShorteningConfirmed,
    uncertain,
    disabled,
    field,
    discard,
    submit,
    reload,
  };
}
