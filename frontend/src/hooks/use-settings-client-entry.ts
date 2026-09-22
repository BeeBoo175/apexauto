"use client";

import type { SettingsWorkspace } from "@/components/settings/SettingsWorkspace";
import { useStaffClientEntry } from "./use-staff-client-entry";
import type { SearchParams } from "@/lib/query";
import { privateApiFetch } from "@/lib/api-browser";
import { apiReadError, unavailableResult, type ApiResult, type Page } from "@/lib/api";
import type { SettingsSelectionPath } from "@/lib/settings/settings-location";
import { isRecordSection } from "@/lib/settings/settings";
import { parseRetentionSettings, type RetentionSettings } from "@/lib/settings/retention";
import {
  listParams,
  parsePromo,
  parsePromos,
  parseSettings,
  parseWarranties,
  parseWarranty,
  recordPath,
  sectionPath,
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
  retention: ApiResult<RetentionSettings> | null;
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

export function useSettingsClientEntry(parameters: SearchParams, selection: SettingsSelectionPath) {
  return useStaffClientEntry<SettingsEntry>(async (signal) => {
    // Only the section being opened is read; the others load when the administrator selects them.
    const { section } = selection;
    const { filters, page } = listParams(parameters, section);
    const path = sectionPath(section, filters, page);
    const promoId = section === "promos" ? selection.id : "";
    const warrantyId = section === "warranties" ? selection.id : "";
    const [
      settings,
      retention,
      promos,
      warranties,
      selectedPromo,
      selectedWarranty,
      { SettingsWorkspace: Workspace },
    ] = await Promise.all([
      section === "settings" ? privateApiFetch(path, parseSettings, { signal }) : null,
      section === "retention" ? privateApiFetch(path, parseRetentionSettings, { signal }) : null,
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
    // A missing settings record (404) still opens the workspace, so its Promos and Warranties sections stay reachable
    // and the Settings section can say the record is not set up; any other failed read keeps the retry notice.
    const record = section === "settings" ? settings : retention;
    const recordUnavailable = isRecordSection(section) && !record?.data && record?.status !== 404;
    const listUnavailable =
      !isRecordSection(section) &&
      (!list || list.page !== page || list.pageSize !== filters.pageSize);
    if (recordUnavailable || listUnavailable) {
      throw apiReadError("Settings unavailable", settings, retention, promos, warranties);
    }
    return {
      Workspace,
      section,
      filters,
      page,
      settings,
      retention,
      promos,
      warranties,
      promoId,
      warrantyId,
      selectedPromo: confirmed(promoId, selectedPromo),
      selectedWarranty: confirmed(warrantyId, selectedWarranty),
    };
  });
}
