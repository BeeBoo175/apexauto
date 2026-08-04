"use client";

import { useRef, useState } from "react";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { parseSale } from "@/lib/sales/parse";
import { salePath } from "@/lib/sales/query";
import type { Sale } from "@/lib/sales/contracts";
import { isSaleVersionSection, type SaleSection } from "@/lib/sales/sale-sections";
import type { SaleSelectionPath } from "@/lib/sales/sales-location";
import { useStaffSelection } from "./use-staff-selection";
import { useGuardedAction } from "./use-guarded-action";

export type SaleLocationHandler = (
  selection: SaleSelectionPath,
  mode: false | "push" | "replace",
) => void;

type Versions = Partial<Record<SaleSection, number | null>>;

// The selected sale, its section and any past version. The path names them; each versioned section retains its
// own selected version while the sale stays open, as Workshop retains a selected repair across tabs.
export function useSaleSelection(
  id: string,
  initial: ApiResult<Sale> | null,
  onLocation: SaleLocationHandler,
  initialSection: SaleSection = "deal",
  initialVersion: number | null = null,
) {
  const interaction = useGuardedAction();
  const [section, setSection] = useState(initialSection);
  const [versions, setVersions] = useState<Versions>({
    [initialSection]: isSaleVersionSection(initialSection) ? initialVersion : null,
  });
  // The destination of the read in flight; it becomes the shown section only once the read is confirmed.
  const destination = useRef({ section: initialSection, version: initialVersion });
  // Unsaved terms or an unconfirmed command keep this sale open until they are saved, discarded or reconciled.
  const [locked, setLocked] = useState(false);
  const selection = useStaffSelection(id, initial, {
    parameter: "sale",
    path: salePath,
    parse: parseSale,
    matches: (value, saleId) => value.id === saleId,
    read: privateApiFetch,
    commitAfterRead: true,
    address: (confirmedId, updateAddress = true) => {
      const target = confirmedId
        ? destination.current
        : { section: "deal" as const, version: null };
      onLocation({ id: confirmedId, ...target }, updateAddress && "push");
    },
  });
  const version = isSaleVersionSection(section) ? (versions[section] ?? null) : null;

  function show(target: { section: SaleSection; version: number | null }) {
    destination.current = target;
    setSection(target.section);
    if (isSaleVersionSection(target.section)) {
      const retained = target.section;
      setVersions((current) => ({ ...current, [retained]: target.version }));
    }
  }

  // Choosing another sale opens it on the Deal section; reading the same sale again keeps where it was.
  function choose(nextId: string) {
    if (nextId === selection.id) {
      return selection.choose(nextId);
    }
    destination.current = { section: "deal", version: null };
    return selection.choose(nextId, () => {
      setVersions({});
      show(destination.current);
    });
  }

  // Applies a browser Back/Forward or refresh destination without writing the address.
  function visit(target: SaleSelectionPath, updateAddress = false) {
    destination.current = { section: target.section, version: target.version };
    if (target.id === selection.id && selection.result?.data) {
      show(destination.current);
      return Promise.resolve(selection.result.data);
    }
    return selection.load(
      target.id,
      () => {
        setVersions({});
        show(destination.current);
      },
      updateAddress,
    );
  }

  function changeSection(next: SaleSection) {
    const retained = isSaleVersionSection(next) ? (versions[next] ?? null) : null;
    show({ section: next, version: retained });
    onLocation({ id: selection.id, section: next, version: retained }, "push");
  }

  function openVersion(next: number | null, nextSection: SaleSection = section) {
    if (!isSaleVersionSection(nextSection)) {
      return;
    }
    show({ section: nextSection, version: next });
    onLocation({ id: selection.id, section: nextSection, version: next }, "push");
  }

  function close() {
    interaction.request(() => closeConfirmed(), "Discard and close");
  }

  function closeConfirmed(updateAddress = true) {
    selection.close(updateAddress);
    setVersions({});
    show({ section: "deal", version: null });
  }

  return {
    ...selection,
    choose,
    visit,
    close,
    closeConfirmed,
    section,
    changeSection,
    version,
    openVersion,
    locked,
    lock: setLocked,
  };
}
