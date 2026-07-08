export type RoomTeam = "sales" | "workshop";

// What the Who has room section shows: one team, an optional search and one page of people holding open work.
export interface RoomView {
  team: RoomTeam;
  search: string;
  page: number;
  pageSize: number;
}

export const initialRoom: RoomView = { team: "sales", search: "", page: 1, pageSize: 25 };

export function workloadQuery(view: RoomView) {
  const query = new URLSearchParams({
    team: view.team,
    page: String(view.page),
    pageSize: String(view.pageSize),
  });
  const search = view.search.trim();
  if (search) {
    query.set("search", search);
  }
  return query.toString();
}
