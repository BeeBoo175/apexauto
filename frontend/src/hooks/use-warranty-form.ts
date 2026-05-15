"use client";

import { useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { parseMyr } from "@/lib/currency-input";
import { recordPath, type Warranty, type WarrantyGrade } from "@/lib/settings/settings";
import { useEditGuard } from "./use-edit-guard";
import type { useSettingsSave } from "./use-settings-save";

const whole = (value: string) => (/^\d+$/.test(value.trim()) ? Number(value.trim()) : null);

// The draft is based on the record as it was opened; a newer read never replaces it silently.
export function useWarrantyForm({
  base,
  latest,
  saver,
  onSaved,
  onReload,
  onCancel,
}: {
  base: Warranty | null;
  latest: Warranty | null;
  saver: ReturnType<typeof useSettingsSave<Warranty>>;
  onSaved: () => void;
  onReload: () => void;
  onCancel: () => void;
}) {
  const toast = useToast();
  // One identity per open create drawer lets a lost response be retried without a duplicate.
  const [id] = useState(() => base?.id ?? crypto.randomUUID());
  const [name, setName] = useState(base?.name ?? "");
  const [months, setMonths] = useState(base ? String(base.durationMonths) : "");
  const [kilometres, setKilometres] = useState(base ? String(base.mileageCapKm) : "");
  const [price, setPrice] = useState(base ? base.price.toFixed(2) : "");
  const [grade, setGrade] = useState<WarrantyGrade>(base?.minimumGrade ?? "C");
  const [isActive, setIsActive] = useState(base?.isActive ?? true);
  const durationMonths = whole(months);
  const mileageCapKm = whole(kilometres);
  const amount = parseMyr(price.trim());
  const valid =
    Boolean(name.trim()) &&
    durationMonths !== null &&
    durationMonths > 0 &&
    mileageCapKm !== null &&
    mileageCapKm > 0 &&
    amount !== null;
  const uncertain = Boolean(saver.notice?.uncertain);
  const newer = Boolean(base && latest && latest.version !== base.version);
  // Like the other editors, an existing record can be saved only once something differs from it.
  const changed =
    !base ||
    name.trim() !== base.name ||
    durationMonths !== base.durationMonths ||
    mileageCapKm !== base.mileageCapKm ||
    amount !== base.price ||
    grade !== base.minimumGrade ||
    isActive !== base.isActive;
  const dirty = base
    ? changed
    : Boolean(name || months || kilometres || price || grade !== "C" || !isActive);
  useEditGuard({
    dirty,
    pending: saver.busy,
    uncertain,
    label: "Warranty package",
    discard: onCancel,
  });

  async function submit() {
    if (!valid || durationMonths === null || mileageCapKm === null || amount === null) {
      return;
    }
    const terms = {
      name: name.trim(),
      durationMonths,
      mileageCapKm,
      price: amount,
      minimumGrade: grade,
      isActive,
    };
    const saved = base
      ? await saver.save(recordPath("warranties", base.id), "PUT", {
          ...terms,
          expectedVersion: base.version,
        })
      : await saver.save("/api/staff/settings/warranties", "POST", { id, ...terms });
    if (saved) {
      toast.success(base ? "Warranty package saved." : "Warranty package created.");
      onSaved();
    }
  }

  async function reload() {
    const result = await saver.reload(recordPath("warranties", id));
    if (result.status === "loaded") {
      if (base) {
        onReload();
      } else {
        onSaved();
      }
    } else if (result.status === "missing") {
      toast.info("This package was not created. Review your entries or discard them.");
    } else {
      toast.error("The package could not be checked. Your entries and recovery state are kept.");
    }
  }

  return {
    name,
    months,
    kilometres,
    price,
    grade,
    isActive,
    amount,
    valid,
    uncertain,
    newer,
    changed,
    dirty,
    setName,
    setMonths,
    setKilometres,
    setPrice,
    setGrade,
    setIsActive,
    submit,
    reload,
  };
}
