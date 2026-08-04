import type { SearchParams } from "@/lib/query";
import { isUuid } from "@/lib/auth/contracts";
import { DEFAULT_PAGE_SIZE, isPageSize } from "@/lib/pagination";
import { staffRecordLocation } from "@/lib/staff/record-location";
import type { QueueFilters } from "./contracts";
import {
  inspectionQueryToken,
  queueParams,
  queueQuery,
  type RepairSelection,
  type WorkshopSection,
} from "./query";
import { technicianTasks, workshopDate, type TechnicianTask } from "./workshop-contracts";

export const workshopWorkspace = "/dashboard/inspection";

export type TechnicianQueueState = { search: string; page: number; pageSize: number };
export type TechnicianQueues = Record<TechnicianTask, TechnicianQueueState>;

const idleQueue: TechnicianQueueState = { search: "", page: 1, pageSize: DEFAULT_PAGE_SIZE };

// Managers have no worklist tabs; their state carries these neutral queues.
export const idleTechnicianQueues: TechnicianQueues = {
  MyWork: idleQueue,
  Waiting: idleQueue,
  Claimable: idleQueue,
};

export type WorkshopSelectionPath = {
  id: string;
  section: WorkshopSection;
  request: RepairSelection | null;
};

export type WorkshopState = WorkshopSelectionPath & {
  filters: QueueFilters;
  page: number;
  view: TechnicianTask;
  queues: TechnicianQueues;
};

// Shapes: base, /<id>, /<id>/history, /<id>/repairs and /<id>/repairs/<requestId>/revisions/<revision>.
// The default Inspection section has no segment.
export function workshopSelectionPath(pathname: string): WorkshopSelectionPath | null {
  const record = staffRecordLocation(pathname, workshopWorkspace);
  if (!record) {
    return null;
  }
  const { id, section, trail } = record;
  if (section === "" && trail.length === 0) {
    return { id, section: "inspection", request: null };
  }
  if (section === "history" && trail.length === 0) {
    return { id, section: "history", request: null };
  }
  if (section !== "repairs") {
    return null;
  }
  if (trail.length === 0) {
    return { id, section: "repairs", request: null };
  }
  const [requestId, keyword, revision, ...extra] = trail;
  if (
    !isUuid(requestId) ||
    keyword !== "revisions" ||
    !/^[1-9]\d{0,8}$/.test(revision ?? "") ||
    extra.length > 0
  ) {
    return null;
  }
  return { id, section: "repairs", request: { requestId, revision: Number(revision) } };
}

export function workshopSelectionRoute(id: string, section: string[] = []) {
  const path = workshopSelectionPath(`${workshopWorkspace}/${[id, ...section].join("/")}`);
  return path && path.id ? path : null;
}

export function technicianView(value: unknown): TechnicianTask {
  return technicianTasks.find((task) => inspectionQueryToken(task) === value) ?? "MyWork";
}

export function technicianQueueParams(params: SearchParams, task: TechnicianTask) {
  const search = params[`${inspectionQueryToken(task)}Search`];
  return typeof search === "string" ? search.trim() : "";
}

export function technicianQueuePaging(params: SearchParams, task: TechnicianTask) {
  const prefix = inspectionQueryToken(task);
  const page = Number(params[`${prefix}Page`] ?? 1);
  const pageSize = Number(params[`${prefix}PageSize`] ?? DEFAULT_PAGE_SIZE);
  return {
    page: Number.isSafeInteger(page) && page > 0 && page <= 2147483647 ? page : 1,
    pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
  };
}

function technicianQueues(params: SearchParams): TechnicianQueues {
  const queue = (task: TechnicianTask) => ({
    search: technicianQueueParams(params, task),
    ...technicianQueuePaging(params, task),
  });
  return { MyWork: queue("MyWork"), Waiting: queue("Waiting"), Claimable: queue("Claimable") };
}

function managerUrlQuery(filters: QueueFilters, page: number) {
  const query = new URLSearchParams(queueQuery(filters, page));
  const defaults: Record<string, string> = {
    page: "1",
    pageSize: String(DEFAULT_PAGE_SIZE),
    status: "active",
    sort: workshopDate(filters.status).column,
    direction: "asc",
  };
  for (const [key, value] of Object.entries(defaults)) {
    if (query.get(key) === value) {
      query.delete(key);
    }
  }
  return query;
}

function technicianUrlQuery(view: TechnicianTask, queues: TechnicianQueues) {
  const query = new URLSearchParams();
  if (view !== "MyWork") {
    query.set("view", inspectionQueryToken(view));
  }
  for (const task of technicianTasks) {
    const prefix = inspectionQueryToken(task);
    const { search, page, pageSize } = queues[task];
    if (search) {
      query.set(`${prefix}Search`, search);
    }
    if (page !== 1) {
      query.set(`${prefix}Page`, String(page));
    }
    if (pageSize !== DEFAULT_PAGE_SIZE) {
      query.set(`${prefix}PageSize`, String(pageSize));
    }
  }
  return query;
}

export function workshopSelectionHref({ id, section, request }: WorkshopSelectionPath) {
  if (!id) {
    return workshopWorkspace;
  }
  if (section === "history") {
    return `${workshopWorkspace}/${id}/history`;
  }
  if (section !== "repairs") {
    return `${workshopWorkspace}/${id}`;
  }
  const retained = request ? `/${request.requestId}/revisions/${request.revision}` : "";
  return `${workshopWorkspace}/${id}/repairs${retained}`;
}

export function workshopHref(state: WorkshopState, technician: boolean) {
  const query = technician
    ? technicianUrlQuery(state.view, state.queues)
    : managerUrlQuery(state.filters, state.page);
  const path = workshopSelectionHref(state);
  return query.size ? `${path}?${query}` : path;
}

export function workshopLocation(pathname: string, search: string, technician: boolean) {
  const selection = workshopSelectionPath(pathname);
  if (!selection) {
    return null;
  }
  const parameters = Object.fromEntries(new URLSearchParams(search));
  const { filters, page } = queueParams(parameters, "workshop");
  const state: WorkshopState = {
    ...selection,
    filters,
    page,
    view: technicianView(parameters.view),
    queues: technicianQueues(parameters),
  };
  return { ...state, href: workshopHref(state, technician) };
}

// The server page entry merges the path selection into the entry parameters.
export function workshopEntryParameters(query: SearchParams, selection: WorkshopSelectionPath) {
  const rest = { ...query };
  for (const key of ["inspection", "section", "request", "revision"]) {
    delete rest[key];
  }
  if (!selection.id) {
    return rest;
  }
  return {
    ...rest,
    inspection: selection.id,
    section: selection.section,
    ...(selection.request
      ? { request: selection.request.requestId, revision: String(selection.request.revision) }
      : {}),
  };
}
