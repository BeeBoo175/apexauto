"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { Page } from "@/lib/api";
import {
  emptyListFilters,
  listQuery,
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

const selectionParameter = { promos: "promo", warranties: "warranty" } as const;

function locationQuery(section: SettingsSection, filters: ListFilters, page: number) {
  const query = new URLSearchParams(section === "settings" ? "" : listQuery(filters, page));
  query.set("section", section);
  // An open record drawer stays addressable while its list refreshes.
  if (section !== "settings") {
    const parameter = selectionParameter[section];
    const selected = new URL(window.location.href).searchParams.get(parameter);
    if (selected) {
      query.set(parameter, selected);
    }
  }
  return query.toString();
}

export function useSettingsWorkspace(entry: SettingsEntry) {
  const [section, setSection] = useState(entry.section);
  const [settings, setSettings] = useState<PurchaseSettings | null>(entry.settings?.data ?? null);
  const [promos, setPromos] = useState(snapshot(entry, entry.promos?.data));
  const [warranties, setWarranties] = useState(snapshot(entry, entry.warranties?.data));
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();

  useEffect(() => {
    queueMicrotask(() => setReady(true));
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, []);

  // Reads one section; every failure keeps the last confirmed content and filters on screen.
  async function open(
    next: SettingsSection = section,
    filters: ListFilters | null = null,
    page: number | null = null,
  ) {
    if (controller.current) {
      return false;
    }
    let current: ListSnapshot<Promo | Warranty> | null = null;
    if (next === "promos") {
      current = promos;
    } else if (next === "warranties") {
      current = warranties;
    }
    const nextFilters = filters ?? current?.filters ?? emptyListFilters;
    const nextPage = page ?? current?.page ?? 1;
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const path = sectionPath(next, nextFilters, nextPage);
      const options = { signal: attempt.signal };
      if (next === "settings") {
        const result = await privateApiFetch(path, parseSettings, options);
        if (attempt.signal.aborted || controller.current !== attempt) {
          return false;
        }
        if (result.status !== 200 || !result.data) {
          throw new Error("Settings unavailable");
        }
        setSettings(result.data);
      } else {
        const result =
          next === "promos"
            ? await privateApiFetch(path, parsePromos, options)
            : await privateApiFetch(path, parseWarranties, options);
        if (attempt.signal.aborted || controller.current !== attempt) {
          return false;
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
      const url = new URL(window.location.href);
      url.search = locationQuery(next, nextFilters, nextPage);
      window.history.replaceState(null, "", url);
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Settings could not be refreshed. The last confirmed content remains shown.");
      }
      return false;
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  return { section, settings, setSettings, promos, warranties, busy, ready, open };
}
