import type { SearchParams } from "@/lib/query";
import { DEFAULT_PAGE_SIZE, isPageSize } from "@/lib/pagination";
import { staffRecordLocation } from "@/lib/staff/record-location";
import {
  queueViewQueries,
  queueViews,
  tableTextKeys,
  type QueueView,
  type SalesTableFilters,
} from "./contracts";
import { defaultTableFilters, salesQueryToken, tableParams } from "./query";
import { isSaleVersionSection, saleSections, type SaleSection } from "./sale-sections";

export const salesWorkspace = "/dashboard/sales";

export type SaleSelectionPath = {
  id: string;
  section: SaleSection;
  version: number | null;
};

// The agent's list: the open queue tab and, when a search is applied, its results. Page and size belong to
// whichever of the two is shown. An empty view means the server has not yet chosen the opening tab.
export type AgentList = {
  view: QueueView | "";
  search: string;
  page: number;
  pageSize: number;
};

export type SalesState = SaleSelectionPath & {
  filters: SalesTableFilters;
  page: number;
  agent: AgentList;
};

export const idleAgentList: AgentList = {
  view: "",
  search: "",
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
};

const versionPattern = /^[1-9]\d{0,9}$/;

// Shapes: base, /<id> (Deal), /<id>/<section> and /<id>/<customer|deal>/versions/<n>. The default Deal section
// has no segment unless it names a past version.
export function saleSelectionPath(pathname: string): SaleSelectionPath | null {
  const record = staffRecordLocation(pathname, salesWorkspace);
  if (!record) {
    return null;
  }
  const { id, section, trail } = record;
  if (section === "" && trail.length === 0) {
    return { id, section: "deal", version: null };
  }
  const named = saleSections.find((value) => value === section);
  if (!named) {
    return null;
  }
  if (trail.length === 0) {
    return named === "deal" ? null : { id, section: named, version: null };
  }
  const [keyword, version, ...extra] = trail;
  if (
    !isSaleVersionSection(named) ||
    keyword !== "versions" ||
    !versionPattern.test(version ?? "") ||
    extra.length > 0
  ) {
    return null;
  }
  return { id, section: named, version: Number(version) };
}

export function saleSelectionRoute(id: string, section: string[] = []) {
  const path = saleSelectionPath(`${salesWorkspace}/${[id, ...section].join("/")}`);
  return path && path.id ? path : null;
}

export function saleSelectionHref({ id, section, version }: SaleSelectionPath) {
  if (!id) {
    return salesWorkspace;
  }
  if (version !== null) {
    return `${salesWorkspace}/${id}/${section}/versions/${version}`;
  }
  return section === "deal" ? `${salesWorkspace}/${id}` : `${salesWorkspace}/${id}/${section}`;
}

export function tableUrlQuery(filters: SalesTableFilters, page: number) {
  const query = new URLSearchParams();
  if (filters.view) {
    query.set("view", salesQueryToken(filters.view));
  }
  if (page !== 1) {
    query.set("page", String(page));
  }
  if (filters.pageSize !== DEFAULT_PAGE_SIZE) {
    query.set("pageSize", String(filters.pageSize));
  }
  if (filters.sort) {
    query.set("sort", filters.sort);
    query.set("direction", filters.direction);
  }
  for (const key of tableTextKeys) {
    const value = filters[key].trim();
    if (value) {
      query.set(key, key === "outcome" ? salesQueryToken(value) : value);
    }
  }
  return query;
}

function agentUrlQuery({ view, search, page, pageSize }: AgentList) {
  const query = new URLSearchParams();
  if (view) {
    query.set("view", queueViewQueries[view]);
  }
  if (search) {
    query.set("search", search);
  }
  if (page !== 1) {
    query.set("page", String(page));
  }
  if (pageSize !== DEFAULT_PAGE_SIZE) {
    query.set("pageSize", String(pageSize));
  }
  return query;
}

export function salesHref(state: SalesState, manager: boolean) {
  const query = manager ? tableUrlQuery(state.filters, state.page) : agentUrlQuery(state.agent);
  const path = saleSelectionHref(state);
  return query.size ? `${path}?${query}` : path;
}

export function agentList(params: SearchParams): AgentList {
  const page = Number(params.page ?? 1);
  const pageSize = Number(params.pageSize ?? DEFAULT_PAGE_SIZE);
  const search = typeof params.search === "string" ? params.search.trim() : "";
  return {
    view: queueViews.find((view) => queueViewQueries[view] === params.view) ?? "",
    search,
    page: Number.isSafeInteger(page) && page > 0 && page <= 2147483647 ? page : 1,
    pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
  };
}

export function salesLocation(pathname: string, search: string, manager: boolean) {
  const selection = saleSelectionPath(pathname);
  if (!selection) {
    return null;
  }
  const parameters = Object.fromEntries(new URLSearchParams(search));
  const table = tableParams(parameters);
  const state: SalesState = {
    ...selection,
    filters: table.filters,
    page: table.page,
    agent: agentList(parameters),
  };
  return { ...state, href: salesHref(state, manager) };
}

export function salesListState(): Pick<SalesState, "filters" | "page" | "agent"> {
  return { filters: defaultTableFilters(), page: 1, agent: idleAgentList };
}

// The server page entry merges the path selection into the entry parameters.
export function salesEntryParameters(query: SearchParams, selection: SaleSelectionPath) {
  const rest = { ...query };
  for (const key of ["sale", "section", "version"]) {
    delete rest[key];
  }
  if (!selection.id) {
    return rest;
  }
  return {
    ...rest,
    sale: selection.id,
    section: selection.section,
    ...(selection.version === null ? {} : { version: String(selection.version) }),
  };
}
