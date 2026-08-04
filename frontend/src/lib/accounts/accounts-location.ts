import type { SearchParams } from "@/lib/query";
import { staffRecordLocation } from "@/lib/staff/record-location";
import {
  customerAccountParams,
  customerAccountUrlQuery,
  type CustomerAccountFilters,
} from "./customer-accounts";
import {
  staffAccountParams,
  staffAccountUrlQuery,
  type StaffAccountFilters,
} from "./staff-accounts";

export type AccountsView = "staff" | "customers";

export const accountSections = ["details", "history"] as const;

export type AccountSection = (typeof accountSections)[number];

export type AccountSelectionPath = { id: string; section: AccountSection };

const workspaces: Record<AccountsView, string> = {
  staff: "/dashboard/accounts",
  customers: "/dashboard/accounts/customers",
};

export const accountsWorkspace = (view: AccountsView) => workspaces[view];

// Shapes: /dashboard/accounts[/<id>[/history]] for staff and /dashboard/accounts/customers[/<id>[/history]].
// Details is the default section and never appears in the path.
export function accountSelectionPath(view: AccountsView, pathname: string) {
  const record = staffRecordLocation(pathname, workspaces[view]);
  if (!record || record.trail.length > 0) {
    return null;
  }
  const { id, section } = record;
  if (!id) {
    return section === "" ? { id: "", section: "details" as const } : null;
  }
  if (section === "") {
    return { id, section: "details" as const };
  }
  return section === "history" ? { id, section: "history" as const } : null;
}

export function accountSelectionRoute(view: AccountsView, id: string, section: string[] = []) {
  const path = accountSelectionPath(view, `${workspaces[view]}/${[id, ...section].join("/")}`);
  return path && path.id ? path : null;
}

export function accountSelectionHref(view: AccountsView, { id, section }: AccountSelectionPath) {
  if (!id) {
    return workspaces[view];
  }
  return section === "details"
    ? `${workspaces[view]}/${id}`
    : `${workspaces[view]}/${id}/${section}`;
}

export type AccountsState<Filters> = AccountSelectionPath & { filters: Filters; page: number };

export type AccountsLocationRules<Filters> = {
  view: AccountsView;
  parse: (parameters: SearchParams) => { filters: Filters; page: number };
  urlQuery: (filters: Filters, page: number) => URLSearchParams;
};

export const staffAccountsRules: AccountsLocationRules<StaffAccountFilters> = {
  view: "staff",
  parse: staffAccountParams,
  urlQuery: staffAccountUrlQuery,
};

export const customerAccountsRules: AccountsLocationRules<CustomerAccountFilters> = {
  view: "customers",
  parse: customerAccountParams,
  urlQuery: customerAccountUrlQuery,
};

export function accountsHref<Filters>(
  rules: AccountsLocationRules<Filters>,
  state: AccountsState<Filters>,
) {
  const query = rules.urlQuery(state.filters, state.page);
  const path = accountSelectionHref(rules.view, state);
  return query.size ? `${path}?${query}` : path;
}

export function accountsLocation<Filters>(
  rules: AccountsLocationRules<Filters>,
  pathname: string,
  search: string,
) {
  const selection = accountSelectionPath(rules.view, pathname);
  if (!selection) {
    return null;
  }
  const { filters, page } = rules.parse(Object.fromEntries(new URLSearchParams(search)));
  const state: AccountsState<Filters> = { ...selection, filters, page };
  return { ...state, href: accountsHref(rules, state) };
}

// The query that identifies the table shown beside the selection, so selection-only traversals can be told apart.
export function accountsListKey<Filters>(
  rules: AccountsLocationRules<Filters>,
  state: AccountsState<Filters>,
) {
  return accountsHref(rules, { ...state, id: "", section: "details" });
}
