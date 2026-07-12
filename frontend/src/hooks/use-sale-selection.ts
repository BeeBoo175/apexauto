"use client";

import { useState } from "react";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { parseSale } from "@/lib/sales/parse";
import { salePath } from "@/lib/sales/query";
import type { Sale } from "@/lib/sales/contracts";
import { isSaleVersionSection, type SaleSection } from "@/lib/sales/sale-sections";
import { useStaffSelection } from "./use-staff-selection";
import { useGuardedAction } from "./use-guarded-action";
import { writeAddress } from "@/lib/browser-url";

// The selected sale, its section and any past version, all kept in the address as the workshop keeps its job.
export function useSaleSelection(
  id: string,
  initial: ApiResult<Sale> | null,
  initialSection: SaleSection = "deal",
  initialVersion: number | null = null,
) {
  const interaction = useGuardedAction();
  const selection = useStaffSelection(id, initial, {
    parameter: "sale",
    path: salePath,
    parse: parseSale,
    matches: (value, saleId) => value.id === saleId,
    read: privateApiFetch,
    commitAfterRead: true,
  });
  const [section, setSection] = useState(initialSection);
  const [versions, setVersions] = useState<Partial<Record<SaleSection, number | null>>>({
    [initialSection]: isSaleVersionSection(initialSection) ? initialVersion : null,
  });
  const version = isSaleVersionSection(section) ? (versions[section] ?? null) : null;
  // Unsaved terms or an unconfirmed command keep this sale open until they are saved, discarded or reconciled.
  const [locked, setLocked] = useState(false);

  function address(apply: (url: URL) => void) {
    const url = new URL(window.location.href);
    apply(url);
    writeAddress(url);
  }

  // Choosing another sale opens it on the Deal section; reading the same sale again keeps where it was.
  function choose(nextId: string) {
    const chooseSale = () =>
      void selection.choose(nextId, (url) => {
        if (nextId === selection.id) {
          return;
        }
        setSection("deal");
        setVersions({});
        url.searchParams.set("section", "deal");
        url.searchParams.delete("version");
      });
    if (nextId === selection.id) {
      chooseSale();
      return;
    }
    chooseSale();
  }

  // Each section retains its selected record, just as Workshop retains a selected repair across tabs.
  function changeSection(next: SaleSection) {
    setSection(next);
    address((url) => {
      url.searchParams.set("section", next);
      const retained = isSaleVersionSection(next) ? versions[next] : null;
      if (retained == null) {
        url.searchParams.delete("version");
      } else {
        url.searchParams.set("version", String(retained));
      }
    });
  }

  function openVersion(next: number | null, nextSection: SaleSection = section) {
    if (!isSaleVersionSection(nextSection)) {
      return;
    }
    setSection(nextSection);
    setVersions((current) => ({ ...current, [nextSection]: next }));
    address((url) => {
      url.searchParams.set("section", nextSection);
      if (next === null) {
        url.searchParams.delete("version");
      } else {
        url.searchParams.set("version", String(next));
      }
    });
  }

  function close() {
    interaction.request(() => {
      selection.close();
      setVersions({});
      address((url) => {
        url.searchParams.delete("section");
        url.searchParams.delete("version");
      });
    }, "Discard and close");
  }

  return {
    ...selection,
    choose,
    close,
    section,
    changeSection,
    version,
    openVersion,
    locked,
    lock: setLocked,
  };
}
