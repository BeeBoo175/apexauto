import type { SearchParams } from "@/lib/query";

type RoomTeam = "sales" | "workshop";

// What the Who has room section shows: one team, an optional search and one page of people holding open work.
export interface RoomView {
  team: RoomTeam;
  search: string;
  page: number;
  pageSize: number;
}

export function workloadQuery(view: RoomView) {
  const query = new URLSearchParams({
    view: view.team,
    page: String(view.page),
    pageSize: String(view.pageSize),
  });
  const search = view.search.trim();
  if (search) {
    query.set("search", search);
  }
  return query.toString();
}

const roomSizes = [10, 25, 50];

const textOf = (value: string | string[] | undefined) =>
  (Array.isArray(value) ? value[0] : value) ?? "";

// What the address says about Who has room, read like the other staff pages read theirs.
export function parseRoomUrl(parameters: SearchParams): RoomView {
  const page = Number.parseInt(textOf(parameters.page), 10);
  const pageSize = Number.parseInt(textOf(parameters.pageSize), 10);
  return {
    team: textOf(parameters.view) === "workshop" ? "workshop" : "sales",
    search: textOf(parameters.search).trim().slice(0, 100),
    page: Number.isSafeInteger(page) && page > 0 ? page : 1,
    pageSize: roomSizes.includes(pageSize) ? pageSize : 25,
  };
}

export function roomUrlValues(view: RoomView) {
  return {
    view: view.team === "workshop" ? "workshop" : "",
    search: view.search.trim(),
    page: view.page > 1 ? String(view.page) : "",
    pageSize: view.pageSize === 25 ? "" : String(view.pageSize),
  };
}
