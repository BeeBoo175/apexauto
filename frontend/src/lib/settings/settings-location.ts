import { isUuid } from "@/lib/auth/contracts";
import {
  emptyListFilters,
  listParams,
  listUrlQuery,
  type ListFilters,
  type SettingsSection,
} from "./settings";

export const settingsWorkspace = "/dashboard/settings";

const sectionSegments = {
  promos: "promos",
  warranties: "warranties",
} as const;

export type SettingsSelectionPath = { section: SettingsSection; id: string };

export type SettingsState = SettingsSelectionPath & { filters: ListFilters; page: number };

// Shapes: /dashboard/settings, /dashboard/settings/promos[/<id>] and /dashboard/settings/warranties[/<id>].
export function settingsSelectionPath(pathname: string): SettingsSelectionPath | null {
  if (pathname === settingsWorkspace) {
    return { section: "settings", id: "" };
  }
  if (!pathname.startsWith(`${settingsWorkspace}/`)) {
    return null;
  }
  const [segment, id = "", ...extra] = pathname.slice(settingsWorkspace.length + 1).split("/");
  const section = Object.entries(sectionSegments).find(([, value]) => value === segment)?.[0];
  if (!section || extra.length > 0 || (id !== "" && !isUuid(id))) {
    return null;
  }
  return { section: section as SettingsSection, id };
}

// Route parameters of the nested pages: `/<section>[/<id>]`.
export function settingsSelectionRoute(section: string, id: string[] = []) {
  const path = settingsSelectionPath(`${settingsWorkspace}/${[section, ...id].join("/")}`);
  return path && path.section !== "settings" ? path : null;
}

export function settingsSelectionHref({ section, id }: SettingsSelectionPath) {
  if (section === "settings") {
    return settingsWorkspace;
  }
  const base = `${settingsWorkspace}/${sectionSegments[section]}`;
  return id ? `${base}/${id}` : base;
}

export function settingsHref(state: SettingsState) {
  const path = settingsSelectionHref(state);
  if (state.section === "settings") {
    return path;
  }
  const query = listUrlQuery(state.filters, state.page);
  return query.size ? `${path}?${query}` : path;
}

export function settingsLocation(pathname: string, search: string) {
  const selection = settingsSelectionPath(pathname);
  if (!selection) {
    return null;
  }
  const { filters, page } =
    selection.section === "settings"
      ? { filters: emptyListFilters, page: 1 }
      : listParams(Object.fromEntries(new URLSearchParams(search)), selection.section);
  const state: SettingsState = { ...selection, filters, page };
  return { ...state, href: settingsHref(state) };
}

// The section and query that identify the list shown beside the selection.
export function settingsListKey(state: SettingsState) {
  return settingsHref({ ...state, id: "" });
}
