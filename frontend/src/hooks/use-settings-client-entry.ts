"use client";

import type { SettingsWorkspace } from "@/components/settings/SettingsWorkspace";
import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import { unavailableResult, type ApiResult, type Page } from "@/lib/api";
import { isUuid } from "@/lib/auth/contracts";
import type { SearchParams } from "@/lib/query";
import {
  listParams,
  parsePromo,
  parsePromos,
  parseSettings,
  parseWarranties,
  parseWarranty,
  recordPath,
  sectionPath,
  settingsSection,
  type ListFilters,
  type Promo,
  type PurchaseSettings,
  type SettingsSection,
  type Warranty,
} from "@/lib/settings/settings";

export interface SettingsEntry {
  Workspace: typeof SettingsWorkspace;
  section: SettingsSection;
  filters: ListFilters;
  page: number;
  settings: ApiResult<PurchaseSettings> | null;
  promos: ApiResult<Page<Promo>> | null;
  warranties: ApiResult<Page<Warranty>> | null;
  // A record named in the URL is read with the section so its drawer opens populated on direct entry.
  promoId: string;
  warrantyId: string;
  selectedPromo: ApiResult<Promo> | null;
  selectedWarranty: ApiResult<Warranty> | null;
}

// A named record opens only when the confirmed read is exactly that record.
function confirmed<T extends { id: string }>(
  id: string,
  result: ApiResult<T> | null,
): ApiResult<T> | null {
  if (!id) {
    return null;
  }
  return result?.data?.id === id ? result : unavailableResult();
}

export function useSettingsClientEntry(parameters: SearchParams) {
  return useStaffClientEntry<SettingsEntry>(async (signal) => {
    // Only the section being opened is read; the others load when the administrator selects them.
    const section = settingsSection(parameters);
    const { filters, page } = listParams(parameters, section);
    const path = sectionPath(section, filters, page);
    const promoId = section === "promos" && isUuid(parameters.promo) ? parameters.promo : "";
    const warrantyId =
      section === "warranties" && isUuid(parameters.warranty) ? parameters.warranty : "";
    const [
      settings,
      promos,
      warranties,
      selectedPromo,
      selectedWarranty,
      { SettingsWorkspace: Workspace },
    ] = await Promise.all([
      section === "settings" ? privateApiFetch(path, parseSettings, { signal }) : null,
      section === "promos" ? privateApiFetch(path, parsePromos, { signal }) : null,
      section === "warranties" ? privateApiFetch(path, parseWarranties, { signal }) : null,
      promoId
        ? privateApiFetch(recordPath("promos", promoId), parsePromo, { signal }).catch(() => null)
        : null,
      warrantyId
        ? privateApiFetch(recordPath("warranties", warrantyId), parseWarranty, { signal }).catch(
            () => null,
          )
        : null,
      import("@/components/settings/SettingsWorkspace"),
    ]);
    const list = promos?.data ?? warranties?.data ?? null;
    if (
      (section === "settings" && !settings?.data) ||
      (section !== "settings" &&
        (!list || list.page !== page || list.pageSize !== filters.pageSize))
    ) {
      throw new Error("Settings unavailable");
    }
    return {
      Workspace,
      section,
      filters,
      page,
      settings,
      promos,
      warranties,
      promoId,
      warrantyId,
      selectedPromo: confirmed(promoId, selectedPromo),
      selectedWarranty: confirmed(warrantyId, selectedWarranty),
    };
  }, "Settings could not be loaded. Retry to open the workspace.");
}
