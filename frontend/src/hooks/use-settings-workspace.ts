"use client";

import { useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { Page } from "@/lib/api";
import {
  emptyListFilters,
  parsePromos,
  parseSettings,
  parseWarranties,
  sectionPath,
  type ListFilters,
  type Promo,
  type PurchaseSettings,
  type SettingsSection,
  type Warranty,
} from "@/lib/settings/settings";
import { parseRetentionSettings, type RetentionSettings } from "@/lib/settings/retention";
import { useBoundedRead } from "./use-bounded-read";
import type { SettingsEntry } from "./use-settings-client-entry";

export interface ListSnapshot<T> {
  filters: ListFilters;
  page: number;
  data: Page<T>;
}

function snapshot<T>(
  entry: SettingsEntry,
  data: Page<T> | null | undefined,
): ListSnapshot<T> | null {
  return data ? { filters: entry.filters, page: entry.page, data } : null;
}

export function useSettingsWorkspace(entry: SettingsEntry) {
  const [section, setSection] = useState(entry.section);
  const [settings, setSettings] = useState<PurchaseSettings | null>(entry.settings?.data ?? null);
  // The settings record is created by provisioning, so a confirmed 404 is a state of its own, not a failed read.
  const [settingsMissing, setSettingsMissing] = useState(entry.settings?.status === 404);
  const [retention, setRetention] = useState<RetentionSettings | null>(
    entry.retention?.data ?? null,
  );
  // Like the purchase settings, the retention record comes from provisioning, so a confirmed 404 is its own state.
  const [retentionMissing, setRetentionMissing] = useState(entry.retention?.status === 404);
  const [promos, setPromos] = useState(snapshot(entry, entry.promos?.data));
  const [warranties, setWarranties] = useState(snapshot(entry, entry.warranties?.data));
  const { busy, ready, begin, isCurrent, end, cancel } = useBoundedRead();
  const toast = useToast();

  // Reads one section; every failure keeps the last confirmed content and filters on screen. A confirmed read
  // returns the location it shows so the caller can write the address.
  async function open(
    next: SettingsSection = section,
    filters: ListFilters | null = null,
    page: number | null = null,
    supersede = false,
  ) {
    let current: ListSnapshot<Promo | Warranty> | null = null;
    if (next === "promos") {
      current = promos;
    } else if (next === "warranties") {
      current = warranties;
    }
    const nextFilters = filters ?? current?.filters ?? emptyListFilters;
    const nextPage = page ?? current?.page ?? 1;
    const attempt = begin(supersede);
    if (!attempt) {
      return null;
    }
    try {
      const path = sectionPath(next, nextFilters, nextPage);
      const options = { signal: attempt.signal };
      if (next === "settings") {
        const result = await privateApiFetch(path, parseSettings, options);
        if (!isCurrent(attempt)) {
          return false;
        }
        if (result.status === 404) {
          setSettings(null);
          setSettingsMissing(true);
        } else if (result.status !== 200 || !result.data) {
          throw new Error("Settings unavailable");
        } else {
          setSettings(result.data);
          setSettingsMissing(false);
        }
      } else if (next === "retention") {
        const result = await privateApiFetch(path, parseRetentionSettings, options);
        if (!isCurrent(attempt)) {
          return null;
        }
        if (result.status === 404) {
          setRetention(null);
          setRetentionMissing(true);
        } else if (result.status !== 200 || !result.data) {
          throw new Error("Settings unavailable");
        } else {
          setRetention(result.data);
          setRetentionMissing(false);
        }
      } else {
        const result =
          next === "promos"
            ? await privateApiFetch(path, parsePromos, options)
            : await privateApiFetch(path, parseWarranties, options);
        if (!isCurrent(attempt)) {
          return null;
        }
        const data = result.data;
        if (
          result.status !== 200 ||
          !data ||
          data.page !== nextPage ||
          data.pageSize !== nextFilters.pageSize
        ) {
          throw new Error("Settings unavailable");
        }
        const confirmed = { filters: nextFilters, page: nextPage };
        if (next === "promos") {
          setPromos({ ...confirmed, data: data as Page<Promo> });
        } else {
          setWarranties({ ...confirmed, data: data as Page<Warranty> });
        }
      }
      setSection(next);
      return { section: next, filters: nextFilters, page: nextPage };
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Settings could not be refreshed. The last confirmed content remains shown.");
      }
      return null;
    } finally {
      end(attempt);
    }
  }

  return {
    section,
    settings,
    settingsMissing,
    setSettings,
    retention,
    retentionMissing,
    setRetention,
    promos,
    warranties,
    busy,
    ready,
    open,
    cancel,
  };
}
