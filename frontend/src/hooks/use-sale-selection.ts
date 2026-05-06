"use client";

import { useState } from "react";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { parseSale, salePath, type Sale } from "@/lib/sales/sale";
import type { SaleSection } from "@/lib/sales/sale-sections";
import { useStaffSelection } from "./use-staff-selection";

// The selected sale, its section and any past version, all kept in the address as the workshop keeps its job.
export function useSaleSelection(
  id: string,
  initial: ApiResult<Sale> | null,
  initialSection: SaleSection = "deal",
  initialVersion: number | null = null,
) {
  const selection = useStaffSelection(id, initial, {
    parameter: "sale",
    path: salePath,
    parse: parseSale,
    matches: (value, saleId) => value.id === saleId,
    read: privateApiFetch,
    commitAfterRead: true,
  });
  const [section, setSection] = useState(initialSection);
  const [version, setVersion] = useState(initialVersion);
  // Unsaved terms or an unconfirmed command keep this sale open until they are saved, discarded or reconciled.
  const [locked, setLocked] = useState(false);

  function address(apply: (url: URL) => void) {
    const url = new URL(window.location.href);
    apply(url);
    window.history.replaceState(null, "", url);
  }

  // Choosing another sale opens it on the Deal section; reading the same sale again keeps where it was.
  function choose(nextId: string) {
    return selection.choose(nextId, (url) => {
      if (nextId === selection.id) {
        return;
      }
      setSection("deal");
      setVersion(null);
      url.searchParams.set("section", "deal");
      url.searchParams.delete("version");
    });
  }

  // Changing section returns to the current sale; a past version belongs to the section it was opened in.
  function changeSection(next: SaleSection) {
    setSection(next);
    setVersion(null);
    address((url) => {
      url.searchParams.set("section", next);
      url.searchParams.delete("version");
    });
  }

  function openVersion(next: number | null, nextSection: SaleSection = section) {
    setSection(nextSection);
    setVersion(next);
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
    selection.close();
    address((url) => {
      url.searchParams.delete("section");
      url.searchParams.delete("version");
    });
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
