"use client";

import { useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { parseMyr } from "@/lib/currency-input";
import { malaysiaInput, utcFromMalaysiaInput } from "@/lib/date-format";
import { recordPath, type Promo } from "@/lib/settings/settings";
import { useEditGuard } from "./use-edit-guard";
import type { useSettingsSave } from "./use-settings-save";

// The draft is based on the record as it was opened; a newer read never replaces it silently.
export function usePromoForm({
  base,
  latest,
  saver,
  onSaved,
  onReload,
  onCancel,
}: {
  base: Promo | null;
  latest: Promo | null;
  saver: ReturnType<typeof useSettingsSave<Promo>>;
  onSaved: () => void;
  onReload: () => void;
  onCancel: () => void;
}) {
  const toast = useToast();
  // One identity per open create drawer lets a lost response be retried without a duplicate.
  const [id] = useState(() => base?.id ?? crypto.randomUUID());
  const [code, setCode] = useState(base?.code ?? "");
  const [discount, setDiscount] = useState(base ? base.discount.toFixed(2) : "");
  const [expiry, setExpiry] = useState(base ? malaysiaInput(base.expiresAtUtc) : "");
  const [isActive, setIsActive] = useState(base?.isActive ?? true);
  const amount = parseMyr(discount.trim());
  const expiresAtUtc = utcFromMalaysiaInput(expiry);
  const valid = Boolean(code.trim()) && amount !== null && amount > 0 && expiresAtUtc !== null;
  const uncertain = Boolean(saver.notice?.uncertain);
  const newer = Boolean(base && latest && latest.version !== base.version);
  // Like the other editors, an existing record can be saved only once something differs from it.
  const changed =
    !base ||
    amount !== base.discount ||
    expiresAtUtc !== new Date(base.expiresAtUtc).toISOString() ||
    isActive !== base.isActive;
  const dirty = base ? changed : Boolean(code || discount || expiry || !isActive);
  useEditGuard({
    dirty,
    pending: saver.busy,
    uncertain,
    label: "Promo",
    discard: onCancel,
  });

  async function submit() {
    if (!valid || amount === null || expiresAtUtc === null) {
      return;
    }
    const saved = base
      ? await saver.save(recordPath("promos", base.id), "PUT", {
          discount: amount,
          expiresAtUtc,
          isActive,
          expectedVersion: base.version,
        })
      : await saver.save("/api/staff/settings/promos", "POST", {
          id,
          code: code.trim(),
          discount: amount,
          expiresAtUtc,
          isActive,
        });
    if (saved) {
      toast.success(base ? "Promo saved." : "Promo created.");
      onSaved();
    }
  }

  async function reload() {
    const result = await saver.reload(recordPath("promos", id));
    if (result.status === "loaded") {
      if (base) {
        onReload();
      } else {
        onSaved();
      }
    } else if (result.status === "missing") {
      toast.info("This promo was not created. Review your entries or discard them.");
    } else {
      toast.error("The promo could not be checked. Your entries and recovery state are kept.");
    }
  }

  return {
    code,
    discount,
    expiry,
    isActive,
    amount,
    valid,
    uncertain,
    newer,
    changed,
    dirty,
    setCode,
    setDiscount,
    setExpiry,
    setIsActive,
    submit,
    reload,
  };
}
