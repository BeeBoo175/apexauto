"use client";

import { privateApiFetch } from "@/lib/api-browser";
import {
  emptyListFilters,
  isRecordSection,
  parsePromo,
  parseWarranty,
  recordPath,
  type ListFilters,
  type ListSection,
  type SettingsSection,
} from "@/lib/settings/settings";
import {
  settingsHref,
  settingsListKey,
  settingsLocation,
  settingsWorkspace,
  type SettingsState,
} from "@/lib/settings/settings-location";
import type { SettingsEntry } from "./use-settings-client-entry";
import type { useSettingsWorkspace } from "./use-settings-workspace";
import { useSelectedRecord, useStaffAddress, type AddressMode } from "./use-staff-address";
import { useStaffSelection } from "./use-staff-selection";

function selectedRecordId(entry: SettingsEntry) {
  if (entry.section === "promos") {
    return entry.promoId;
  }
  return entry.section === "warranties" ? entry.warrantyId : "";
}

// Settings address: the path selects the section and the record, the query holds the list that section shows.
// A section switch or an applied list change pushes; a refresh or a post-write read replaces. Back and Forward
// read the requested destination, and a failed read keeps the requested address with the confirmed content.
export function useSettingsAddress(
  entry: SettingsEntry,
  workspace: ReturnType<typeof useSettingsWorkspace>,
) {
  const initial: SettingsState = {
    section: entry.section,
    id: selectedRecordId(entry),
    filters: entry.filters,
    page: entry.page,
  };
  type Target = NonNullable<ReturnType<typeof settingsLocation>>;
  const address = useStaffAddress<SettingsState, Target>({
    workspace: settingsWorkspace,
    initial,
    href: settingsHref,
    locate: settingsLocation,
    synchronize,
  });

  const promoDrawer = useStaffSelection(entry.promoId, entry.selectedPromo, {
    parameter: "promo",
    path: (id) => recordPath("promos", id),
    parse: parsePromo,
    matches: (value, id) => value.id === id,
    read: privateApiFetch,
    commitAfterRead: true,
    address: (id, updateAddress) => selectRecord("promos", id, updateAddress),
  });
  const warrantyDrawer = useStaffSelection(entry.warrantyId, entry.selectedWarranty, {
    parameter: "warranty",
    path: (id) => recordPath("warranties", id),
    parse: parseWarranty,
    matches: (value, id) => value.id === id,
    read: privateApiFetch,
    commitAfterRead: true,
    address: (id, updateAddress) => selectRecord("warranties", id, updateAddress),
  });
  const drawers = { promos: promoDrawer, warranties: warrantyDrawer };
  const { section } = workspace;
  useSelectedRecord(isRecordSection(section) ? "" : drawers[section].id);

  function shown() {
    const snapshots = {
      settings: null,
      retention: null,
      promos: workspace.promos,
      warranties: workspace.warranties,
    };
    const snapshot = snapshots[section];
    return { section, filters: snapshot?.filters ?? emptyListFilters, page: snapshot?.page ?? 1 };
  }

  function selectRecord(target: ListSection, id: string, updateAddress = true) {
    address.update({ section: target, id }, updateAddress ? "push" : false);
  }

  // Reads one section and writes its address once the read is confirmed; the record open in another section closes.
  async function load(
    target: SettingsSection,
    filters: ListFilters | null,
    page: number | null,
    mode: AddressMode,
    supersede = false,
  ) {
    const previous = section;
    const confirmed = await workspace.open(target, filters, page, supersede);
    if (!confirmed) {
      return false;
    }
    if (!isRecordSection(previous) && previous !== confirmed.section) {
      drawers[previous].close(false);
    }
    const id = isRecordSection(confirmed.section) ? "" : drawers[confirmed.section].id;
    address.update({ ...confirmed, id }, mode);
    return true;
  }

  function synchronize(target: Target, previous: Target | null) {
    const sameList = previous !== null && settingsListKey(previous) === settingsListKey(target);
    if (!sameList) {
      workspace.cancel();
      void load(target.section, target.filters, target.page, false, true);
    }
    for (const [name, drawer] of Object.entries(drawers)) {
      if (name !== target.section) {
        drawer.close(false);
      } else if (target.id !== drawer.id) {
        if (target.id) {
          void drawer.load(target.id, undefined, false);
        } else {
          drawer.close(false);
        }
      }
    }
    address.update({ section: target.section, id: target.id }, false);
  }

  // Refresh follows the address: after a failed Back/Forward it reads the requested destination.
  function refresh() {
    const target = address.destination();
    const current = shown();
    const showing = settingsHref({ ...current, id: "" });
    if (!target || settingsListKey(target) === showing) {
      return load(current.section, current.filters, current.page, "replace");
    }
    address.attempted(target);
    return load(target.section, target.filters, target.page, false, true);
  }

  return {
    promoDrawer,
    warrantyDrawer,
    switchSection: (next: SettingsSection) => load(next, null, null, "push"),
    loadList: (filters: ListFilters, page: number) => load(section, filters, page, "push"),
    reload: () => load(section, null, null, "replace"),
    refresh,
  };
}
